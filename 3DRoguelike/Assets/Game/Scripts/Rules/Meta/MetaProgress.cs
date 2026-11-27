using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>판에서 영구 재화를 얼마나 버나(3-7). 값은 BALANCE 16번, 데이터는 Data/MetaTree.asset.</summary>
    public sealed class MetaEarnings
    {
        public MetaEarnings(int perKill, int perRoom, int perDungeon, int perRegionClear)
        {
            if (perKill < 0 || perRoom < 0 || perDungeon < 0 || perRegionClear < 0) throw new ArgumentOutOfRangeException(nameof(perKill));
            PerKill = perKill;
            PerRoom = perRoom;
            PerDungeon = perDungeon;
            PerRegionClear = perRegionClear;
        }

        public int PerKill { get; }
        public int PerRoom { get; }
        public int PerDungeon { get; }
        public int PerRegionClear { get; }

        /// <summary>
        /// 판 기록으로 번 양: 처치 · 깬 방 · 깬 던전 · 지역 클리어. 판 기록 수는 판 저장(3-6)에 이미 들어 있어서
        /// 따로 모으는 칸 없이 판이 끝날 때 한 번 계산한다(이어 해도 같은 값).
        /// </summary>
        public int Earned(RunStats stats)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            int sum = stats.Kills * PerKill + stats.RoomsCleared * PerRoom + stats.DungeonsCleared * PerDungeon;
            if (stats.Outcome == RunOutcome.RegionCleared) sum += PerRegionClear;
            return sum;
        }
    }

    /// <summary>노드가 지금 어떤 상태인가(트리 화면).</summary>
    public enum MetaNodeState
    {
        Owned,
        /// <summary>앞 노드를 아직 안 삼.</summary>
        Locked,
        /// <summary>살 수 있지만 재화가 모자람.</summary>
        TooExpensive,
        Buyable,
    }

    /// <summary>
    /// 판 밖 진행(3-7, 핵심 설계 규칙 3: 판 저장과 따로): 영구 재화 · 산 노드 · 재생 횟수. 판이 끝나도(사망 · 클리어) 남는다.
    /// 파일은 <see cref="MetaSaveRules"/> → 화면 쪽 MetaSaveStore(persistentDataPath/meta_progress.json).
    /// </summary>
    public sealed class MetaProgress
    {
        readonly HashSet<string> owned = new HashSet<string>();

        /// <summary>지금 가진 영구 재화.</summary>
        public int Currency { get; private set; }
        /// <summary>노드를 사는 데 쓴 합(트리 초기화 때 돌려준다).</summary>
        public int Spent { get; private set; }
        /// <summary>지금까지 번 합(보기용).</summary>
        public int TotalEarned { get; private set; }
        /// <summary>끝난 판 수 = 재생한 횟수.</summary>
        public int Runs { get; private set; }

        public IReadOnlyCollection<string> Owned => owned;
        public bool Owns(string id) => id != null && owned.Contains(id);

        public MetaNodeState StateOf(MetaNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (Owns(node.Id)) return MetaNodeState.Owned;
            if (node.Requires != null && !Owns(node.Requires)) return MetaNodeState.Locked;
            return Currency < node.Cost ? MetaNodeState.TooExpensive : MetaNodeState.Buyable;
        }

        /// <returns>샀으면 true. 아니면 까닭(이미 삼 · 앞 노드 · 재화 부족 · 모르는 노드).</returns>
        public bool TryBuy(MetaTree tree, string id, out string error)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            error = null;
            if (!tree.TryGet(id, out MetaNode node)) { error = $"모르는 노드 '{id}'"; return false; }
            switch (StateOf(node))
            {
                case MetaNodeState.Owned: error = "이미 삼"; return false;
                case MetaNodeState.Locked: error = tree.TryGet(node.Requires, out MetaNode before) ? $"먼저 '{before.Name}'" : "앞 노드 필요"; return false;
                case MetaNodeState.TooExpensive: error = $"재화 부족({Currency}/{node.Cost})"; return false;
            }
            Currency -= node.Cost;
            Spent += node.Cost;
            owned.Add(node.Id);
            return true;
        }

        /// <summary>판이 끝났다: 번 재화를 더하고 재생 횟수를 센다.</summary>
        public void Bank(int earned)
        {
            if (earned < 0) throw new ArgumentOutOfRangeException(nameof(earned));
            Currency += earned;
            TotalEarned += earned;
            Runs++;
        }

        /// <summary>표 순서로 산 노드들. 판을 시작할 때 이 순서로 효과를 건다.</summary>
        public List<MetaNode> OwnedNodes(MetaTree tree)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            var list = new List<MetaNode>();
            foreach (MetaNode node in tree.Nodes) if (owned.Contains(node.Id)) list.Add(node);
            return list;
        }

        // --- 디버그 창 ---

        /// <summary>재화를 더한다(번 합 · 재생 횟수는 그대로).</summary>
        public void DebugAdd(int amount)
        {
            Currency = Math.Max(0, Currency + amount);
        }

        /// <summary>산 노드를 모두 지우고 쓴 재화를 돌려준다.</summary>
        public void DebugReset()
        {
            owned.Clear();
            Currency += Spent;
            Spent = 0;
        }

        /// <summary>모든 노드를 재화 없이 갖는다.</summary>
        public void DebugOwnAll(MetaTree tree)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            foreach (MetaNode node in tree.Nodes) owned.Add(node.Id);
        }

        /// <summary>저장 파일에서 되살릴 때만(<see cref="MetaSaveRules"/>).</summary>
        internal void Load(int currency, int spent, int totalEarned, int runs, IEnumerable<string> ownedIds)
        {
            Currency = currency;
            Spent = spent;
            TotalEarned = totalEarned;
            Runs = runs;
            owned.Clear();
            foreach (string id in ownedIds) owned.Add(id);
        }
    }
}
