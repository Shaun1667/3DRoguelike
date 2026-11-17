using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>적이 나타날 자리와 그 자리의 적 종류. 층 좌표(m, <see cref="FloorSpace"/>).</summary>
    public readonly struct SpawnPoint
    {
        public SpawnPoint(float x, float z)
            : this(x, z, new EnemyKind(EnemySpecies.Badger, EnemyGrade.Mutant), 0)
        {
        }

        public SpawnPoint(float x, float z, EnemyKind kind, int group)
        {
            X = x;
            Z = z;
            Kind = kind;
            Group = group;
        }

        public float X { get; }
        public float Z { get; }
        public EnemyKind Kind { get; }
        /// <summary>방 안에서 같이 뽑힌 떼 번호(0부터). 같은 번호 = 같은 떼(멧토끼).</summary>
        public int Group { get; }

        public override string ToString() => $"({X:0.##}, {Z:0.##})";
    }

    /// <summary>방마다 적 수와 자리를 정하는 수치. 값은 BALANCE.md 10번.</summary>
    public sealed class EncounterSettings
    {
        public EncounterSettings(float cellSize, int minEnemies, int maxEnemies, int extraEnemiesPerCell, float wallMargin, float minSpacing, float doorClearance)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (minEnemies < 1) throw new ArgumentOutOfRangeException(nameof(minEnemies));
            if (maxEnemies < minEnemies) throw new ArgumentOutOfRangeException(nameof(maxEnemies));
            if (extraEnemiesPerCell < 0) throw new ArgumentOutOfRangeException(nameof(extraEnemiesPerCell));
            if (wallMargin < 0f || wallMargin * 2f >= cellSize) throw new ArgumentOutOfRangeException(nameof(wallMargin));
            if (minSpacing < 0f) throw new ArgumentOutOfRangeException(nameof(minSpacing));
            if (doorClearance < 0f) throw new ArgumentOutOfRangeException(nameof(doorClearance));

            CellSize = cellSize;
            MinEnemies = minEnemies;
            MaxEnemies = maxEnemies;
            ExtraEnemiesPerCell = extraEnemiesPerCell;
            WallMargin = wallMargin;
            MinSpacing = minSpacing;
            DoorClearance = doorClearance;
        }

        public float CellSize { get; }
        /// <summary>한 칸 방의 적 수 범위.</summary>
        public int MinEnemies { get; }
        public int MaxEnemies { get; }
        /// <summary>큰 방은 칸이 하나 늘 때마다 이만큼 더.</summary>
        public int ExtraEnemiesPerCell { get; }
        /// <summary>벽에서 떨어질 거리(m).</summary>
        public float WallMargin { get; }
        /// <summary>적끼리 떨어질 거리(m).</summary>
        public float MinSpacing { get; }
        /// <summary>문에서 떨어질 거리(m). 들어서자마자 붙어 있지 않게.</summary>
        public float DoorClearance { get; }
    }

    /// <summary>층의 방마다 정해 둔 적 자리.</summary>
    public sealed class FloorEncounters
    {
        readonly IReadOnlyList<SpawnPoint>[] spawns;

        internal FloorEncounters(IReadOnlyList<SpawnPoint>[] spawns)
        {
            this.spawns = spawns;
        }

        public int RoomCount => spawns.Length;

        /// <summary>그 방의 적 자리. 시작 방은 비어 있다.</summary>
        public IReadOnlyList<SpawnPoint> SpawnsFor(int roomId)
        {
            if (roomId < 0 || roomId >= spawns.Length) throw new ArgumentOutOfRangeException(nameof(roomId));
            return spawns[roomId];
        }
    }

    /// <summary>
    /// 층을 시작할 때 모든 방의 적 수 · 자리 · 종류를 한 번에 정한다. 난수는 적 용도(<see cref="RandomPurpose.Enemy"/>)로 받는다.
    /// 방 Id 순서로 정하므로, 같은 시드면 방에 들어가는 순서와 무관하게 같은 자리다.
    /// 방 적 수는 '자리'로 쓰고, 종은 <see cref="EnemyMixPlanner"/>가 그 자리를 나눠 고른다(멧토끼 떼는 자리 2개에 3~5마리 등).
    /// </summary>
    public static class EncounterPlanner
    {
        // 자리 하나를 찾는 재시도 상한. 넘으면 간격 조건만 빼고 그 자리에 둔다(적 수는 그대로).
        const int AttemptsPerSpawn = 50;

        /// <summary>오소리 Mutant만(종 구성 이전과 같은 자리).</summary>
        public static FloorEncounters Plan(FloorLayout layout, EncounterSettings settings, IRandom random)
        {
            return Plan(layout, settings, EnemyMix.Single(EnemySpecies.Badger), 0, random);
        }

        /// <param name="stage">적 구성 단계 = 던전 순서(0부터).</param>
        public static FloorEncounters Plan(FloorLayout layout, EncounterSettings settings, EnemyMix mix, int stage, IRandom random)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (mix == null) throw new ArgumentNullException(nameof(mix));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var spawns = new IReadOnlyList<SpawnPoint>[layout.Rooms.Count];
            foreach (FloorRoom room in layout.Rooms)
            {
                spawns[room.Id] = room.Kind == RoomKind.Start || room.Kind == RoomKind.Treasure
                    ? (IReadOnlyList<SpawnPoint>)Array.Empty<SpawnPoint>()
                    : PlanRoom(layout, settings, mix, stage, random, room).AsReadOnly();
            }
            return new FloorEncounters(spawns);
        }

        /// <summary>그 방의 적 자리 수(멧토끼 떼는 자리 여러 개를 쓴다).</summary>
        static int SlotsFor(EncounterSettings settings, IRandom random, FloorRoom room)
        {
            int extraCells = room.Width * room.Height - 1;
            return random.Range(settings.MinEnemies, settings.MaxEnemies + 1) + extraCells * settings.ExtraEnemiesPerCell;
        }

        static List<SpawnPoint> PlanRoom(FloorLayout layout, EncounterSettings settings, EnemyMix mix, int stage, IRandom random, FloorRoom room)
        {
            int slots = SlotsFor(settings, random, room);
            List<EnemyPick> picks = EnemyMixPlanner.Pick(mix, stage, slots, allowEvolved: true, random);

            var doors = new List<(float x, float z)>();
            foreach (FloorDoor door in layout.Doors)
            {
                if (door.RoomA == room.Id || door.RoomB == room.Id) doors.Add(FloorSpace.DoorCenter(layout, settings.CellSize, door));
            }

            (float centerX, float centerZ) = FloorSpace.RoomCenter(layout, settings.CellSize, room);
            float halfX = room.Width * settings.CellSize * 0.5f - settings.WallMargin;
            float halfZ = room.Height * settings.CellSize * 0.5f - settings.WallMargin;

            var points = new List<SpawnPoint>(slots);
            for (int group = 0; group < picks.Count; group++)
            {
                EnemyPick pick = picks[group];
                SpawnPoint candidate = default;
                for (int attempt = 0; attempt < AttemptsPerSpawn; attempt++)
                {
                    candidate = new SpawnPoint(
                        centerX + (random.NextFloat() * 2f - 1f) * halfX,
                        centerZ + (random.NextFloat() * 2f - 1f) * halfZ);
                    if (FarFromAll(candidate, points, settings.MinSpacing) && FarFromDoors(candidate, doors, settings.DoorClearance)) break;
                }

                if (pick.GroupSize == 1)
                {
                    points.Add(new SpawnPoint(candidate.X, candidate.Z, pick.Kind, group));
                    continue;
                }
                // 떼: 가운데를 둘러선다. 방 밖(벽 가까이)으로 나가면 안쪽으로 당긴다
                float spread = mix.Rule(pick.Kind.Species).GroupSpread;
                float startAngle = random.NextFloat() * (float)(Math.PI * 2.0);
                foreach ((float x, float z) in EnemyMixPlanner.GroupOffsets(pick.GroupSize, spread, startAngle))
                {
                    points.Add(new SpawnPoint(
                        Clamp(candidate.X + x, centerX - halfX, centerX + halfX),
                        Clamp(candidate.Z + z, centerZ - halfZ, centerZ + halfZ),
                        pick.Kind, group));
                }
            }
            return points;
        }

        static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        static bool FarFromAll(SpawnPoint candidate, List<SpawnPoint> points, float distance)
        {
            foreach (SpawnPoint point in points)
            {
                if (SquaredDistance(candidate.X, candidate.Z, point.X, point.Z) < distance * distance) return false;
            }
            return true;
        }

        static bool FarFromDoors(SpawnPoint candidate, List<(float x, float z)> doors, float distance)
        {
            foreach ((float x, float z) in doors)
            {
                if (SquaredDistance(candidate.X, candidate.Z, x, z) < distance * distance) return false;
            }
            return true;
        }

        static float SquaredDistance(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return dx * dx + dz * dz;
        }
    }
}
