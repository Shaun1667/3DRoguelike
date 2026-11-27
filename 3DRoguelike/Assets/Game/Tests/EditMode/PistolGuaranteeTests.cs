using System;
using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    /// <summary>3-4 첫 던전 권총 보장: 보장 방 · 중복 없음 · 다른 방 보상 그대로 · 도구 합계 · 표 값(BALANCE 11번).</summary>
    public class PistolGuaranteeTests
    {
        static RewardDefinition Pistol(int fromDungeon = 0) =>
            new RewardDefinition("weapon_pistol", "권총", "", Tendency.Human, 0, RewardRarity.Common, unlock: RewardUnlock.Pistol, guaranteeFromDungeon: fromDungeon);

        /// <summary>RewardPlannerTests와 같은 일반 6 · 희귀 4에 권총 보장 한 줄.</summary>
        static List<RewardDefinition> Table(bool withPistol = true)
        {
            var list = new List<RewardDefinition>();
            for (int i = 0; i < 6; i++) list.Add(new RewardDefinition($"c{i}", $"common {i}", "", i % 2 == 0 ? Tendency.Beast : Tendency.Human, 1, RewardRarity.Common));
            for (int i = 0; i < 4; i++) list.Add(new RewardDefinition($"r{i}", $"rare {i}", "", i % 2 == 0 ? Tendency.Beast : Tendency.Human, 2, RewardRarity.Rare));
            if (withPistol) list.Add(Pistol());
            return list;
        }

        static (FloorLayout layout, FloorRewards rewards) Plan(uint seed, int dungeon, UpgradeSheet owned, List<RewardDefinition> table, float chance = 0.5f)
        {
            var run = new RunRandom(new RunSeed(seed));
            FloorLayout layout = FloorGeneratorTests.Generate(seed, dungeon);
            SpecialRoomPlanner.AssignTreasure(layout, run.Create(RandomPurpose.Decor, dungeon));
            FloorRewards rewards = RewardPlanner.Plan(layout, new RewardSettings(chance, 3), table, run.Create(RandomPurpose.Reward, dungeon), dungeon, owned);
            return (layout, rewards);
        }

        static string Offers(FloorRewards rewards, int room) => string.Join(",", rewards.OffersFor(room).Select(r => r.Id));

        /// <summary>시작 방에서 그 방을 지나지 않고 보스 방에 갈 수 있는가.</summary>
        static bool BossReachableWithout(FloorLayout layout, int blocked)
        {
            var seen = new HashSet<int> { layout.StartRoom.Id };
            var queue = new Queue<int>(seen);
            while (queue.Count > 0)
            {
                int room = queue.Dequeue();
                foreach (FloorDoor door in layout.Doors)
                {
                    int other = door.RoomA == room ? door.RoomB : door.RoomB == room ? door.RoomA : -1;
                    if (other < 0 || other == blocked || !seen.Add(other)) continue;
                    queue.Enqueue(other);
                }
            }
            return seen.Contains(layout.BossRoom.Id);
        }

        [Test]
        public void FirstDungeon_ManySeeds_PistolInARoomOnTheWayToTheBoss()
        {
            for (uint seed = 0; seed < 300; seed++)
            {
                var (layout, rewards) = Plan(seed, 0, new UpgradeSheet(), Table());
                int room = rewards.GuaranteedRoom;
                Assert.That(room, Is.GreaterThanOrEqualTo(0), $"seed {seed}");
                Assert.That(rewards.Guaranteed.Unlock, Is.EqualTo(RewardUnlock.Pistol));
                Assert.That(Offers(rewards, room), Is.EqualTo("weapon_pistol"), "보장 방은 권총 하나만");
                if (room == layout.StartRoom.Id) continue;
                Assert.That(layout.Rooms[room].Kind, Is.EqualTo(RoomKind.Battle), $"seed {seed}");
                Assert.That(layout.Rooms[room].DistanceFromStart, Is.EqualTo(1), "시작 바로 옆");
                Assert.That(BossReachableWithout(layout, room), Is.False, $"seed {seed}: 보스에 가려면 반드시 지나야 함");
            }
        }

        [Test]
        public void Guarantee_LeavesOtherRoomsAndOtherRandomAlone()
        {
            for (uint seed = 0; seed < 200; seed++)
            {
                var with = Plan(seed, 0, new UpgradeSheet(), Table());
                var without = Plan(seed, 0, new UpgradeSheet(), Table(withPistol: false));
                Assert.That(without.rewards.GuaranteedRoom, Is.EqualTo(-1));
                for (int id = 0; id < with.rewards.RoomCount; id++)
                {
                    if (id == with.rewards.GuaranteedRoom) continue;
                    Assert.That(Offers(with.rewards, id), Is.EqualTo(Offers(without.rewards, id)), $"seed {seed} room {id}");
                }
            }

            // 보상 난수를 더 쓰지 않는다: 같은 난수로 정한 뒤 다음 값이 같다
            FloorLayout a = FloorGeneratorTests.Generate(5u, 0), b = FloorGeneratorTests.Generate(5u, 0);
            var ra = new Pcg32(9, 9); var rb = new Pcg32(9, 9);
            RewardPlanner.Plan(a, new RewardSettings(0.5f, 3), Table(), ra, 0, null);
            RewardPlanner.Plan(b, new RewardSettings(0.5f, 3), Table(withPistol: false), rb);
            Assert.That(ra.NextUInt(), Is.EqualTo(rb.NextUInt()));
        }

        [Test]
        public void AlreadyOwned_NoGuarantee_AndNeverOffered()
        {
            var owned = new UpgradeSheet();
            owned.SetUnlocked(RewardUnlock.Pistol, true);
            for (uint seed = 0; seed < 200; seed++)
            {
                var (layout, rewards) = Plan(seed, 0, owned, Table(), chance: 1f);
                var plain = Plan(seed, 0, owned, Table(withPistol: false), chance: 1f);
                Assert.That(rewards.GuaranteedRoom, Is.EqualTo(-1));
                foreach (FloorRoom room in layout.Rooms)
                {
                    Assert.That(rewards.OffersFor(room.Id).Any(r => r.Unlock != RewardUnlock.None), Is.False);
                    Assert.That(Offers(rewards, room.Id), Is.EqualTo(Offers(plain.rewards, room.Id)), "가졌으면 보장이 없던 때와 같다");
                }
            }
        }

        [Test]
        public void PistolIsNeverARandomChoice()
        {
            for (uint seed = 0; seed < 200; seed++)
            {
                var (layout, rewards) = Plan(seed, 0, new UpgradeSheet(), Table(), chance: 1f);
                foreach (FloorRoom room in layout.Rooms)
                {
                    if (room.Id == rewards.GuaranteedRoom) continue;
                    Assert.That(rewards.OffersFor(room.Id).Any(r => r.Id == "weapon_pistol"), Is.False, $"seed {seed} room {room.Id}");
                }
            }
        }

        [Test]
        public void FromDungeon_StartsThere_AndCarriesOnUntilOwned()
        {
            var table = new List<RewardDefinition>(Table(withPistol: false)) { Pistol(fromDungeon: 1) };
            Assert.That(Plan(3u, 0, new UpgradeSheet(), table).rewards.GuaranteedRoom, Is.EqualTo(-1), "보장 던전 전에는 없음");
            Assert.That(Plan(3u, 1, new UpgradeSheet(), table).rewards.GuaranteedRoom, Is.GreaterThanOrEqualTo(0));
            Assert.That(Plan(3u, 2, new UpgradeSheet(), table).rewards.GuaranteedRoom, Is.GreaterThanOrEqualTo(0), "못 얻었으면 다음 던전에도");
            Assert.That(Plan(3u, 2, null, table).rewards.GuaranteedRoom, Is.GreaterThanOrEqualTo(0), "합계가 없으면 없는 것으로");
        }

        [Test]
        public void BossNextToStart_PistolInTheStartRoom()
        {
            var settings = new FloorLayoutSettings(3, 3, 2, 2, new[] { new WeightedRoomShape(new RoomShape(1, 1), 1) });
            FloorLayout layout = FloorGenerator.Generate(settings, new Pcg32(1, 1));
            Assume.That(layout.BossRoom.DistanceFromStart, Is.EqualTo(1));
            Assert.That(RewardPlanner.GuaranteeRoom(layout), Is.EqualTo(layout.StartRoom.Id));
            FloorRewards rewards = RewardPlanner.Plan(layout, new RewardSettings(1f, 3), Table(), new Pcg32(2, 2), 0, new UpgradeSheet());
            Assert.That(Offers(rewards, layout.StartRoom.Id), Is.EqualTo("weapon_pistol"));
            Assert.That(rewards.OffersFor(layout.BossRoom.Id), Is.Empty);
        }

        [Test]
        public void TakingPistol_UnlocksOnce_WithoutTendency()
        {
            var stats = new RunStats(new RunSeed(1u), 3);
            int changed = 0;
            stats.Upgrades.Changed += () => changed++;
            Assert.That(stats.Upgrades.Has(RewardUnlock.Pistol), Is.False, "판은 맨몸으로 시작");
            Assert.That(stats.Upgrades.Has(RewardUnlock.None), Is.True);

            stats.TakeReward(Pistol());
            Assert.That(stats.Upgrades.Has(RewardUnlock.Pistol), Is.True);
            Assert.That(stats.Human, Is.EqualTo(0), "보장 보상은 성향을 올리지 않음");
            Assert.That(changed, Is.EqualTo(1));
            stats.Upgrades.Add(Pistol());
            Assert.That(changed, Is.EqualTo(1), "이미 가졌으면 알리지 않음");

            stats.Upgrades.SetUnlocked(RewardUnlock.Pistol, false);
            Assert.That(stats.Upgrades.Has(RewardUnlock.Pistol), Is.False);
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void GuaranteeWithoutUnlock_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RewardDefinition("x", "x", "", Tendency.Human, 1, RewardRarity.Common, guaranteeFromDungeon: 0));
            Assert.That(new RewardDefinition("x", "x", "", Tendency.Human, 1, RewardRarity.Common).IsGuaranteed, Is.False);
        }

        /// <summary>BALANCE.md 11번: 권총은 첫 던전(0)부터 보장, 성향 0, 수치 효과 없음, 무작위 선택지에 없음.</summary>
        [Test]
        public void RewardTable_PistolRow_MatchesBalance()
        {
            var table = AssetDatabase.LoadAssetAtPath<RewardTable>("Assets/Game/Data/RewardTable.asset");
            Assert.That(table, Is.Not.Null);
            List<RewardDefinition> rewards = table.ToDefinitions();
            RewardDefinition pistol = rewards.Single(r => r.Unlock == RewardUnlock.Pistol);
            Assert.That(pistol.Id, Is.EqualTo("weapon_pistol"));
            Assert.That(pistol.Name, Is.EqualTo("권총"));
            Assert.That(pistol.GuaranteeFromDungeon, Is.EqualTo(0));
            Assert.That(pistol.Amount, Is.EqualTo(0));
            Assert.That(pistol.Effects, Is.Empty);
            Assert.That(pistol.ComingLater, Is.False);
            Assert.That(rewards.Count(r => r.IsGuaranteed), Is.EqualTo(1));
        }
    }
}
