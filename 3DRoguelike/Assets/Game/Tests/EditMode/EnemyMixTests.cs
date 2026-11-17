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
                },
                new[]
                {
                    new EnemyStage(new[] { (EnemySpecies.Badger, 3), (EnemySpecies.Hare, 1) }, 0f),
                    new EnemyStage(new[] { (EnemySpecies.Badger, 3), (EnemySpecies.Hare, 1), (EnemySpecies.Boar, 1), (EnemySpecies.Toad, 1) }, 0.25f),
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

        [Test]
        public void Stage0_OnlyBadgerAndHare()
        {
            List<EnemyPick> picks = ManyPicks(0, allowEvolved: true);

            Assert.That(picks.Select(p => p.Kind.Species).Distinct(), Is.EquivalentTo(new[] { EnemySpecies.Badger, EnemySpecies.Hare }));
            Assert.That(picks.Count(p => p.Kind.Species == EnemySpecies.Badger), Is.GreaterThan(picks.Count / 2), "오소리 위주");
        }

        [TestCase(1)]
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
        }
    }
}
