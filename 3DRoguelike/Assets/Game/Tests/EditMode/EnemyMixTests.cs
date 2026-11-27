using System;
using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>적 구성(종 비중 · E 개체 비율 · 멧토끼 떼)과 방 배치. 표는 BALANCE.md 14번.</summary>
    public class EnemyMixTests
    {
        /// <summary>BALANCE.md 14번 = `Data/EnemyRoster.asset` 기본값.</summary>
        internal static EnemyMix BalanceMix()
        {
            return new EnemyMix(
                new[]
                {
                    new EnemySpeciesRule(EnemySpecies.Badger, 1, 1, 1, 0f),
                    new EnemySpeciesRule(EnemySpecies.Boar, 2, 1, 1, 0f),
                    new EnemySpeciesRule(EnemySpecies.Hare, 2, 3, 5, 1.5f),
                    new EnemySpeciesRule(EnemySpecies.Toad, 1, 1, 1, 0f),
                    new EnemySpeciesRule(EnemySpecies.RaccoonDog, 2, 3, 5, 1.5f),
                    new EnemySpeciesRule(EnemySpecies.Fox, 1, 1, 1, 0f),
                },
                new[]
                {
                    // 던전 1: 방은 4종(결정 2026-11-25), 앞 방(시작 방에서 1) · 길 위는 오소리 · 멧토끼만
                    new EnemyStage(new[] { (EnemySpecies.Badger, 3), (EnemySpecies.Hare, 1), (EnemySpecies.Boar, 1), (EnemySpecies.Toad, 1) }, 0f,
                        new[] { (EnemySpecies.Badger, 3), (EnemySpecies.Hare, 1) }, 1,
                        new[] { (EnemySpecies.Badger, 3), (EnemySpecies.Hare, 1) }),
                    // 도시 던전(2026-11-27): 방은 너구리 · 여우 위주 + 멧돼지 · 두꺼비, 길 위는 너구리 · 여우만
                    new EnemyStage(new[] { (EnemySpecies.RaccoonDog, 2), (EnemySpecies.Fox, 3), (EnemySpecies.Boar, 1), (EnemySpecies.Toad, 1) }, 0.25f,
                        null, 0,
                        new[] { (EnemySpecies.RaccoonDog, 1), (EnemySpecies.Fox, 1) }),
                    new EnemyStage(new[] { (EnemySpecies.Badger, 2), (EnemySpecies.Hare, 1), (EnemySpecies.Boar, 2), (EnemySpecies.Toad, 2) }, 0.5f),
                });
        }

        static IRandom Random(uint seed, int floor) => new RunRandom(new RunSeed(seed)).Create(RandomPurpose.Enemy, floor);

        /// <summary>방 4000개(자리 4) 분량을 뽑는다.</summary>
        static List<EnemyPick> ManyPicks(int stage, bool allowEvolved)
        {
            EnemyMix mix = BalanceMix();
            var all = new List<EnemyPick>();
            for (uint seed = 0; seed < 4000; seed++) all.AddRange(EnemyMixPlanner.Pick(mix, stage, 4, allowEvolved, Random(seed, stage)));
            return all;
        }

        static List<EnemyPick> ManyPicks(int stage, EnemyPlace place)
        {
            EnemyMix mix = BalanceMix();
            var all = new List<EnemyPick>();
            for (uint seed = 0; seed < 4000; seed++) all.AddRange(EnemyMixPlanner.Pick(mix, stage, place, 4, Random(seed, stage)));
            return all;
        }

        /// <summary>결정 2026-11-25(사용자) "멧돼지 두꺼비를 던전 1에 섞어줘" — 던전 1 방에는 4종, 오소리가 가장 많다.</summary>
        [Test]
        public void Stage0_Rooms_AllFourSpecies_BadgerMost()
        {
            List<EnemyPick> picks = ManyPicks(0, EnemyPlace.Room);

            Assert.That(picks.Select(p => p.Kind.Species).Distinct(),
                Is.EquivalentTo(new[] { EnemySpecies.Badger, EnemySpecies.Boar, EnemySpecies.Hare, EnemySpecies.Toad }));
            int badgers = picks.Count(p => p.Kind.Species == EnemySpecies.Badger);
            foreach (EnemySpecies other in new[] { EnemySpecies.Boar, EnemySpecies.Hare, EnemySpecies.Toad })
                Assert.That(badgers, Is.GreaterThan(picks.Count(p => p.Kind.Species == other) * 2), $"오소리 위주 · {other}");
            Assert.That(picks.Any(p => p.Kind.Grade == EnemyGrade.Evolved), Is.False, "던전 1은 E 개체 0%");
        }

        /// <summary>던전 1 앞 방 · 길 위 무리는 예전처럼 오소리 · 멧토끼만(첫 방이 너무 세지 않게, 길 위는 그대로).</summary>
        [TestCase(EnemyPlace.NearRoom)]
        [TestCase(EnemyPlace.Road)]
        public void Stage0_NearRoomsAndRoad_OnlyBadgerAndHare(EnemyPlace place)
        {
            List<EnemyPick> picks = ManyPicks(0, place);

            Assert.That(picks.Select(p => p.Kind.Species).Distinct(), Is.EquivalentTo(new[] { EnemySpecies.Badger, EnemySpecies.Hare }));
            Assert.That(picks.Count(p => p.Kind.Species == EnemySpecies.Badger), Is.GreaterThan(picks.Count / 2), "오소리 위주");
        }

        /// <summary>앞 방 · 길 위 비중을 따로 주지 않은 단계는 방 비중을 그대로 쓴다(같은 난수면 같은 결과).</summary>
        [Test]
        public void StageWithoutNearOrRoad_UsesRoomWeights()
        {
            EnemyMix mix = BalanceMix();
            EnemyStage stage = mix.Stage(2);

            Assert.That(stage.NearWeights, Is.SameAs(stage.Weights));
            Assert.That(stage.RoadWeights, Is.SameAs(stage.Weights));
            Assert.That(stage.RoomPlace(1), Is.EqualTo(EnemyPlace.Room));
            Assert.That(mix.Stage(0).RoomPlace(1), Is.EqualTo(EnemyPlace.NearRoom));
            Assert.That(mix.Stage(0).RoomPlace(2), Is.EqualTo(EnemyPlace.Room));
            for (uint seed = 0; seed < 50; seed++)
            {
                string room = string.Join(" ", EnemyMixPlanner.Pick(mix, 2, 5, false, Random(seed, 2)));
                string road = string.Join(" ", EnemyMixPlanner.Pick(mix, 2, EnemyPlace.Road, 5, Random(seed, 2)));
                Assert.That(road, Is.EqualTo(room), $"seed {seed}");
            }
        }

        /// <summary>던전 1 층 계획: 시작 방 바로 옆 방에는 멧돼지 · 두꺼비가 없고, 그 뒤 방에는 나온다.</summary>
        [Test]
        public void Plan_Stage0_NearRoomsLight_FartherRoomsMixed()
        {
            EnemyMix mix = BalanceMix();
            int heavyFar = 0;
            for (uint seed = 0; seed < 200; seed++)
            {
                FloorLayout layout = FloorGeneratorTests.Generate(seed, 0);
                FloorEncounters encounters = EncounterPlanner.Plan(layout, EncounterPlannerTests.BalanceSettings(), mix, 0, Random(seed, 0));
                foreach (FloorRoom room in layout.Rooms)
                {
                    bool heavy = encounters.SpawnsFor(room.Id).Any(p => p.Kind.Species == EnemySpecies.Boar || p.Kind.Species == EnemySpecies.Toad);
                    if (room.DistanceFromStart <= 1) Assert.That(heavy, Is.False, $"seed {seed} room {room.Id}");
                    else if (heavy) heavyFar++;
                }
            }
            Assert.That(heavyFar, Is.GreaterThan(200), "먼 방에는 멧돼지 · 두꺼비가 섞인다");
        }

        /// <summary>도시 던전(단계 2) 방: 너구리 · 여우 위주, 멧돼지 · 두꺼비는 조금, 오소리 · 멧토끼는 없음(2026-11-27).</summary>
        [Test]
        public void CityStage_Rooms_CitySpeciesMost_SomeBoarAndToad()
        {
            List<EnemyPick> picks = ManyPicks(1, EnemyPlace.Room);

            Assert.That(picks.Select(p => p.Kind.Species).Distinct(),
                Is.EquivalentTo(new[] { EnemySpecies.RaccoonDog, EnemySpecies.Fox, EnemySpecies.Boar, EnemySpecies.Toad }));
            int city = picks.Count(p => p.Kind.Species == EnemySpecies.RaccoonDog || p.Kind.Species == EnemySpecies.Fox);
            Assert.That(city, Is.GreaterThan((picks.Count - city) * 2), "도시 종 위주(뽑힌 횟수, 무게 5 : 2)");
            Assert.That(picks.Any(p => p.Kind.Grade == EnemyGrade.Evolved), Is.True, "도시 던전은 E 개체도 나온다");
        }

        /// <summary>도시 길 위 무리: 너구리 · 여우 Mutant만.</summary>
        [Test]
        public void CityStage_Road_OnlyRaccoonDogAndFox_Mutant()
        {
            List<EnemyPick> picks = ManyPicks(1, EnemyPlace.Road);

            Assert.That(picks.Select(p => p.Kind.Species).Distinct(), Is.EquivalentTo(new[] { EnemySpecies.RaccoonDog, EnemySpecies.Fox }));
            Assert.That(picks.All(p => p.Kind.Grade == EnemyGrade.Mutant), Is.True);
        }

        /// <summary>너구리는 3~5마리 떼, 여우는 한 마리.</summary>
        [Test]
        public void RaccoonDogComesInPacksOfThreeToFive_FoxAlone()
        {
            List<EnemyPick> picks = ManyPicks(1, allowEvolved: true);
            var packs = picks.Where(p => p.Kind.Species == EnemySpecies.RaccoonDog).Select(p => p.GroupSize).ToList();

            Assert.That(packs.Distinct(), Is.EquivalentTo(new[] { 3, 4, 5 }));
            Assert.That(picks.Where(p => p.Kind.Species == EnemySpecies.Fox).Select(p => p.GroupSize), Is.All.EqualTo(1));
        }

        /// <summary>숲(단계 1)에는 도시 종이 어디에도 나오지 않는다.</summary>
        [TestCase(EnemyPlace.Room)]
        [TestCase(EnemyPlace.NearRoom)]
        [TestCase(EnemyPlace.Road)]
        public void ForestStage_HasNoCitySpecies(EnemyPlace place)
        {
            Assert.That(ManyPicks(0, place).Any(p => p.Kind.Species == EnemySpecies.RaccoonDog || p.Kind.Species == EnemySpecies.Fox), Is.False);
        }

        [TestCase(2)]
        public void LaterStages_AllFourSpeciesAppear(int stage)
        {
            List<EnemyPick> picks = ManyPicks(stage, allowEvolved: true);

            Assert.That(picks.Select(p => p.Kind.Species).Distinct(),
                Is.EquivalentTo(new[] { EnemySpecies.Badger, EnemySpecies.Boar, EnemySpecies.Hare, EnemySpecies.Toad }));
        }

        [TestCase(0, 0f)]
        [TestCase(1, 0.25f)]
        [TestCase(2, 0.5f)]
        public void EvolvedShare_MatchesStage(int stage, float expected)
        {
            List<EnemyPick> picks = ManyPicks(stage, allowEvolved: true);
            float share = picks.Count(p => p.Kind.Grade == EnemyGrade.Evolved) / (float)picks.Count;

            Assert.That(share, Is.EqualTo(expected).Within(0.03f), $"{share:0.000} of {picks.Count}");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void RoadGroups_AreMutantOnly(int stage)
        {
            Assert.That(ManyPicks(stage, allowEvolved: false).Any(p => p.Kind.Grade == EnemyGrade.Evolved), Is.False);
        }

        [Test]
        public void HareComesInSwarmsOfThreeToFive_OthersAlone()
        {
            List<EnemyPick> picks = ManyPicks(2, allowEvolved: true);
            var hareSizes = picks.Where(p => p.Kind.Species == EnemySpecies.Hare).Select(p => p.GroupSize).ToList();

            Assert.That(hareSizes, Is.Not.Empty);
            Assert.That(hareSizes, Is.All.InRange(3, 5));
            Assert.That(hareSizes.Distinct(), Is.EquivalentTo(new[] { 3, 4, 5 }));
            Assert.That(picks.Where(p => p.Kind.Species != EnemySpecies.Hare).Select(p => p.GroupSize), Is.All.EqualTo(1));
        }

        [Test]
        public void SlotsAreUsedUp_BecauseBadgerAlwaysFits()
        {
            EnemyMix mix = BalanceMix();
            for (int stage = 0; stage < 3; stage++)
            {
                for (int slots = 0; slots <= 8; slots++)
                {
                    for (uint seed = 0; seed < 200; seed++)
                    {
                        List<EnemyPick> picks = EnemyMixPlanner.Pick(mix, stage, slots, true, Random(seed, stage));
                        Assert.That(picks.Sum(p => mix.Rule(p.Kind.Species).Slots), Is.EqualTo(slots), $"stage {stage} slots {slots} seed {seed}");
                    }
                }
            }
        }

        [Test]
        public void SameRandom_GivesSamePicks()
        {
            string Describe(uint seed) => string.Join(" ", EnemyMixPlanner.Pick(BalanceMix(), 2, 7, true, Random(seed, 2)));

            Assert.That(Describe(42u), Is.EqualTo(Describe(42u)));
            Assert.That(Enumerable.Range(0, 20).Select(i => Describe((uint)i)).Distinct().Count(), Is.GreaterThan(1));
        }

        [Test]
        public void SingleSpecies_DrawsNoRandom()
        {
            IRandom used = Random(7u, 0);
            List<EnemyPick> picks = EnemyMixPlanner.Pick(EnemyMix.Single(EnemySpecies.Badger), 0, 5, true, used);

            Assert.That(picks.Count, Is.EqualTo(5));
            Assert.That(picks, Is.All.Matches<EnemyPick>(p => p.Kind.Equals(new EnemyKind(EnemySpecies.Badger, EnemyGrade.Mutant)) && p.GroupSize == 1));
            // 수열을 하나도 쓰지 않았다 → 종 구성 이전의 자리 계획과 같은 수열
            Assert.That(used.NextUInt(), Is.EqualTo(Random(7u, 0).NextUInt()));
        }

        [Test]
        public void StageBeyondTable_UsesLastStage()
        {
            EnemyMix mix = BalanceMix();

            Assert.That(mix.Stage(5), Is.SameAs(mix.Stage(2)));
            Assert.That(mix.Stage(-1), Is.SameAs(mix.Stage(0)));
        }

        [Test]
        public void GroupOffsets_RingAroundCenter()
        {
            Assert.That(EnemyMixPlanner.GroupOffsets(1, 1.5f, 0f), Is.EqualTo(new[] { (0f, 0f) }));
            foreach ((float x, float z) in EnemyMixPlanner.GroupOffsets(4, 1.5f, 0.3f))
            {
                Assert.That(Math.Sqrt(x * x + z * z), Is.EqualTo(1.5).Within(0.0001));
            }
        }

        [Test]
        public void Plan_WithMix_GroupsShareKind_SlotsInRange_InsideRoom()
        {
            EnemyMix mix = BalanceMix();
            int evolvedGroups = 0, groups = 0;
            for (uint seed = 0; seed < 200; seed++)
            {
                FloorLayout layout = FloorGeneratorTests.Generate(seed, 2);
                FloorEncounters encounters = EncounterPlanner.Plan(layout, EncounterPlannerTests.BalanceSettings(), mix, 2, Random(seed, 2));
                foreach (FloorRoom room in layout.Rooms)
                {
                    IReadOnlyList<SpawnPoint> points = encounters.SpawnsFor(room.Id);
                    if (room.Kind == RoomKind.Start || room.Kind == RoomKind.Treasure)
                    {
                        Assert.That(points.Count, Is.EqualTo(0));
                        continue;
                    }
                    var byGroup = points.GroupBy(p => p.Group).ToList();
                    int slots = 0;
                    foreach (IGrouping<int, SpawnPoint> group in byGroup)
                    {
                        Assert.That(group.Select(p => p.Kind).Distinct().Count(), Is.EqualTo(1), "한 떼는 같은 종 · 모습");
                        EnemyKind kind = group.First().Kind;
                        EnemySpeciesRule rule = mix.Rule(kind.Species);
                        Assert.That(group.Count(), Is.InRange(rule.GroupMin, rule.GroupMax));
                        slots += rule.Slots;
                        groups++;
                        if (kind.Grade == EnemyGrade.Evolved) evolvedGroups++;
                    }
                    int extra = room.Width * room.Height - 1;
                    Assert.That(slots, Is.InRange(2 + extra, 4 + extra), $"room {room.Id}");

                    (float centerX, float centerZ) = FloorSpace.RoomCenter(layout, 24f, room);
                    foreach (SpawnPoint point in points)
                    {
                        Assert.That(Math.Abs(point.X - centerX), Is.LessThanOrEqualTo(room.Width * 12f - 4f + 0.001f));
                        Assert.That(Math.Abs(point.Z - centerZ), Is.LessThanOrEqualTo(room.Height * 12f - 4f + 0.001f));
                    }
                }
            }
            Assert.That(evolvedGroups / (float)groups, Is.EqualTo(0.5f).Within(0.05f), "던전 3은 E 개체 약 50%");
        }

        [Test]
        public void Settings_RejectBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemySpeciesRule(EnemySpecies.Hare, 0, 3, 5, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemySpeciesRule(EnemySpecies.Hare, 2, 5, 3, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyStage(new[] { (EnemySpecies.Badger, 1) }, 1.5f));
            Assert.Throws<ArgumentException>(() => new EnemyStage(new[] { (EnemySpecies.Badger, 0) }, 0f));
            Assert.Throws<ArgumentException>(() => new EnemyMix(
                new[] { new EnemySpeciesRule(EnemySpecies.Badger, 1, 1, 1, 0f) },
                new[] { new EnemyStage(new[] { (EnemySpecies.Toad, 1) }, 0f) }));
            Assert.Throws<ArgumentException>(() => new EnemyMix(new[] { new EnemySpeciesRule(EnemySpecies.Badger, 1, 1, 1, 0f) }, new EnemyStage[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyStage(new[] { (EnemySpecies.Badger, 1) }, 0f, null, -1, null));
            Assert.Throws<ArgumentException>(() => new EnemyStage(new[] { (EnemySpecies.Badger, 1) }, 0f, new[] { (EnemySpecies.Badger, 0) }, 1, null));
            Assert.Throws<ArgumentException>(() => new EnemyMix(
                new[] { new EnemySpeciesRule(EnemySpecies.Badger, 1, 1, 1, 0f) },
                new[] { new EnemyStage(new[] { (EnemySpecies.Badger, 1) }, 0f, null, 0, new[] { (EnemySpecies.Toad, 1) }) }), "길 위 비중의 종도 규칙이 있어야");
        }
    }
}
