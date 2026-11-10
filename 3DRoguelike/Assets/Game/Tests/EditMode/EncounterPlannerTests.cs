using System;
using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using NUnit.Framework;

namespace Game.Tests
{
    public class EncounterPlannerTests
    {
        /// <summary>BALANCE.md 8 · 10번과 같은 값.</summary>
        internal static EncounterSettings BalanceSettings()
        {
            return new EncounterSettings(24f, 2, 4, 1, 4f, 3f, 6f);
        }

        static FloorEncounters Plan(FloorLayout layout, uint seed)
        {
            return EncounterPlanner.Plan(layout, BalanceSettings(), new RunRandom(new RunSeed(seed)).Create(RandomPurpose.Enemy, 0));
        }

        static List<(FloorLayout layout, FloorEncounters encounters)> manyPlans;

        /// <summary>시드 300개 × 2층.</summary>
        static List<(FloorLayout layout, FloorEncounters encounters)> ManyPlans()
        {
            if (manyPlans != null) return manyPlans;
            manyPlans = new List<(FloorLayout, FloorEncounters)>();
            for (uint seed = 0; seed < 300; seed++)
            {
                for (int floor = 0; floor < 2; floor++)
                {
                    FloorLayout layout = FloorGeneratorTests.Generate(seed, floor);
                    manyPlans.Add((layout, EncounterPlanner.Plan(layout, BalanceSettings(), new RunRandom(new RunSeed(seed)).Create(RandomPurpose.Enemy, floor))));
                }
            }
            return manyPlans;
        }

        static string Describe(FloorEncounters encounters)
        {
            var rooms = new List<string>();
            for (int id = 0; id < encounters.RoomCount; id++) rooms.Add(string.Join(" ", encounters.SpawnsFor(id)));
            return string.Join(" | ", rooms);
        }

        [Test]
        public void SameSeed_GivesSamePlan()
        {
            FloorLayout layout = FloorGeneratorTests.Generate(0x12345678u, 0);

            Assert.That(Describe(Plan(layout, 0x12345678u)), Is.EqualTo(Describe(Plan(layout, 0x12345678u))));
            Assert.That(Describe(Plan(layout, 0x12345678u)), Is.Not.EqualTo(Describe(Plan(layout, 1u))));
        }

        [Test]
        public void ManySeeds_StartRoomEmpty_OthersInCountRange()
        {
            foreach ((FloorLayout layout, FloorEncounters encounters) in ManyPlans())
            {
                Assert.That(encounters.RoomCount, Is.EqualTo(layout.Rooms.Count));
                foreach (FloorRoom room in layout.Rooms)
                {
                    int count = encounters.SpawnsFor(room.Id).Count;
                    if (room.Kind == RoomKind.Start)
                    {
                        Assert.That(count, Is.EqualTo(0));
                        continue;
                    }
                    int extra = room.Width * room.Height - 1;
                    Assert.That(count, Is.InRange(2 + extra, 4 + extra), $"room {room.Id} {room.Width}x{room.Height}");
                }
            }
        }

        [Test]
        public void ManySeeds_SpawnsInsideTheirRoomAwayFromWalls()
        {
            foreach ((FloorLayout layout, FloorEncounters encounters) in ManyPlans())
            {
                foreach (FloorRoom room in layout.Rooms)
                {
                    (float centerX, float centerZ) = FloorSpace.RoomCenter(layout, 24f, room);
                    float halfX = room.Width * 12f - 4f;
                    float halfZ = room.Height * 12f - 4f;
                    foreach (SpawnPoint point in encounters.SpawnsFor(room.Id))
                    {
                        Assert.That(Math.Abs(point.X - centerX), Is.LessThanOrEqualTo(halfX + 0.001f), point.ToString());
                        Assert.That(Math.Abs(point.Z - centerZ), Is.LessThanOrEqualTo(halfZ + 0.001f), point.ToString());
                    }
                }
            }
        }

        [Test]
        public void ManySeeds_SpawnsMostlyKeepSpacingAndDoorClearance()
        {
            int total = 0;
            int tooClose = 0;
            foreach ((FloorLayout layout, FloorEncounters encounters) in ManyPlans())
            {
                foreach (FloorRoom room in layout.Rooms)
                {
                    IReadOnlyList<SpawnPoint> points = encounters.SpawnsFor(room.Id);
                    var doors = layout.Doors.Where(door => door.RoomA == room.Id || door.RoomB == room.Id)
                        .Select(door => FloorSpace.DoorCenter(layout, 24f, door)).ToList();
                    for (int i = 0; i < points.Count; i++)
                    {
                        total++;
                        bool close = doors.Any(door => Distance(points[i].X, points[i].Z, door.x, door.z) < 6f);
                        for (int j = 0; j < i; j++) close |= Distance(points[i].X, points[i].Z, points[j].X, points[j].Z) < 3f;
                        if (close) tooClose++;
                    }
                }
            }

            // 자리를 못 찾으면 간격 조건만 빼고 둔다. 그런 자리는 아주 드물어야 한다.
            Assert.That(total, Is.GreaterThan(1000));
            Assert.That(tooClose, Is.LessThanOrEqualTo(total / 200), $"{tooClose} / {total}");
        }

        [Test]
        public void PlanDoesNotChangeMapLayout()
        {
            string before = FloorGeneratorTests.Generate(0x12345678u, 0).ToAscii();
            Plan(FloorGeneratorTests.Generate(0x12345678u, 0), 0x12345678u);

            Assert.That(FloorGeneratorTests.Generate(0x12345678u, 0).ToAscii(), Is.EqualTo(before));
        }

        [Test]
        public void Settings_RejectBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EncounterSettings(0f, 2, 4, 1, 4f, 3f, 6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EncounterSettings(24f, 0, 4, 1, 4f, 3f, 6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EncounterSettings(24f, 5, 4, 1, 4f, 3f, 6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EncounterSettings(24f, 2, 4, -1, 4f, 3f, 6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EncounterSettings(24f, 2, 4, 1, 12f, 3f, 6f));
            Assert.Throws<ArgumentNullException>(() => EncounterPlanner.Plan(null, BalanceSettings(), new Pcg32(1, 1)));
        }

        static float Distance(float ax, float az, float bx, float bz)
        {
            return (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
        }
    }
}
