using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>장식을 두는 곳. 벽: 벽 앞 띠(벽에 붙여 안쪽을 봄). 방 안: 엄폐물(문 → 방 가운데 길 · 가운데 · 적 자리는 비움).</summary>
    public enum DressingPlace
    {
        Wall,
        Open,
    }

    /// <summary>장식 한 종류. 바닥 자리 크기는 돌리기 전 폭(x) · 깊이(z, 정면 -Z 방향)(m).</summary>
    public readonly struct DressingKind
    {
        public DressingKind(float width, float depth, float weight, DressingPlace place, float yawOffset, float yawJitter)
        {
            if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
            if (depth <= 0f) throw new ArgumentOutOfRangeException(nameof(depth));
            if (weight < 0f) throw new ArgumentOutOfRangeException(nameof(weight));
            if (yawJitter < 0f) throw new ArgumentOutOfRangeException(nameof(yawJitter));
            Width = width;
            Depth = depth;
            Weight = weight;
            Place = place;
            YawOffset = yawOffset;
            YawJitter = yawJitter;
        }

        public float Width { get; }
        public float Depth { get; }
        public float Weight { get; }
        public DressingPlace Place { get; }
        /// <summary>기본 방향에 더 돌리는 각도(도). 벽 장식은 기본이 정면(-Z)이 방 안쪽, 90이면 긴 변(z)이 벽을 따라 놓인다(차).</summary>
        public float YawOffset { get; }
        /// <summary>흔드는 각도(±도). 방 안 장식에 180이면 아무 방향.</summary>
        public float YawJitter { get; }
    }

    /// <summary>방 꾸미기 수치. 테마 에셋(FloorTheme)이 만든다.</summary>
    public sealed class DressingSettings
    {
        public DressingSettings(float cellSize, float wallThickness, IReadOnlyList<DressingKind> kinds,
            int wallMinPerCell, int wallMaxPerCell, int openMinPerCell, int openMaxPerCell,
            float wallBandDepth, float doorClearance, float spawnClearance, float centerClearance, float laneHalfWidth, float gap, float openWallMargin)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (wallThickness < 0f) throw new ArgumentOutOfRangeException(nameof(wallThickness));
            if (kinds == null) throw new ArgumentNullException(nameof(kinds));
            if (wallMinPerCell < 0 || wallMaxPerCell < wallMinPerCell) throw new ArgumentOutOfRangeException(nameof(wallMaxPerCell));
            if (openMinPerCell < 0 || openMaxPerCell < openMinPerCell) throw new ArgumentOutOfRangeException(nameof(openMaxPerCell));
            if (wallBandDepth <= wallThickness * 0.5f) throw new ArgumentOutOfRangeException(nameof(wallBandDepth));
            if (doorClearance < 0f || spawnClearance < 0f || centerClearance < 0f || laneHalfWidth < 0f || gap < 0f || openWallMargin < 0f)
                throw new ArgumentOutOfRangeException(nameof(doorClearance));

            CellSize = cellSize;
            WallThickness = wallThickness;
            Kinds = kinds;
            WallMinPerCell = wallMinPerCell;
            WallMaxPerCell = wallMaxPerCell;
            OpenMinPerCell = openMinPerCell;
            OpenMaxPerCell = openMaxPerCell;
            WallBandDepth = wallBandDepth;
            DoorClearance = doorClearance;
            SpawnClearance = spawnClearance;
            CenterClearance = centerClearance;
            LaneHalfWidth = laneHalfWidth;
            Gap = gap;
            OpenWallMargin = openWallMargin;
        }

        public float CellSize { get; }
        public float WallThickness { get; }
        public IReadOnlyList<DressingKind> Kinds { get; }
        /// <summary>칸 하나에 벽 장식 수 범위(큰 방은 칸 수만큼 곱함).</summary>
        public int WallMinPerCell { get; }
        public int WallMaxPerCell { get; }
        /// <summary>칸 하나에 방 안 엄폐물 수 범위. 시작 방 · 보물방은 0.</summary>
        public int OpenMinPerCell { get; }
        public int OpenMaxPerCell { get; }
        /// <summary>벽 장식이 방 경계에서 들어올 수 있는 깊이(m). 적 자리(벽에서 4 m 안쪽)보다 얕게.</summary>
        public float WallBandDepth { get; }
        /// <summary>문 가운데에서 장식까지 비우는 거리(m).</summary>
        public float DoorClearance { get; }
        /// <summary>적 자리에서 장식까지 비우는 거리(m).</summary>
        public float SpawnClearance { get; }
        /// <summary>방 가운데(보상물 자리)에서 비우는 거리(m).</summary>
        public float CenterClearance { get; }
        /// <summary>문 → 방 가운데 길에서 비우는 반폭(m).</summary>
        public float LaneHalfWidth { get; }
        /// <summary>장식끼리 · 벽과 띄우는 틈(m).</summary>
        public float Gap { get; }
        /// <summary>방 안 엄폐물이 방 경계에서 떨어질 거리(m).</summary>
        public float OpenWallMargin { get; }
    }

    /// <summary>놓을 장식 하나. 층 좌표(m), Yaw는 도(위에서 시계 방향, 0 = 정면 -Z가 남쪽).</summary>
    public readonly struct DressingPlacement
    {
        public DressingPlacement(int room, int kind, float x, float z, float yaw)
        {
            Room = room;
            Kind = kind;
            X = x;
            Z = z;
            Yaw = yaw;
        }

        public int Room { get; }
        public int Kind { get; }
        public float X { get; }
        public float Z { get; }
        public float Yaw { get; }

        public override string ToString() => $"{Room}:{Kind}@({X:0.###}, {Z:0.###}) {Yaw:0.#}°";
    }

    /// <summary>층의 방마다 정해 둔 장식.</summary>
    public sealed class FloorDressing
    {
        readonly IReadOnlyList<DressingPlacement>[] rooms;

        internal FloorDressing(IReadOnlyList<DressingPlacement>[] rooms)
        {
            this.rooms = rooms;
        }

        public int RoomCount => rooms.Length;

        public IReadOnlyList<DressingPlacement> For(int roomId)
        {
            if (roomId < 0 || roomId >= rooms.Length) throw new ArgumentOutOfRangeException(nameof(roomId));
            return rooms[roomId];
        }

        public IEnumerable<DressingPlacement> All()
        {
            foreach (IReadOnlyList<DressingPlacement> room in rooms)
                foreach (DressingPlacement placement in room) yield return placement;
        }
    }

    /// <summary>
    /// 던전 테마의 방 꾸미기(잔해 · 바리케이드 · 방벽 · 차)를 층 시작 때 한 번에 정한다. 난수는 꾸미기 용도(<see cref="RandomPurpose.Dressing"/>)로 받아
    /// 맵 · 적 · 보상 난수를 건드리지 않는다 — 테마가 없는 던전(숲)은 이것을 부르지 않으므로 그대로다.
    /// 막지 않기: 문 둘레 · 문 → 방 가운데 길 · 방 가운데(보상물) · 적 자리를 비우고, 벽 장식은 적 자리(벽에서 wallMargin)보다 얕은 띠에만 둔다.
    /// </summary>
    public static class RoomDressingPlanner
    {
        const int AttemptsPerProp = 30;
        const float LaneStep = 0.1f;

        public static FloorDressing Plan(FloorLayout layout, FloorEncounters encounters, DressingSettings settings, IRandom random)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (encounters == null) throw new ArgumentNullException(nameof(encounters));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var wallKinds = new List<int>();
            var openKinds = new List<int>();
            for (int i = 0; i < settings.Kinds.Count; i++)
            {
                if (settings.Kinds[i].Weight <= 0f) continue;
                (settings.Kinds[i].Place == DressingPlace.Wall ? wallKinds : openKinds).Add(i);
            }

            var rooms = new IReadOnlyList<DressingPlacement>[layout.Rooms.Count];
            foreach (FloorRoom room in layout.Rooms)
            {
                rooms[room.Id] = PlanRoom(layout, encounters.SpawnsFor(room.Id), settings, wallKinds, openKinds, random, room).AsReadOnly();
            }
            return new FloorDressing(rooms);
        }

        /// <summary>돌린 장식의 바닥 자리를 축에 맞춘 상자(반폭 x, z)로.</summary>
        public static (float x, float z) Extents(DressingKind kind, float yawDegrees)
        {
            double radians = yawDegrees * Math.PI / 180.0;
            float c = Math.Abs((float)Math.Cos(radians));
            float s = Math.Abs((float)Math.Sin(radians));
            float hw = kind.Width * 0.5f;
            float hd = kind.Depth * 0.5f;
            return (c * hw + s * hd, s * hw + c * hd);
        }

        static List<DressingPlacement> PlanRoom(FloorLayout layout, IReadOnlyList<SpawnPoint> spawns, DressingSettings settings,
            List<int> wallKinds, List<int> openKinds, IRandom random, FloorRoom room)
        {
            var placed = new List<DressingPlacement>();
            var boxes = new List<Box>();
            int cells = room.Width * room.Height;

            (float cx, float cz) = FloorSpace.RoomCenter(layout, settings.CellSize, room);
            float halfX = room.Width * settings.CellSize * 0.5f;
            float halfZ = room.Height * settings.CellSize * 0.5f;

            var doors = new List<(float x, float z)>();
            foreach (FloorDoor door in layout.Doors)
            {
                if (door.RoomA == room.Id || door.RoomB == room.Id) doors.Add(FloorSpace.DoorCenter(layout, settings.CellSize, door));
            }

            // 수를 먼저 뽑는다(종류가 없어도 같은 난수 흐름)
            int wallCount = random.Range(settings.WallMinPerCell * cells, settings.WallMaxPerCell * cells + 1);
            bool openAllowed = room.Kind == RoomKind.Battle || room.Kind == RoomKind.Boss;
            int openCount = openAllowed ? random.Range(settings.OpenMinPerCell * cells, settings.OpenMaxPerCell * cells + 1) : 0;

            for (int n = 0; n < wallCount && wallKinds.Count > 0; n++)
            {
                int kindIndex = PickKind(settings.Kinds, wallKinds, random);
                DressingKind kind = settings.Kinds[kindIndex];
                for (int attempt = 0; attempt < AttemptsPerProp; attempt++)
                {
                    int side = random.Next(4); // 0 북(+z) · 1 동(+x) · 2 남(-z) · 3 서(-x)
                    float along = random.NextFloat() * 2f - 1f;
                    float yaw = Wrap(SideYaw(side) + kind.YawOffset + (random.NextFloat() * 2f - 1f) * kind.YawJitter);
                    (float ex, float ez) = Extents(kind, yaw);
                    bool northSouth = side == 0 || side == 2;
                    float normalExtent = northSouth ? ez : ex;
                    float alongExtent = northSouth ? ex : ez;
                    float alongHalf = (northSouth ? halfX : halfZ) - settings.WallThickness * 0.5f - alongExtent - settings.Gap;
                    if (alongHalf <= 0f) continue;
                    // 벽 안쪽 면에서 틈만큼 떨어져 벽에 붙임. 방 경계에서 장식 안쪽 끝까지가 띠 깊이를 넘으면 버림
                    float fromEdge = settings.WallThickness * 0.5f + settings.Gap + normalExtent;
                    if (fromEdge + normalExtent > settings.WallBandDepth) continue;
                    float x, z;
                    switch (side)
                    {
                        case 0: x = cx + along * alongHalf; z = cz + halfZ - fromEdge; break;
                        case 1: x = cx + halfX - fromEdge; z = cz + along * alongHalf; break;
                        case 2: x = cx + along * alongHalf; z = cz - halfZ + fromEdge; break;
                        default: x = cx - halfX + fromEdge; z = cz + along * alongHalf; break;
                    }
                    var box = new Box(x, z, ex, ez);
                    if (!Clear(box, boxes, doors, spawns, settings, settings.DoorClearance)) continue;
                    boxes.Add(box);
                    placed.Add(new DressingPlacement(room.Id, kindIndex, x, z, yaw));
                    break;
                }
            }

            for (int n = 0; n < openCount && openKinds.Count > 0; n++)
            {
                int kindIndex = PickKind(settings.Kinds, openKinds, random);
                DressingKind kind = settings.Kinds[kindIndex];
                for (int attempt = 0; attempt < AttemptsPerProp; attempt++)
                {
                    float yaw = Wrap(kind.YawOffset + (random.NextFloat() * 2f - 1f) * kind.YawJitter);
                    (float ex, float ez) = Extents(kind, yaw);
                    float rangeX = halfX - settings.OpenWallMargin - ex;
                    float rangeZ = halfZ - settings.OpenWallMargin - ez;
                    if (rangeX <= 0f || rangeZ <= 0f) continue;
                    float x = cx + (random.NextFloat() * 2f - 1f) * rangeX;
                    float z = cz + (random.NextFloat() * 2f - 1f) * rangeZ;
                    var box = new Box(x, z, ex, ez);
                    if (box.DistanceTo(cx, cz) < settings.CenterClearance) continue;
                    if (!Clear(box, boxes, doors, spawns, settings, settings.DoorClearance)) continue;
                    if (OnLane(box, doors, cx, cz, settings.LaneHalfWidth)) continue;
                    boxes.Add(box);
                    placed.Add(new DressingPlacement(room.Id, kindIndex, x, z, yaw));
                    break;
                }
            }
            return placed;
        }

        /// <summary>문 · 적 자리 · 다른 장식과 떨어져 있는가.</summary>
        static bool Clear(Box box, List<Box> boxes, List<(float x, float z)> doors, IReadOnlyList<SpawnPoint> spawns, DressingSettings settings, float doorClearance)
        {
            foreach ((float x, float z) in doors) if (box.DistanceTo(x, z) < doorClearance) return false;
            foreach (SpawnPoint spawn in spawns) if (box.DistanceTo(spawn.X, spawn.Z) < settings.SpawnClearance) return false;
            foreach (Box other in boxes) if (box.Overlaps(other, settings.Gap)) return false;
            return true;
        }

        /// <summary>문 → 방 가운데 곧은 길 반폭 안에 걸치는가(보스 방까지 다니는 길).</summary>
        static bool OnLane(Box box, List<(float x, float z)> doors, float cx, float cz, float laneHalfWidth)
        {
            foreach ((float dx, float dz) in doors)
            {
                float length = (float)Math.Sqrt((cx - dx) * (cx - dx) + (cz - dz) * (cz - dz));
                int steps = Math.Max(1, (int)Math.Ceiling(length / LaneStep));
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    if (box.DistanceTo(dx + (cx - dx) * t, dz + (cz - dz) * t) < laneHalfWidth) return true;
                }
            }
            return false;
        }

        static int PickKind(IReadOnlyList<DressingKind> kinds, List<int> candidates, IRandom random)
        {
            float total = 0f;
            foreach (int i in candidates) total += kinds[i].Weight;
            float roll = random.NextFloat() * total;
            foreach (int i in candidates)
            {
                roll -= kinds[i].Weight;
                if (roll < 0f) return i;
            }
            return candidates[candidates.Count - 1];
        }

        /// <summary>그 벽 앞에서 정면(-Z)이 방 안쪽을 보는 각도.</summary>
        static float SideYaw(int side)
        {
            switch (side)
            {
                case 0: return 0f;    // 북쪽 벽: 정면이 남쪽(-z)
                case 1: return 90f;   // 동쪽 벽: 정면이 서쪽(-x)
                case 2: return 180f;  // 남쪽 벽: 정면이 북쪽
                default: return 270f; // 서쪽 벽: 정면이 동쪽
            }
        }

        static float Wrap(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }

        readonly struct Box
        {
            public Box(float x, float z, float ex, float ez)
            {
                X = x;
                Z = z;
                Ex = ex;
                Ez = ez;
            }

            public float X { get; }
            public float Z { get; }
            public float Ex { get; }
            public float Ez { get; }

            /// <summary>점에서 상자까지 거리(안이면 0).</summary>
            public float DistanceTo(float px, float pz)
            {
                float dx = Math.Max(0f, Math.Abs(px - X) - Ex);
                float dz = Math.Max(0f, Math.Abs(pz - Z) - Ez);
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }

            public bool Overlaps(Box other, float gap)
            {
                return Math.Abs(X - other.X) < Ex + other.Ex + gap && Math.Abs(Z - other.Z) < Ez + other.Ez + gap;
            }
        }
    }
}
