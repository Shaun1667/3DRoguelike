using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>
    /// 들어가 있는 던전 하나의 판 상태(3-6 저장): 지금 선 방 · 깬 방 · 가 본 방 · 보상을 받은 방, 그리고 들어설 때 가졌던 도구.
    /// 층 모양 · 적 · 보상은 시드로 다시 만들고, 여기 있는 것만 그 위에 덮는다.
    /// 들어설 때 가졌던 도구로 보상을 정해야(<see cref="EntrySheet"/>) 던전 안에서 권총을 얻은 뒤 이어 해도 같은 보상이 나온다.
    /// </summary>
    public sealed class DungeonVisit
    {
        readonly HashSet<int> cleared = new HashSet<int>();
        readonly HashSet<int> visited = new HashSet<int>();
        readonly HashSet<int> rewardsTaken = new HashSet<int>();
        readonly List<RewardUnlock> entryUnlocks;

        public DungeonVisit(int dungeon, IEnumerable<RewardUnlock> entryUnlocks)
        {
            if (dungeon < 0) throw new ArgumentOutOfRangeException(nameof(dungeon));
            Dungeon = dungeon;
            this.entryUnlocks = new List<RewardUnlock>();
            if (entryUnlocks != null)
            {
                foreach (RewardUnlock unlock in entryUnlocks)
                    if (unlock != RewardUnlock.None && !this.entryUnlocks.Contains(unlock)) this.entryUnlocks.Add(unlock);
            }
            this.entryUnlocks.Sort();
        }

        /// <summary>길 위 던전 순서(= 난수 층 번호).</summary>
        public int Dungeon { get; }

        /// <summary>그 층의 깰 방 수를 판 기록에 이미 더했는가(이어 할 때 두 번 더하지 않게).</summary>
        public bool BattleRoomsCounted { get; set; }

        /// <summary>마지막으로 기록한 선 방. 아직 기록이 없으면 -1(층 처음 — 시작 방).</summary>
        public int CurrentRoom { get; private set; } = -1;

        public bool HasProgress => CurrentRoom >= 0;
        public IReadOnlyCollection<int> ClearedRooms => cleared;
        public IReadOnlyCollection<int> VisitedRooms => visited;
        public IReadOnlyCollection<int> RewardsTakenRooms => rewardsTaken;
        public IReadOnlyList<RewardUnlock> EntryUnlocks => entryUnlocks;

        /// <summary>들어설 때 가졌던 도구만 가진 강화 합계 — 보상 계획(<see cref="RewardPlanner"/>)에 넘긴다.</summary>
        public UpgradeSheet EntrySheet()
        {
            var sheet = new UpgradeSheet();
            foreach (RewardUnlock unlock in entryUnlocks) sheet.SetUnlocked(unlock, true);
            return sheet;
        }

        /// <summary>
        /// 들어설 때 가졌던 도구 + 이 판의 열린 보상 후보(3-7 성장 트리, 판 내내 같다). 보상 계획에 넘긴다.
        /// </summary>
        public UpgradeSheet EntrySheet(UpgradeSheet runSheet)
        {
            UpgradeSheet sheet = EntrySheet();
            if (runSheet != null) foreach (string id in runSheet.OpenedRewards()) sheet.OpenReward(id);
            return sheet;
        }

        /// <summary>
        /// 층 진행을 옮겨 적는다. 문이 잠긴 동안(싸우는 중)은 적지 않는다 — 저장은 안전한 지점에서만(핵심 설계 규칙 3).
        /// </summary>
        /// <returns>적었으면 true</returns>
        public bool Record(FloorProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (progress.LockedRoom >= 0) return false;
            cleared.Clear();
            visited.Clear();
            for (int id = 0; id < progress.Layout.Rooms.Count; id++)
            {
                if (progress.IsCleared(id)) cleared.Add(id);
                if (progress.IsVisited(id)) visited.Add(id);
            }
            CurrentRoom = progress.CurrentRoom;
            return true;
        }

        /// <summary>적어 둔 진행을 새로 만든 층에 덮는다. 적어 둔 것이 없으면 그대로(층 처음).</summary>
        public void ApplyTo(FloorProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (!HasProgress) return;
            progress.Restore(cleared, visited, CurrentRoom);
        }

        /// <summary>그 방의 보상(보상물)을 골랐다.</summary>
        public void MarkRewardTaken(int roomId)
        {
            if (roomId < 0) throw new ArgumentOutOfRangeException(nameof(roomId));
            rewardsTaken.Add(roomId);
        }

        public bool IsRewardTaken(int roomId) => rewardsTaken.Contains(roomId);

        /// <summary>저장 파일에서 되살릴 때만(<see cref="RunSaveRules"/>).</summary>
        internal void Load(int currentRoom, IEnumerable<int> clearedRooms, IEnumerable<int> visitedRooms, IEnumerable<int> takenRooms)
        {
            cleared.Clear(); visited.Clear(); rewardsTaken.Clear();
            if (clearedRooms != null) foreach (int id in clearedRooms) cleared.Add(id);
            if (visitedRooms != null) foreach (int id in visitedRooms) visited.Add(id);
            if (takenRooms != null) foreach (int id in takenRooms) rewardsTaken.Add(id);
            CurrentRoom = currentRoom;
        }
    }
}
