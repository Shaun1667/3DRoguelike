using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>
    /// 월드 진행: 길 위의 던전은 순서대로만 들어가고, 던전을 깨면 그 뒤 관문이 열린다. 길 위 적 무리는 한 번 잡으면 그 판에서 다시 나오지 않는다.
    /// 판이 끝나면(죽음) 처음부터 — 판 상태라 <see cref="RunSession"/>이 가진다.
    /// </summary>
    public sealed class WorldProgress
    {
        private readonly bool[] cleared;
        private readonly HashSet<int> clearedEncounters = new HashSet<int>();

        public WorldProgress(int dungeonCount)
        {
            if (dungeonCount < 1) throw new ArgumentOutOfRangeException(nameof(dungeonCount));
            cleared = new bool[dungeonCount];
        }

        public int DungeonCount => cleared.Length;
        public int ClearedCount { get; private set; }
        public bool AllCleared => ClearedCount == cleared.Length;

        /// <summary>다음에 깰 던전. 모두 깼으면 -1.</summary>
        public int NextDungeon => AllCleared ? -1 : ClearedCount;

        /// <summary>던전을 깬 순간. 값은 던전 순서.</summary>
        public event Action<int> DungeonCleared;

        public bool IsCleared(int dungeon) => cleared[dungeon];

        /// <summary>순서대로만 들어간다: 다음 던전만 열려 있다.</summary>
        public bool CanEnter(int dungeon) => dungeon == NextDungeon;

        /// <summary>던전 뒤 관문. 그 던전을 깨면 열린다.</summary>
        public bool IsGateOpen(int dungeon) => cleared[dungeon];

        /// <returns>이번에 깼으면 true. 순서가 아니거나 이미 깼으면 false.</returns>
        public bool Clear(int dungeon)
        {
            if (!CanEnter(dungeon)) return false;
            cleared[dungeon] = true;
            ClearedCount++;
            DungeonCleared?.Invoke(dungeon);
            return true;
        }

        public bool IsEncounterCleared(int encounter) => clearedEncounters.Contains(encounter);

        /// <returns>이번에 처음 잡았으면 true.</returns>
        public bool ClearEncounter(int encounter) => clearedEncounters.Add(encounter);
    }
}
