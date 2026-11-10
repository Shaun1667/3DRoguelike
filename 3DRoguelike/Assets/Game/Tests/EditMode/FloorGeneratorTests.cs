using System;
using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using NUnit.Framework;

namespace Game.Tests
{
    public class FloorGeneratorTests
    {
        const int SeedCount = 500;
        const int FloorsPerSeed = 3;

        /// <summary>BALANCE.md 7번과 같은 값.</summary>
        internal static FloorLayoutSettings BalanceSettings()
        {
            return new FloorLayoutSettings(9, 9, 8, 12, new[]
            {
                new WeightedRoomShape(new RoomShape(1, 1), 7),
                new WeightedRoomShape(new RoomShape(2, 1), 1),
                new WeightedRoomShape(new RoomShape(1, 2), 1),
                new WeightedRoomShape(new RoomShape(2, 2), 1),
            });
        }

        internal static FloorLayout Generate(uint seed, int floorIndex)
        {
            var random = new RunRandom(new RunSeed(seed)).Create(RandomPurpose.Map, floorIndex);
            return FloorGenerator.Generate(BalanceSettings(), random);
        }

        static List<FloorLayout> manyLayouts;

        /// <summary>시드 1000개(작은 수 500 + 흩어진 수 500) × 3층 = 3000개.</summary>
        static List<FloorLayout> ManyLayouts()
        {
            if (manyLayouts != null) return manyLayouts;
            manyLayouts = new List<FloorLayout>();
            for (uint i = 0; i < SeedCount; i++)
            {
                for (int floor = 0; floor < FloorsPerSeed; floor++)
                {
                    manyLayouts.Add(Generate(i, floor));
                    manyLayouts.Add(Generate(unchecked(i * 2654435761u + 1u), floor));
                }
            }
            return manyLayouts;
        }

        static string Describe(FloorLayout layout)
        {
            return layout.ToAscii() + "\n" + string.Join(" ", layout.Doors);
        }

        [Test]
        public void FixedSeed_GivesFixedLayout()
        {
            // 이 배치가 바뀌면 모든 시드의 층 모양이 바뀐다. 일부러 바꾼 게 아니면 코드를 되돌린다.
            var layout = Generate(0x12345678u, 0);

            Assert.That(layout.ToAscii(), Is.EqualTo(
                ".......67\n" +
                ".......4.\n" +
                ".....9.35\n" +
                ".....112.\n" +
                "....011..\n" +
                ".....88..\n" +
                ".........\n" +
                ".........\n" +
                "........."));
            Assert.That(string.Join(" ", layout.Doors), Is.EqualTo(
                "0-1@4,4E 1-2@6,5E 2-3@7,5N 3-4@7,6N 3-5@7,6E 4-6@7,7N 6-7@7,8E 1-8@6,4S 1-9@5,5N"));
            Assert.That(layout.BossRoom.Id, Is.EqualTo(7));
            Assert.That(layout.BossRoom.DistanceFromStart, Is.EqualTo(6));
        }

        [Test]
        public void SameRandom_GivesSameLayout()
        {
            for (int floor = 0; floor < FloorsPerSeed; floor++)
            {
                Assert.That(Describe(Generate(777u, floor)), Is.EqualTo(Describe(Generate(777u, floor))));
            }
        }

        [Test]
        public void DifferentFloors_GiveDifferentLayouts()
        {
            var layouts = new HashSet<string>();
            for (int floor = 0; floor < FloorsPerSeed; floor++) layouts.Add(Describe(Generate(0x12345678u, floor)));

            Assert.That(layouts.Count, Is.EqualTo(FloorsPerSeed));
        }

        [Test]
        public void ManySeeds_RoomCountInRange()
        {
            var counts = new HashSet<int>();
            foreach (var layout in ManyLayouts())
            {
                Assert.That(layout.Rooms.Count, Is.InRange(8, 12));
                counts.Add(layout.Rooms.Count);
            }

            Assert.That(counts.Count, Is.EqualTo(5), "8~12개가 모두 나와야 한다");
        }

        [Test]
        public void ManySeeds_RoomsInsideGridWithoutOverlap()
        {
            foreach (var layout in ManyLayouts())
            {
                int area = 0;
                foreach (var room in layout.Rooms)
                {
                    Assert.That(room.X >= 0 && room.Y >= 0 && room.X + room.Width <= layout.Width && room.Y + room.Height <= layout.Height, Is.True);
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                    {
                        for (int x = room.X; x < room.X + room.Width; x++) Assert.That(layout.RoomIdAt(x, y), Is.EqualTo(room.Id));
                    }
                    area += room.Width * room.Height;
                }

                int filled = 0;
                for (int y = 0; y < layout.Height; y++)
                {
                    for (int x = 0; x < layout.Width; x++)
                    {
                        if (layout.RoomIdAt(x, y) >= 0) filled++;
                    }
                }
                Assert.That(filled, Is.EqualTo(area));
            }
        }

