using System;

namespace Game.Rules
{
    /// <summary>
    /// 판 상태 한 곳(핵심 설계 규칙 3): 시드 · 판 기록(성향 · 강화) · 월드 진행 · 지금 있는 곳 · 플레이어 체력.
    /// 월드와 던전은 씬이 달라서, 씬을 오가는 동안 이것만 남는다. 3-6 저장도 이것을 저장한다.
    /// </summary>
    public sealed class RunSession
    {
        public const int InWorld = -1;
        public const int FullHealth = -1;

        public RunSession(RunSeed seed, int dungeonCount)
        {
            Seed = seed;
            World = new WorldProgress(dungeonCount);
            Stats = new RunStats(seed, 0);
            Stats.UseWorld(dungeonCount);
        }

        public RunSeed Seed { get; }
        public RunStats Stats { get; }
        public WorldProgress World { get; }

        /// <summary>들어가 있는 던전 순서. 월드면 <see cref="InWorld"/>.</summary>
        public int CurrentDungeon { get; private set; } = InWorld;

        /// <summary>월드로 돌아올 때 설 던전 입구. 아직 깬 던전이 없으면 -1(길 시작).</summary>
        public int ReturnDungeon { get; private set; } = -1;

        /// <summary>씬을 옮길 때 넘기는 체력. 판 처음은 <see cref="FullHealth"/>.</summary>
        public int PlayerHealth { get; private set; } = FullHealth;

        public bool InDungeon => CurrentDungeon != InWorld;

        /// <returns>들어갈 수 있어서 들어갔으면 true</returns>
        public bool EnterDungeon(int dungeon, int playerHealth)
        {
            if (Stats.Outcome != RunOutcome.Playing || InDungeon || !World.CanEnter(dungeon)) return false;
            CurrentDungeon = dungeon;
            PlayerHealth = playerHealth;
            return true;
        }

        /// <summary>들어가 있는 던전의 마지막 방을 깼다. 월드로 돌아갈 준비를 하고, 마지막 던전이면 지역을 깬 것으로 판이 끝난다.</summary>
        /// <returns>이번에 지역을 깨서 판이 끝났으면 true</returns>
        public bool FinishDungeon(int playerHealth, float elapsedSeconds)
        {
            if (!InDungeon) throw new InvalidOperationException("Not in a dungeon.");
            if (Stats.Outcome != RunOutcome.Playing) return false;
            World.Clear(CurrentDungeon);
            Stats.RecordDungeonCleared();
            ReturnDungeon = CurrentDungeon;
            CurrentDungeon = InWorld;
            PlayerHealth = playerHealth;
            return World.AllCleared && Stats.ClearRegion(elapsedSeconds);
        }
    }
}
