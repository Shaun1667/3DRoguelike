using System;
using System.Collections.Generic;
using System.IO;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 판 밖 진행(3-7)을 들고 있는 곳: 성장 트리 표 · 영구 재화 · 산 노드 · 재생 횟수. 판 저장(<see cref="CurrentRun"/>)과 따로 저장한다
    /// (<see cref="MetaSaveStore"/>, meta_progress.json — 판이 끝나도 남음). 규칙은 <see cref="MetaProgress"/> · <see cref="MetaTree"/>.
    /// 흐름: 판 시작 = 산 노드를 판에 건다(<see cref="ApplyOwned"/>) → 판 끝(사망 · 클리어) = 판 기록으로 번 재화를 더한다(<see cref="Bank"/>)
    /// → 결과 화면 · 월드 시작 지점의 트리 화면에서 산다(<see cref="TryBuy"/>, 아무것도 안 한 판이면 그 판에도 바로).
    /// </summary>
    public static class MetaRun
    {
        private static MetaTreeTable table;
        private static MetaProgress progress;
        private static RunSession bankedSession;

        /// <summary>노드를 샀거나 재화 · 트리가 바뀐 뒤(화면이 다시 그린다).</summary>
        public static event Action Changed;

        public static MetaTree Tree { get; private set; }
        public static MetaEarnings Earnings { get; private set; }
        public static bool Ready => Tree != null;

        /// <summary>마지막으로 더한 판의 번 양 · 그 뒤 합계(결과 화면).</summary>
        public static int LastEarned { get; private set; }
        public static int LastTotal { get; private set; }

        public static MetaSaveStore Store => MetaSaveStore.Default;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            table = null;
            Tree = null;
            Earnings = null;
            progress = null;
            bankedSession = null;
            LastEarned = LastTotal = 0;
            Changed = null;
        }

        /// <summary>월드 · 층 진행자가 부른다(WorldTuning.metaTree). 표가 없으면 판 밖 진행 없이 지금 숫자 그대로.</summary>
        public static void Use(MetaTreeTable treeTable)
        {
            if (treeTable == null)
            {
                if (table == null) Debug.LogWarning("[MetaRun] 성장 트리 표가 없습니다(WorldTuning.metaTree) — 영구 재화 · 트리 없이 진행");
                return;
            }
            if (table == treeTable && Tree != null) return;
            table = treeTable;
            Tree = treeTable.ToTree();
            Earnings = treeTable.ToEarnings();
            progress = null; // 표가 바뀌면 다시 읽는다(모르는 노드를 거르려고)
        }

        /// <summary>판 밖 진행. 처음 물을 때 파일에서 읽는다(깨진 파일은 백업 후 새로).</summary>
        public static MetaProgress Progress
        {
            get
            {
                if (progress == null) progress = Load();
                return progress;
            }
        }

        private static MetaProgress Load()
        {
            if (Tree == null) return new MetaProgress();
            MetaSaveStore store = Store;
            RunSaveStore.ReadResult read = store.TryRead(out MetaSaveData data, out string error);
            if (read == RunSaveStore.ReadResult.None) return new MetaProgress();
            if (read == RunSaveStore.ReadResult.Ok && MetaSaveRules.TryRestore(data, Tree, out MetaProgress loaded, out List<string> dropped, out error))
            {
                if (dropped.Count > 0) Debug.LogWarning($"[MetaRun] 트리에 없는 노드를 뺐습니다: {string.Join(", ", dropped)}");
                Debug.Log($"[MetaRun] 판 밖 진행 읽음 · 재화 {loaded.Currency} · 노드 {loaded.Owned.Count}/{Tree.Count} · 재생 {loaded.Runs}");
                return loaded;
            }
            try
            {
                string backup = store.BackUpBroken(DateTime.UtcNow);
                Debug.LogWarning($"[MetaRun] 판 밖 진행 파일을 읽지 못해 새로 시작합니다({error}). 백업: {backup}");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[MetaRun] 판 밖 진행 파일을 읽지 못했고({error}) 백업도 못 함: {exception.Message}");
            }
            return new MetaProgress();
        }

        public static bool Save(string reason)
        {
            if (Tree == null) return false;
            MetaSaveData data = MetaSaveRules.Capture(Progress, Tree);
            data.savedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            try
            {
                Store.Write(data);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[MetaRun] 판 밖 진행 저장 실패({reason}): {exception.Message}");
                return false;
            }
            Debug.Log($"[MetaRun] 판 밖 진행 저장({reason}) · 재화 {data.currency} · 노드 {data.owned.Length} · 재생 {data.runs}");
            return true;
        }

        /// <summary>새 판에 산 노드의 효과를 건다(표 순서). 아무것도 안 샀으면 아무것도 안 바뀐다.</summary>
        /// <returns>건 노드 수</returns>
        public static int ApplyOwned(RunSession session)
        {
            if (session == null || Tree == null) return 0;
            int applied = 0;
            foreach (MetaNode node in Progress.OwnedNodes(Tree)) if (session.ApplyMeta(node)) applied++;
            if (applied > 0) Debug.Log($"[MetaRun] 새 판에 성장 트리 노드 {applied}개를 걸었습니다: {string.Join(", ", session.MetaNodes)}");
            return applied;
        }

        /// <summary>판이 끝났다(사망 · 클리어): 판 기록으로 번 재화를 더하고 저장한다. 같은 판은 한 번만.</summary>
        /// <returns>이번에 더했으면 true</returns>
        public static bool Bank(RunSession session)
        {
            if (session == null || Tree == null || Earnings == null || ReferenceEquals(session, bankedSession)) return false;
            if (session.Stats.Outcome == RunOutcome.Playing) return false;
            bankedSession = session;
            LastEarned = Earnings.Earned(session.Stats);
            Progress.Bank(LastEarned);
            LastTotal = Progress.Currency;
            Save($"판 끝 +{LastEarned}");
            Debug.Log($"[MetaRun] 영구 재화 이번 판 +{LastEarned} · 합계 {LastTotal} (처치 {session.Stats.Kills} · 방 {session.Stats.RoomsCleared} · 던전 {session.Stats.DungeonsCleared})");
            Changed?.Invoke();
            return true;
        }

        /// <summary>그 판의 결과(결과 화면 한 줄). 더하지 않은 판이면 false.</summary>
        public static bool TryGetBank(RunStats stats, out int earned, out int total)
        {
            earned = LastEarned;
            total = LastTotal;
            return stats != null && bankedSession != null && ReferenceEquals(bankedSession.Stats, stats);
        }

        /// <summary>
        /// 노드를 산다. 지금 판이 아직 아무것도 안 한 판(<see cref="RunSession.IsUntouched"/>)이면 그 판에도 바로 건다.
        /// 아니면 다음 재생부터.
        /// </summary>
        public static bool TryBuy(string id, RunSession current, out string message)
        {
            if (Tree == null) { message = "성장 트리 표가 없음"; return false; }
            if (!Progress.TryBuy(Tree, id, out string error)) { message = error; return false; }
            Tree.TryGet(id, out MetaNode node);
            Save($"{node.Name} 삼");
            bool now = current != null && current.IsUntouched && current.ApplyMeta(node);
            if (now) CurrentRun.Save("성장 트리", syncFromScene: false);
            message = now ? $"'{node.Name}'을 샀다 — 이번 판부터 적용" : $"'{node.Name}'을 샀다 — 다음 재생부터 적용";
            Changed?.Invoke();
            return true;
        }

        // --- 디버그 창 ---

        public static void DebugAddCurrency(int amount)
        {
            if (Tree == null) return;
            Progress.DebugAdd(amount);
            Save($"디버그 재화 +{amount}");
            Changed?.Invoke();
        }

        public static void DebugResetTree()
        {
            if (Tree == null) return;
            Progress.DebugReset();
            Save("디버그 트리 초기화");
            Changed?.Invoke();
        }

        /// <returns>지금 판에도 건 노드 수(아무것도 안 한 판일 때만)</returns>
        public static int DebugBuyAll(RunSession current)
        {
            if (Tree == null) return 0;
            Progress.DebugOwnAll(Tree);
            Save("디버그 모두 사기");
            int now = current != null && current.IsUntouched ? ApplyOwned(current) : 0;
            if (now > 0) CurrentRun.Save("성장 트리", syncFromScene: false);
            Changed?.Invoke();
            return now;
        }
    }
}
