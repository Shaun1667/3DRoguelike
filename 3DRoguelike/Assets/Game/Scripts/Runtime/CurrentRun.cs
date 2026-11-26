using System;
using Game.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// 씬을 오가는 동안 판 상태(<see cref="RunSession"/>)를 들고 있는 곳. 월드 씬이 판을 시작하고, 던전(층 씬)은 이어받는다.
    /// 판 시간은 <see cref="Time.time"/> 기준(씬을 옮겨도 이어지고, 보상 · 결과 화면으로 멈춘 동안은 흐르지 않음).
    /// </summary>
    public static class CurrentRun
    {
        public const string WorldScene = "Assets/Game/Scenes/World.unity";
        public const string FloorScene = "Assets/Game/Scenes/Floor.unity";

        private static float startTime;
        private static RunSeed? pendingSeed;

        /// <summary>진행 중인 판. 월드 없이 층 씬만 Play했으면 null.</summary>
        public static RunSession Session { get; private set; }

        /// <summary>판 시작부터 흐른 시간(초).</summary>
        public static float Elapsed => Time.time - startTime;

        // 도메인 다시 불러오기를 끈 Play에서도 지난 판이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Session = null;
            pendingSeed = null;
        }

        /// <summary>월드 씬이 부른다. 진행 중인 판이 있으면 그대로, 없거나 끝났으면 새 판.</summary>
        /// <param name="lastDungeonOpensNextRegion">마지막 던전 뒤에 다음 지역이 있는가(<see cref="WorldTuning.LastDungeonOpensRegion"/>)</param>
        public static RunSession EnsureSession(int dungeonCount, bool lastDungeonOpensNextRegion, Func<RunSeed> chooseSeed)
        {
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
            startTime = Time.time;
            Debug.Log($"[CurrentRun] 새 판 · 시드 {seed.ToCode()} · 던전 {dungeonCount}개");
            return Session;
        }

        /// <summary>결과 화면에서 다시: 판을 버리고 월드 길 시작부터. sameSeed면 같은 시드(같은 던전들).</summary>
        public static void RestartFromWorld(bool sameSeed)
        {
            pendingSeed = sameSeed && Session != null ? Session.Seed : (RunSeed?)null;
            Session = null;
            Time.timeScale = 1f;
            SceneManager.LoadScene(WorldScene);
        }
    }
}
