using System;
using System.Collections.Generic;
using System.IO;
using Game.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// 저장 지점에서 지금 씬의 살아 있는 값(체력 · 탄 · 자리 · 층 진행)을 판 상태에 옮겨 적는 곳(월드 · 층 진행자).
    /// 싸우는 중이라 안전하지 않으면 false — 그러면 저장하지 않고 앞 저장이 남는다.
    /// </summary>
    public interface IRunSavePoint
    {
        bool SyncForSave(RunSession session);
    }

    /// <summary>
    /// 씬을 오가는 동안 판 상태(<see cref="RunSession"/>)를 들고 있는 곳. 월드 씬이 판을 시작하고, 던전(층 씬)은 이어받는다.
    /// 판 시간은 <see cref="Time.time"/> 기준(씬을 옮겨도 이어지고, 보상 · 결과 화면으로 멈춘 동안은 흐르지 않음).
    /// 판 중간 저장(3-6)도 여기서 부른다: 무엇을 담는지는 규칙 <see cref="RunSaveRules"/>, 파일은 <see cref="RunSaveStore"/>.
    /// 저장 시점: 판 시작 · 방을 깬 때 · 보상을 고른 때 · 던전에 들어갈 때 · 나올 때 · 길 위 무리를 깬 때 · 끌 때(싸우는 중이면 저장 안 함).
    /// 판이 끝나면(사망 · 클리어) 저장을 지운다.
    /// </summary>
    public static class CurrentRun
    {
        public const string WorldScene = "Assets/Game/Scenes/World.unity";
        public const string FloorScene = "Assets/Game/Scenes/Floor.unity";

        private static float startTime;
        private static RunSeed? pendingSeed;
        private static IRunSavePoint savePoint;
        private static bool launchChecked;

        /// <summary>진행 중인 판. 월드 없이 층 씬만 Play했으면 null.</summary>
        public static RunSession Session { get; private set; }

        /// <summary>판 시작부터 흐른 시간(초).</summary>
        public static float Elapsed => Time.time - startTime;

        /// <summary>마지막 저장(디버그 창 표시용). 없으면 빈 글자.</summary>
        public static string LastSaveNote { get; private set; } = "";

        /// <summary>이번 실행에서 저장을 쓴 횟수.</summary>
        public static int SaveCount { get; private set; }

        public static RunSaveStore Store => RunSaveStore.Default;

        // 도메인 다시 불러오기를 끈 Play에서도 지난 판이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Session = null;
            pendingSeed = null;
            savePoint = null;
            launchChecked = false;
            LastSaveNote = "";
            SaveCount = 0;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        /// <summary>월드 씬이 부른다. 진행 중인 판이 있으면 그대로, 없거나 끝났으면 새 판.</summary>
        /// <param name="lastDungeonOpensNextRegion">마지막 던전 뒤에 다음 지역이 있는가(<see cref="WorldTuning.LastDungeonOpensRegion"/>)</param>
        public static RunSession EnsureSession(int dungeonCount, bool lastDungeonOpensNextRegion, Func<RunSeed> chooseSeed)
        {
            return EnsureSession(dungeonCount, lastDungeonOpensNextRegion, chooseSeed, out _);
        }

        /// <param name="created">이번에 새 판을 만들었으면 true</param>
        public static RunSession EnsureSession(int dungeonCount, bool lastDungeonOpensNextRegion, Func<RunSeed> chooseSeed, out bool created)
        {
            created = false;
            if (Session != null && Session.Stats.Outcome == RunOutcome.Playing && Session.World.DungeonCount == dungeonCount
                && Session.LastDungeonOpensNextRegion == lastDungeonOpensNextRegion) return Session;
            RunSeed seed;
            if (pendingSeed.HasValue)
            {
                seed = pendingSeed.Value;
                pendingSeed = null;
            }
            else seed = chooseSeed();
            Session = new RunSession(seed, dungeonCount, lastDungeonOpensNextRegion);
            MetaRun.ApplyOwned(Session); // 재생(3-7): 산 성장 트리 노드를 새 판에 건다 — 아무것도 안 샀으면 그대로
            startTime = Time.time;
            created = true;
            Debug.Log($"[CurrentRun] 새 판 · 시드 {seed.ToCode()} · 던전 {dungeonCount}개");
            return Session;
        }

        /// <summary>결과 화면에서 다시: 판을 버리고 월드 길 시작부터. sameSeed면 같은 시드(같은 던전들).</summary>
        public static void RestartFromWorld(bool sameSeed)
        {
            pendingSeed = sameSeed && Session != null ? Session.Seed : (RunSeed?)null;
            Session = null;
            launchChecked = true; // 다시 시작은 이어 하기를 묻지 않는다(끝난 판의 저장은 이미 지움)
            Time.timeScale = 1f;
            SceneManager.LoadScene(WorldScene);
        }

        // --- 저장 · 이어 하기 (3-6) ---

        /// <summary>지금 씬의 진행자(월드 · 층)가 자기를 알린다. 끌 때 저장이 이것에 묻는다.</summary>
        public static void UseSavePoint(IRunSavePoint point) => savePoint = point;

        public static void ReleaseSavePoint(IRunSavePoint point)
        {
            if (savePoint == point) savePoint = null;
        }

        /// <summary>
        /// 판을 저장한다. syncFromScene이면 지금 씬에서 체력 · 탄 · 자리를 옮겨 적고, 싸우는 중이면 저장하지 않는다.
        /// 판이 끝났으면 저장을 지운다. 층 씬만 Play(판 없음)이면 아무것도 안 한다.
        /// </summary>
        /// <returns>파일을 썼으면 true</returns>
        public static bool Save(string reason, bool syncFromScene = true)
        {
            if (Session == null) return false;
            if (!RunSaveRules.ShouldKeep(Session))
            {
                DeleteSave("판 끝");
                return false;
            }
            if (syncFromScene && savePoint != null && !savePoint.SyncForSave(Session))
            {
                Debug.Log($"[CurrentRun] 저장 안 함({reason}): 싸우는 중 — 앞 저장 그대로");
                return false;
            }
            RunSaveData data = RunSaveRules.Capture(Session, Elapsed);
            data.reason = reason ?? "";
            data.savedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            try
            {
                Store.Write(data);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[CurrentRun] 저장 실패({reason}): {exception.Message}");
                return false;
            }
            SaveCount++;
            LastSaveNote = $"{reason} · {DateTime.Now:HH:mm:ss}";
            Debug.Log($"[CurrentRun] 저장({reason}) · 시드 {data.seed} · {(Session.InDungeon ? $"던전 {Session.CurrentDungeon} 방 {data.currentRoom} · 깬 방 {data.clearedRooms.Length}" : "월드")} · 체력 {data.playerHealth} · 탄 {data.playerAmmo}");
            return true;
        }

        /// <summary>저장을 지운다(판 끝 · 새 판 고름 · 디버그).</summary>
        public static bool DeleteSave(string reason)
        {
            try
            {
                if (!Store.Delete()) return false;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[CurrentRun] 저장 지우기 실패({reason}): {exception.Message}");
                return false;
            }
            LastSaveNote = $"지움({reason}) · {DateTime.Now:HH:mm:ss}";
            Debug.Log($"[CurrentRun] 저장 지움({reason})");
            return true;
        }

        /// <summary>
        /// 판이 끝났다(사망 · 클리어): 저장을 지워 같은 판을 되살리지 못하게 하고, 판 기록으로 번 영구 재화를 판 밖 진행에 더한다(3-7).
        /// </summary>
        public static void RunEnded()
        {
            if (Session == null || RunSaveRules.ShouldKeep(Session)) return;
            DeleteSave(Session.Stats.Outcome.ToString());
            MetaRun.Bank(Session);
        }

        private static void OnQuitting()
        {
            if (Session == null) return;
            Save("끔");
        }

        /// <summary>
        /// 게임을 켜고 처음 월드에 왔을 때 한 번: 이어 할 저장이 있으면 되살린 판을 돌려준다(아직 쓰지 않음).
        /// 깨진 파일 · 형식이 다른 파일 · 월드가 바뀐 파일은 백업하고 false(새 판).
        /// </summary>
        public static bool TryTakeSavedRun(int dungeonCount, bool lastDungeonOpensNextRegion, IReadOnlyList<RewardDefinition> rewardTable,
                                           out RunSession saved, out float elapsedSeconds, out string summary)
        {
            saved = null;
            elapsedSeconds = 0f;
            summary = null;
            if (launchChecked) return false;
            launchChecked = true;
            RunSaveStore store = Store;
            RunSaveStore.ReadResult read = store.TryRead(out RunSaveData data, out string error);
            if (read == RunSaveStore.ReadResult.None) return false;
            if (read == RunSaveStore.ReadResult.Ok && rewardTable != null
                && RunSaveRules.TryRestore(data, rewardTable, dungeonCount, lastDungeonOpensNextRegion, out saved, out elapsedSeconds, out error, MetaRun.Tree))
            {
                summary = $"시드 {data.seed} · 깬 던전 {data.dungeonsCleared}/{data.dungeonCount}" +
                          (data.currentDungeon >= 0 ? $" · 던전 {data.currentDungeon + 1} 안" : " · 월드") +
                          $" · 깬 방 {data.roomsCleared} · 시간 {RunStats.FormatTime(elapsedSeconds)}" +
                          (DateTime.TryParse(data.savedAtUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime savedAt)
                              ? $"\n저장 {savedAt.ToLocalTime():yyyy-MM-dd HH:mm} ({data.reason})" : "");
                return true;
            }
            if (rewardTable == null) error = "보상 표를 찾지 못함(WorldTuning.rewardTable)";
            BackUpBroken(store, error);
            return false;
        }

        private static void BackUpBroken(RunSaveStore store, string error)
        {
            try
            {
                string backup = store.BackUpBroken(DateTime.UtcNow);
                Debug.LogWarning($"[CurrentRun] 저장을 읽지 못해 새 판으로 시작합니다({error}). 백업: {backup}");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[CurrentRun] 저장을 읽지 못했고({error}) 백업도 못 함: {exception.Message}");
            }
        }

        /// <summary>층 씬이 저장한 층 진행을 올리지 못했을 때(저장이 층과 맞지 않음): 파일을 백업해 둔다.</summary>
        public static void ReportBrokenSave(string error) => BackUpBroken(Store, error);

        /// <summary>[이어 하기]: 되살린 판을 쓰고 그 판이 있던 씬(월드 · 던전)을 연다. 맵 · 적 · 보상은 같은 시드로 다시 만든다.</summary>
        public static void Resume(RunSession saved, float elapsedSeconds)
        {
            Session = saved ?? throw new ArgumentNullException(nameof(saved));
            pendingSeed = null;
            startTime = Time.time - elapsedSeconds;
            Time.timeScale = 1f;
            Debug.Log($"[CurrentRun] 이어 하기 · 시드 {saved.Seed.ToCode()} · {(saved.InDungeon ? $"던전 {saved.CurrentDungeon}" : "월드")}");
            SceneManager.LoadScene(saved.InDungeon ? FloorScene : WorldScene);
        }
    }
}
