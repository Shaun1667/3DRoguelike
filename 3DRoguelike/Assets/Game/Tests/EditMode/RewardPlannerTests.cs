using System;
using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using NUnit.Framework;

namespace Game.Tests
{
    public class RewardPlannerTests
    {
        static List<RewardDefinition> Table()
        {
            var list = new List<RewardDefinition>();
            for (int i = 0; i < 6; i++) list.Add(new RewardDefinition($"c{i}", $"common {i}", "", i % 2 == 0 ? Tendency.Beast : Tendency.Human, 1, RewardRarity.Common));
            for (int i = 0; i < 4; i++) list.Add(new RewardDefinition($"r{i}", $"rare {i}", "", i % 2 == 0 ? Tendency.Beast : Tendency.Human, 2, RewardRarity.Rare));
            return list;
        }

        /// <summary>FloorRun과 같은 순서: 배치 → 보물방(꾸미기 난수) → 보상(보상 난수).</summary>
        static (FloorLayout layout, int treasure, FloorRewards rewards) Plan(uint seed, float chance = 0.5f)
        {
            var run = new RunRandom(new RunSeed(seed));
            FloorLayout layout = FloorGeneratorTests.Generate(seed, 0);
            int treasure = SpecialRoomPlanner.AssignTreasure(layout, run.Create(RandomPurpose.Decor, 0));
            FloorRewards rewards = RewardPlanner.Plan(layout, new RewardSettings(chance, 3), Table(), run.Create(RandomPurpose.Reward, 0));
            return (layout, treasure, rewards);
        }

        static string Describe(FloorRewards rewards)
        {
            return string.Join("|", Enumerable.Range(0, rewards.RoomCount).Select(id => string.Join(",", rewards.OffersFor(id).Select(r => r.Id))));
        }

        [Test]
        public void SameSeed_GivesSameTreasureAndRewards()
        {
            var a = Plan(0x12345678u); var b = Plan(0x12345678u);

            Assert.That(a.treasure, Is.EqualTo(b.treasure));
            Assert.That(Describe(a.rewards), Is.EqualTo(Describe(b.rewards)));
        }

        [Test]
        public void ManySeeds_TreasureIsANonBossDeadEnd()
        {
            for (uint seed = 0; seed < 300; seed++)
            {
                var (layout, treasure, _) = Plan(seed);
                bool hasCandidate = layout.DeadEnds.Any(r => r.Id != layout.BossRoom.Id);
                if (!hasCandidate) { Assert.That(treasure, Is.EqualTo(-1)); continue; }
                Assert.That(treasure, Is.Not.EqualTo(-1));
                FloorRoom room = layout.Rooms[treasure];
                Assert.That(room.Kind, Is.EqualTo(RoomKind.Treasure));
                Assert.That(room.DoorCount, Is.EqualTo(1));
                Assert.That(layout.Rooms.Count(r => r.Kind == RoomKind.Treasure), Is.EqualTo(1));
                Assert.That(layout.BossRoom.Kind, Is.EqualTo(RoomKind.Boss));
            }
        }

        [Test]
        public void ManySeeds_OffersFollowRoomKind()
        {
            int battle = 0, withReward = 0;
            for (uint seed = 0; seed < 300; seed++)
            {
                var (layout, _, rewards) = Plan(seed);
                foreach (FloorRoom room in layout.Rooms)
                {
                    IReadOnlyList<RewardDefinition> offers = rewards.OffersFor(room.Id);
                    Assert.That(offers.Select(r => r.Id).Distinct().Count(), Is.EqualTo(offers.Count), "선택지는 겹치지 않는다");
                    switch (room.Kind)
                    {
                        case RoomKind.Start:
                        case RoomKind.Boss:
                            Assert.That(offers, Is.Empty);
                            break;
                        case RoomKind.Treasure:
                            Assert.That(offers.Count, Is.EqualTo(3));
                            Assert.That(offers.All(r => r.Rarity == RewardRarity.Rare), Is.True);
                            break;
                        case RoomKind.Battle:
                            battle++;
                            if (offers.Count > 0) withReward++;
                            Assert.That(offers.Count, Is.EqualTo(0).Or.EqualTo(3));
                            Assert.That(offers.All(r => r.Rarity == RewardRarity.Common), Is.True);
                            break;
                    }
                }
            }
            double rate = (double)withReward / battle;
            Assert.That(rate, Is.InRange(0.45, 0.55), $"{withReward}/{battle}");
        }

        [Test]
        public void ChanceZeroAndOne_AreRespected()
        {
            var none = Plan(7u, 0f); var all = Plan(7u, 1f);
            foreach (FloorRoom room in none.layout.Rooms.Where(r => r.Kind == RoomKind.Battle)) Assert.That(none.rewards.OffersFor(room.Id), Is.Empty);
            foreach (FloorRoom room in all.layout.Rooms.Where(r => r.Kind == RoomKind.Battle)) Assert.That(all.rewards.OffersFor(room.Id).Count, Is.EqualTo(3));
        }

        [Test]
        public void SmallTable_OffersWhatItHas()
        {
            var layout = FloorGeneratorTests.Generate(1u, 0);
            var table = new List<RewardDefinition> { new RewardDefinition("only", "only", "", Tendency.Beast, 1, RewardRarity.Common) };
            FloorRewards rewards = RewardPlanner.Plan(layout, new RewardSettings(1f, 3), table, new Pcg32(1, 1));
            foreach (FloorRoom room in layout.Rooms.Where(r => r.Kind == RoomKind.Battle)) Assert.That(rewards.OffersFor(room.Id).Count, Is.EqualTo(1));
        }

        [Test]
        public void TreasureRoom_IsClearedAndHasNoEnemies()
        {
            var (layout, treasure, _) = Plan(0x12345678u);
            Assume.That(treasure, Is.Not.EqualTo(-1));
            var progress = new FloorProgress(layout);
            FloorEncounters encounters = EncounterPlanner.Plan(layout, EncounterPlannerTests.BalanceSettings(), new Pcg32(3, 3));

            Assert.That(progress.IsCleared(treasure), Is.True);
            Assert.That(encounters.SpawnsFor(treasure), Is.Empty);
        }

        [Test]
        public void TakeReward_RaisesTendencyOnlyWhilePlaying()
        {
            var stats = new RunStats(new RunSeed(1u), 5);
            stats.TakeReward(new RewardDefinition("a", "a", "", Tendency.Beast, 1, RewardRarity.Common));
            stats.TakeReward(new RewardDefinition("b", "b", "", Tendency.Human, 2, RewardRarity.Rare));

            Assert.That(stats.Beast, Is.EqualTo(1));
            Assert.That(stats.Human, Is.EqualTo(2));
            Assert.That(stats.RewardsTaken.Count, Is.EqualTo(2));

            stats.Die("x", 1f);
            Assert.That(stats.TakeReward(new RewardDefinition("c", "c", "", Tendency.Beast, 1, RewardRarity.Common)), Is.False);
            Assert.That(stats.Beast, Is.EqualTo(1));
        }

        [Test]
        public void BadValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardSettings(1.5f, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardSettings(0.5f, 0));
            Assert.Throws<ArgumentException>(() => new RewardDefinition("", "x", "", Tendency.Beast, 1, RewardRarity.Common));
        }
    }
}
