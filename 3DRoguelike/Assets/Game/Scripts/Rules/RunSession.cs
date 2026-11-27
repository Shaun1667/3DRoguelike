using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>
    /// 판 상태 한 곳(핵심 설계 규칙 3): 시드 · 판 기록(성향 · 강화) · 월드 진행 · 지금 있는 곳 · 플레이어 체력 · 탄,
    /// 던전 안이면 그 던전의 진행(<see cref="Visit"/>). 월드와 던전은 씬이 달라서, 씬을 오가는 동안 이것만 남는다.
    /// 3-6 저장도 이것을 저장한다(<see cref="RunSaveRules"/>).
    /// </summary>
    public sealed class RunSession
    {
        public const int InWorld = -1;
        public const int FullHealth = -1;
        /// <summary>탄창을 가득 채운 채로(씬을 옮길 때 · 판 처음).</summary>
        public const int FullAmmo = -1;

        public RunSession(RunSeed seed, int dungeonCount) : this(seed, dungeonCount, lastDungeonOpensNextRegion: false)
        {
        }

        /// <param name="lastDungeonOpensNextRegion">
        /// 마지막 던전 뒤 관문 너머에 다음 지역이 있는가(월드 지역 표가 정한다). true면 마지막 던전을 깨도 판이 끝나지 않고
        /// 관문만 열린다 — 결정 2026-11-25(사용자): 지역 던전을 깨면 다음 지역이 열리고, 다음 지역이 없는 마지막 던전을 깨면 클리어.
        /// </param>
        public RunSession(RunSeed seed, int dungeonCount, bool lastDungeonOpensNextRegion)
        {
            LastDungeonOpensNextRegion = lastDungeonOpensNextRegion;
            Seed = seed;
            World = new WorldProgress(dungeonCount);
            Stats = new RunStats(seed, 0);
            Stats.UseWorld(dungeonCount);
        }

        public RunSeed Seed { get; }
        /// <summary>마지막 던전을 깨면 다음 지역 관문이 열리는가(그러면 깨도 판이 이어진다).</summary>
        public bool LastDungeonOpensNextRegion { get; }
        public RunStats Stats { get; }
        public WorldProgress World { get; }

        /// <summary>들어가 있는 던전 순서. 월드면 <see cref="InWorld"/>.</summary>
        public int CurrentDungeon { get; internal set; } = InWorld;

        /// <summary>월드로 돌아올 때 설 던전 입구. 아직 깬 던전이 없으면 -1(길 시작).</summary>
        public int ReturnDungeon { get; internal set; } = -1;

        /// <summary>씬을 옮길 때 넘기는 체력. 판 처음은 <see cref="FullHealth"/>.</summary>
        public int PlayerHealth { get; internal set; } = FullHealth;

        /// <summary>탄창에 남은 탄. 씬을 옮기면 가득(<see cref="FullAmmo"/>) — 저장한 지점에서 이어 할 때만 남은 수를 쓴다.</summary>
        public int PlayerAmmo { get; internal set; } = FullAmmo;

        /// <summary>들어가 있는 던전의 진행. 월드면 null.</summary>
        public DungeonVisit Visit { get; internal set; }

        /// <summary>월드에서 마지막으로 저장한 자리(길 위 무리를 깬 곳 · 끈 곳). 없으면 입구 앞 · 길 시작.</summary>
        public bool HasWorldSpot { get; private set; }
        public float WorldX { get; private set; }
        public float WorldY { get; private set; }
        public float WorldZ { get; private set; }
        /// <summary>바라보는 방향(도, y축).</summary>
        public float WorldYaw { get; private set; }

        public bool InDungeon => CurrentDungeon != InWorld;

        readonly List<string> metaNodes = new List<string>();

        /// <summary>이 판에 건 성장 트리(3-7) 노드 id(건 순서). 판 저장에 들어가 이어 해도 같은 효과.</summary>
        public IReadOnlyList<string> MetaNodes => metaNodes;

        /// <summary>
        /// 성장 트리 노드 하나의 효과를 이 판에 건다: 능력치는 강화 합계에 더하고(성향은 그대로), 보상 후보를 연다.
        /// 이미 건 노드면 아무것도 안 한다.
        /// </summary>
        /// <returns>이번에 걸었으면 true</returns>
        public bool ApplyMeta(MetaNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (metaNodes.Contains(node.Id)) return false;
            metaNodes.Add(node.Id);
            Stats.Upgrades.AddEffects(node.Effects);
            if (node.OpensReward != null) Stats.Upgrades.OpenReward(node.OpensReward);
            return true;
        }

        /// <summary>
        /// 아직 아무것도 안 한 판(월드 길 시작, 처치 · 방 · 보상 · 던전 · 길 위 무리 모두 0). 이때 산 노드는 이 판에도 바로 건다 —
        /// 재생 직후 시작 지점에서 산 것과 결과 화면에서 산 것이 같게.
        /// </summary>
        public bool IsUntouched =>
            Stats.Outcome == RunOutcome.Playing && !InDungeon && World.ClearedCount == 0 && World.ClearedEncounters().Count == 0 &&
            Stats.Kills == 0 && Stats.RoomsCleared == 0 && Stats.RewardsTaken.Count == 0;


        /// <returns>들어갈 수 있어서 들어갔으면 true</returns>
        public bool EnterDungeon(int dungeon, int playerHealth)
        {
            return EnterDungeon(dungeon, playerHealth, null);
        }

        /// <param name="ownedUnlocks">들어설 때 가진 도구(보상 계획이 쓴다). null이면 판 기록의 강화 합계에서 읽는다.</param>
        /// <returns>들어갈 수 있어서 들어갔으면 true</returns>
        public bool EnterDungeon(int dungeon, int playerHealth, IEnumerable<RewardUnlock> ownedUnlocks)
        {
            if (Stats.Outcome != RunOutcome.Playing || InDungeon || !World.CanEnter(dungeon)) return false;
            CurrentDungeon = dungeon;
            PlayerHealth = playerHealth;
            PlayerAmmo = FullAmmo;
            Visit = new DungeonVisit(dungeon, ownedUnlocks ?? Stats.Upgrades.Unlocked());
            ClearWorldSpot();
            return true;
        }

        /// <summary>
        /// 들어가 있는 던전의 마지막 방을 깼다. 월드로 돌아갈 준비를 하고(그 뒤 관문이 열림), 다음 지역이 없는 마지막 던전이면
        /// 지역을 깬 것으로 판이 끝난다. 다음 지역이 있으면(<see cref="LastDungeonOpensNextRegion"/>) 판은 이어진다.
        /// </summary>
        /// <returns>이번에 지역을 깨서 판이 끝났으면 true</returns>
        public bool FinishDungeon(int playerHealth, float elapsedSeconds)
        {
            if (!InDungeon) throw new InvalidOperationException("Not in a dungeon.");
            if (Stats.Outcome != RunOutcome.Playing) return false;
            World.Clear(CurrentDungeon);
            Stats.RecordDungeonCleared();
            ReturnDungeon = CurrentDungeon;
            CurrentDungeon = InWorld;
            Visit = null;
            PlayerHealth = playerHealth;
            PlayerAmmo = FullAmmo;
            ClearWorldSpot();
            return World.AllCleared && !LastDungeonOpensNextRegion && Stats.ClearRegion(elapsedSeconds);
        }

        /// <summary>저장 직전에 지금 체력 · 탄을 옮겨 적는다(저장 지점에서만 부른다).</summary>
        public void UpdatePlayer(int playerHealth, int playerAmmo)
        {
            if (playerHealth < 0 && playerHealth != FullHealth) throw new ArgumentOutOfRangeException(nameof(playerHealth));
            if (playerAmmo < 0 && playerAmmo != FullAmmo) throw new ArgumentOutOfRangeException(nameof(playerAmmo));
            PlayerHealth = playerHealth;
            PlayerAmmo = playerAmmo;
        }

        /// <summary>월드에서 저장한 자리. 던전 안에서는 쓰지 않는다.</summary>
        public void SetWorldSpot(float x, float y, float z, float yaw)
        {
            if (InDungeon) throw new InvalidOperationException("The world spot is only kept while in the world.");
            HasWorldSpot = true;
            WorldX = x; WorldY = y; WorldZ = z; WorldYaw = yaw;
        }

        public void ClearWorldSpot()
        {
            HasWorldSpot = false;
            WorldX = WorldY = WorldZ = WorldYaw = 0f;
        }
    }
}
