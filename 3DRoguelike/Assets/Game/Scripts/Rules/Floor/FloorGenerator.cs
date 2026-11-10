using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>
    /// 층 방 배치 생성. 시작 방(1×1)을 격자 가운데 두고, 기존 방 하나에만 닿는 새 방을 붙여 나무 모양으로 키운다.
    /// 난수는 맵 용도(<see cref="RandomPurpose.Map"/>)로 받는다. 같은 난수 = 같은 배치.
    /// </summary>
    public static class FloorGenerator
    {
        // 재시도 상한. 수치가 아니라 무한 반복을 막는 안전장치다.
        const int AttemptsPerLayout = 1000;
        const int MaxLayouts = 50;

        public static FloorLayout Generate(FloorLayoutSettings settings, IRandom random)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (random == null) throw new ArgumentNullException(nameof(random));

            int targetRooms = random.Range(settings.MinRooms, settings.MaxRooms + 1);
            for (int layout = 0; layout < MaxLayouts; layout++)
            {
                var builder = new Builder(settings);
                if (builder.TryGrow(targetRooms, random)) return builder.Finish();
            }
            throw new InvalidOperationException($"Could not place {targetRooms} rooms after {MaxLayouts} layouts.");
        }

        sealed class Builder
        {
            readonly FloorLayoutSettings settings;
            readonly int[] grid;
            readonly List<FloorRoom> rooms = new List<FloorRoom>();
            readonly List<FloorDoor> doors = new List<FloorDoor>();

            public Builder(FloorLayoutSettings settings)
            {
                this.settings = settings;
                grid = new int[settings.GridWidth * settings.GridHeight];
                for (int i = 0; i < grid.Length; i++) grid[i] = -1;
                Place(settings.GridWidth / 2, settings.GridHeight / 2, new RoomShape(1, 1));
            }

            public bool TryGrow(int targetRooms, IRandom random)
            {
                for (int attempt = 0; attempt < AttemptsPerLayout && rooms.Count < targetRooms; attempt++)
                {
                    var parent = rooms[random.Next(rooms.Count)];
                    int fromX = parent.X + random.Next(parent.Width);
                    int fromY = parent.Y + random.Next(parent.Height);
                    var direction = (GridDirection)random.Next(4);
                    int toX = fromX + direction.Dx();
                    int toY = fromY + direction.Dy();
                    if (IdAt(toX, toY) != -1 || !InBounds(toX, toY)) continue;

                    var shape = PickShape(random);
                    int x = toX - random.Next(shape.Width);
                    int y = toY - random.Next(shape.Height);
                    if (!CanPlace(x, y, shape, parent.Id)) continue;

                    var room = Place(x, y, shape);
                    doors.Add(new FloorDoor(parent.Id, room.Id, fromX, fromY, direction));
                }
                return rooms.Count == targetRooms;
            }

            public FloorLayout Finish()
            {
                var neighbours = new List<int>[rooms.Count];
                for (int i = 0; i < rooms.Count; i++) neighbours[i] = new List<int>();
                foreach (var door in doors)
                {
                    neighbours[door.RoomA].Add(door.RoomB);
                    neighbours[door.RoomB].Add(door.RoomA);
                }

                var queue = new Queue<int>();
                var visited = new bool[rooms.Count];
                queue.Enqueue(0);
                visited[0] = true;
                while (queue.Count > 0)
                {
                    int id = queue.Dequeue();
                    foreach (int next in neighbours[id])
                    {
                        if (visited[next]) continue;
                        visited[next] = true;
                        rooms[next].DistanceFromStart = rooms[id].DistanceFromStart + 1;
                        queue.Enqueue(next);
                    }
                }

                var deadEnds = new List<FloorRoom>();
                FloorRoom boss = null;
                foreach (var room in rooms)
                {
                    room.DoorCount = neighbours[room.Id].Count;
                    room.Kind = RoomKind.Battle;
                    if (room.Id == 0 || room.DoorCount != 1) continue;
                    deadEnds.Add(room);
                    if (boss == null || room.DistanceFromStart > boss.DistanceFromStart) boss = room;
                }
                rooms[0].Kind = RoomKind.Start;
                boss.Kind = RoomKind.Boss;

                return new FloorLayout(settings.GridWidth, settings.GridHeight, grid, rooms, doors, deadEnds, boss);
            }

            RoomShape PickShape(IRandom random)
            {
                int roll = random.Next(settings.TotalShapeWeight);
                foreach (var entry in settings.Shapes)
                {
                    if (roll < entry.Weight) return entry.Shape;
                    roll -= entry.Weight;
                }
                return settings.Shapes[settings.Shapes.Count - 1].Shape;
            }

            /// <summary>칸이 모두 비어 있고, 바깥쪽으로 닿는 방이 부모 방뿐이어야 한다.</summary>
            bool CanPlace(int x, int y, RoomShape shape, int parentId)
            {
                for (int cy = y; cy < y + shape.Height; cy++)
                {
                    for (int cx = x; cx < x + shape.Width; cx++)
                    {
                        if (!InBounds(cx, cy) || IdAt(cx, cy) != -1) return false;
                        for (int d = 0; d < 4; d++)
                        {
                            var direction = (GridDirection)d;
                            int nx = cx + direction.Dx();
                            int ny = cy + direction.Dy();
                            bool insideNewRoom = nx >= x && nx < x + shape.Width && ny >= y && ny < y + shape.Height;
                            if (insideNewRoom) continue;
                            int neighbour = IdAt(nx, ny);
                            if (neighbour != -1 && neighbour != parentId) return false;
                        }
                    }
                }
                return true;
            }

            FloorRoom Place(int x, int y, RoomShape shape)
            {
                var room = new FloorRoom(rooms.Count, x, y, shape);
                rooms.Add(room);
                for (int cy = y; cy < y + shape.Height; cy++)
                {
                    for (int cx = x; cx < x + shape.Width; cx++) grid[cy * settings.GridWidth + cx] = room.Id;
                }
                return room;
            }

            bool InBounds(int x, int y)
            {
                return x >= 0 && y >= 0 && x < settings.GridWidth && y < settings.GridHeight;
            }

            int IdAt(int x, int y)
            {
                return InBounds(x, y) ? grid[y * settings.GridWidth + x] : -1;
            }
        }
    }
}