        [Test]
        public void ManySeeds_EveryRoomReachableFromStart_AsTree()
        {
            foreach (var layout in ManyLayouts())
            {
                Assert.That(layout.Doors.Count, Is.EqualTo(layout.Rooms.Count - 1), "나무 모양이면 문 = 방 - 1");

                var reached = new HashSet<int> { 0 };
                var queue = new Queue<int>();
                queue.Enqueue(0);
                while (queue.Count > 0)
                {
                    int id = queue.Dequeue();
                    foreach (var door in layout.Doors)
                    {
                        int next = door.RoomA == id ? door.RoomB : door.RoomB == id ? door.RoomA : -1;
                        if (next >= 0 && reached.Add(next)) queue.Enqueue(next);
                    }
                }
                Assert.That(reached.Count, Is.EqualTo(layout.Rooms.Count), layout.ToAscii());
            }
        }

        [Test]
        public void ManySeeds_DoorsJoinTouchingCellsOfTheirRooms()
        {
            foreach (var layout in ManyLayouts())
            {
                foreach (var door in layout.Doors)
                {
                    Assert.That(layout.RoomIdAt(door.X, door.Y), Is.EqualTo(door.RoomA), door.ToString());
                    Assert.That(layout.RoomIdAt(door.OtherX, door.OtherY), Is.EqualTo(door.RoomB), door.ToString());
                    int distanceA = layout.Rooms[door.RoomA].DistanceFromStart;
                    int distanceB = layout.Rooms[door.RoomB].DistanceFromStart;
                    Assert.That(Math.Abs(distanceA - distanceB), Is.EqualTo(1), door.ToString());
                }
            }
        }

        [Test]
        public void ManySeeds_TouchingRoomsAlwaysHaveDoor()
        {
            foreach (var layout in ManyLayouts())
            {
                var doorPairs = new HashSet<(int, int)>();
                foreach (var door in layout.Doors) doorPairs.Add((Math.Min(door.RoomA, door.RoomB), Math.Max(door.RoomA, door.RoomB)));

                for (int y = 0; y < layout.Height; y++)
                {
                    for (int x = 0; x < layout.Width; x++)
                    {
                        int here = layout.RoomIdAt(x, y);
                        if (here < 0) continue;
                        foreach (int other in new[] { layout.RoomIdAt(x + 1, y), layout.RoomIdAt(x, y + 1) })
                        {
                            if (other < 0 || other == here) continue;
                            Assert.That(doorPairs.Contains((Math.Min(here, other), Math.Max(here, other))), Is.True, layout.ToAscii());
                        }
                    }
                }
            }
        }

        [Test]
        public void ManySeeds_StartAndBossFollowRules()
        {
            foreach (var layout in ManyLayouts())
            {
                var start = layout.StartRoom;
                Assert.That(start.Id, Is.EqualTo(0));
                Assert.That(start.Kind, Is.EqualTo(RoomKind.Start));
                Assert.That(start.Width == 1 && start.Height == 1, Is.True);
                Assert.That(start.X == layout.Width / 2 && start.Y == layout.Height / 2, Is.True);
                Assert.That(start.DistanceFromStart, Is.EqualTo(0));

                var boss = layout.BossRoom;
                Assert.That(boss.Kind, Is.EqualTo(RoomKind.Boss));
                Assert.That(boss.DoorCount, Is.EqualTo(1));
                Assert.That(layout.Rooms.Count(room => room.Kind == RoomKind.Boss), Is.EqualTo(1));
                Assert.That(layout.Rooms.Count(room => room.Kind == RoomKind.Start), Is.EqualTo(1));

                Assert.That(layout.DeadEnds, Does.Contain(boss));
                foreach (var deadEnd in layout.DeadEnds)
                {
                    Assert.That(deadEnd.Id, Is.Not.EqualTo(0));
                    Assert.That(deadEnd.DoorCount, Is.EqualTo(1));
                    Assert.That(deadEnd.DistanceFromStart, Is.LessThanOrEqualTo(boss.DistanceFromStart));
                }
                int deadEndCount = layout.Rooms.Count(room => room.Id != 0 && room.DoorCount == 1);
                Assert.That(layout.DeadEnds.Count, Is.EqualTo(deadEndCount));
            }
        }

        [Test]
        public void ManySeeds_UseEveryShape()
        {
            var shapes = new HashSet<(int, int)>();
            foreach (var layout in ManyLayouts())
            {
                foreach (var room in layout.Rooms) shapes.Add((room.Width, room.Height));
            }

            Assert.That(shapes, Is.EquivalentTo(new[] { (1, 1), (2, 1), (1, 2), (2, 2) }));
        }

        [Test]
        public void Settings_RejectBadValues()
        {
            var oneByOne = new[] { new WeightedRoomShape(new RoomShape(1, 1), 1) };

            Assert.Throws<ArgumentOutOfRangeException>(() => new FloorLayoutSettings(2, 9, 8, 12, oneByOne));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FloorLayoutSettings(9, 9, 1, 12, oneByOne));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FloorLayoutSettings(9, 9, 12, 8, oneByOne));
            Assert.Throws<ArgumentException>(() => new FloorLayoutSettings(9, 9, 8, 12, new WeightedRoomShape[0]));
            Assert.Throws<ArgumentException>(() => new FloorLayoutSettings(3, 3, 2, 4, new[] { new WeightedRoomShape(new RoomShape(4, 1), 1) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeightedRoomShape(new RoomShape(1, 1), 0));
        }
    }
}
