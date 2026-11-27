using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Rules;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// 3-7 재생 · 영구 재화 · 작은 트리: 트리 검사 · 사기 규칙 · 버는 양 · 판에 걸기(아무것도 안 사면 그대로) · 잠긴 보상 후보 ·
    /// 판 밖 저장(왕복 · 깨진 파일 · 모르는 노드) · 판 저장의 트리 노드 칸(형식 2, 형식 1도 읽음) · 표 값(BALANCE 16번).
    /// </summary>
    public class MetaTreeTests
    {
        static readonly RunSeed Seed = new RunSeed(0xC0FFEEu);

        static MetaTree Tree() => new MetaTree(new[]
        {
            new MetaNode("a", "A", "", Tendency.Beast, 20, null, new[] { new StatEffect(UpgradeStat.MeleeDamage, 10f) }),
            new MetaNode("b", "B", "", Tendency.Beast, 40, "a", new[] { new StatEffect(UpgradeStat.MoveSpeed, 5f) }),
            new MetaNode("c", "C", "", Tendency.Beast, 60, "b", opensReward: "locked_rare"),
            new MetaNode("h", "H", "", Tendency.Human, 20, null, new[] { new StatEffect(UpgradeStat.PistolDamage, 10f) }),
        });

        /// <summary>RewardPlannerTests와 같은 일반 6 · 희귀 4 + 권총 보장, 원하면 트리가 여는 잠긴 희귀 한 줄.</summary>
        static List<RewardDefinition> Table(bool withLocked)
        {
            var list = new List<RewardDefinition>();
            for (int i = 0; i < 6; i++) list.Add(new RewardDefinition($"c{i}", $"common {i}", "", i % 2 == 0 ? Tendency.Beast : Tendency.Human, 1, RewardRarity.Common));
            for (int i = 0; i < 4; i++) list.Add(new RewardDefinition($"r{i}", $"rare {i}", "", i % 2 == 0 ? Tendency.Beast : Tendency.Human, 2, RewardRarity.Rare));
            list.Add(new RewardDefinition("weapon_pistol", "권총", "", Tendency.Human, 0, RewardRarity.Common, unlock: RewardUnlock.Pistol, guaranteeFromDungeon: 0));
            if (withLocked)
            {
                list.Insert(3, new RewardDefinition("locked_common", "잠긴 일반", "", Tendency.Beast, 1, RewardRarity.Common, metaLocked: true));
                list.Add(new RewardDefinition("locked_rare", "잠긴 희귀", "", Tendency.Beast, 2, RewardRarity.Rare, metaLocked: true));
            }
            return list;
        }

        static string PlanText(uint seed, int dungeon, List<RewardDefinition> table, UpgradeSheet sheet)
        {
            var run = new RunRandom(new RunSeed(seed));
            FloorLayout layout = FloorGeneratorTests.Generate(seed, dungeon);
            SpecialRoomPlanner.AssignTreasure(layout, run.Create(RandomPurpose.Decor, dungeon));
            FloorRewards rewards = RewardPlanner.Plan(layout, new RewardSettings(0.5f, 3), table, run.Create(RandomPurpose.Reward, dungeon), dungeon, sheet);
            var rooms = new List<string>();
            for (int id = 0; id < rewards.RoomCount; id++) rooms.Add(string.Join(",", rewards.OffersFor(id).Select(r => r.Id)));
            return $"{rewards.GuaranteedRoom}|{string.Join(";", rooms)}";
        }

        [Test]
        public void Tree_RejectsDuplicateUnknownAndLoop()
        {
            Assert.Throws<ArgumentException>(() => new MetaTree(new[] { new MetaNode("x", "x", "", Tendency.Beast, 1, null), new MetaNode("x", "x", "", Tendency.Beast, 1, null) }));
            Assert.Throws<ArgumentException>(() => new MetaTree(new[] { new MetaNode("x", "x", "", Tendency.Beast, 1, "nope") }));
            Assert.Throws<ArgumentException>(() => new MetaTree(new[] { new MetaNode("x", "x", "", Tendency.Beast, 1, "y"), new MetaNode("y", "y", "", Tendency.Beast, 1, "x") }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MetaNode("x", "x", "", Tendency.Beast, -1, null));
            Assert.That(Tree().TotalCost, Is.EqualTo(140));
        }

        [Test]
        public void Progress_BuyNeedsRequirementAndCurrency()
        {
            MetaTree tree = Tree();
            var progress = new MetaProgress();
            tree.TryGet("a", out MetaNode a);
            tree.TryGet("b", out MetaNode b);
            Assert.That(progress.StateOf(a), Is.EqualTo(MetaNodeState.TooExpensive));
            Assert.That(progress.StateOf(b), Is.EqualTo(MetaNodeState.Locked), "앞 노드가 없으면 잠김");

            progress.Bank(50);
            Assert.That(progress.TryBuy(tree, "b", out string locked), Is.False);
            Assert.That(locked, Does.Contain("A"), "먼저 살 노드 이름");
            Assert.That(progress.TryBuy(tree, "a", out _), Is.True);
            Assert.That(progress.Currency, Is.EqualTo(30));
            Assert.That(progress.StateOf(a), Is.EqualTo(MetaNodeState.Owned));
            Assert.That(progress.TryBuy(tree, "a", out _), Is.False, "두 번 못 삼");
            Assert.That(progress.StateOf(b), Is.EqualTo(MetaNodeState.TooExpensive));
            Assert.That(progress.TryBuy(tree, "b", out string poor), Is.False);
            Assert.That(poor, Does.Contain("30/40"));
            Assert.That(progress.TryBuy(tree, "zzz", out _), Is.False);
            Assert.That(progress.Spent, Is.EqualTo(20));
            Assert.That(progress.Runs, Is.EqualTo(1), "판을 끝낸 수 = 재생 횟수");
            Assert.That(progress.TotalEarned, Is.EqualTo(50));
        }

        [Test]
        public void Progress_DebugResetRefundsAndOwnAll()
        {
            MetaTree tree = Tree();
            var progress = new MetaProgress();
            progress.Bank(100);
            progress.TryBuy(tree, "a", out _);
            progress.TryBuy(tree, "b", out _);
            progress.DebugReset();
            Assert.That(progress.Owned, Is.Empty);
            Assert.That(progress.Currency, Is.EqualTo(100), "쓴 재화를 돌려받음");
            progress.DebugOwnAll(tree);
            Assert.That(progress.OwnedNodes(tree).Select(n => n.Id), Is.EqualTo(new[] { "a", "b", "c", "h" }), "표 순서");
            Assert.That(progress.Currency, Is.EqualTo(100), "모두 사기는 재화를 쓰지 않음");
            progress.DebugAdd(-1000);
            Assert.That(progress.Currency, Is.EqualTo(0));
        }

        [Test]
        public void Earnings_FromRunStats()
        {
            var earnings = new MetaEarnings(1, 3, 20, 30);
            var session = new RunSession(Seed, 1);
            for (int i = 0; i < 7; i++) session.Stats.RecordKill();
            session.Stats.RecordRoomCleared();
            session.Stats.RecordRoomCleared();
            Assert.That(earnings.Earned(session.Stats), Is.EqualTo(7 + 6));
            session.EnterDungeon(0, 50);
            Assert.That(session.FinishDungeon(50, 1f), Is.True, "마지막 던전 = 지역 클리어");
            Assert.That(earnings.Earned(session.Stats), Is.EqualTo(7 + 6 + 20 + 30));
        }

        [Test]
        public void NothingBought_SameNumbersAndSameRewards()
        {
            var session = new RunSession(Seed, 2);
            Assert.That(MetaApply(session, new MetaProgress(), Tree()), Is.EqualTo(0));
            foreach (UpgradeStat stat in Enum.GetValues(typeof(UpgradeStat))) Assert.That(session.Stats.Upgrades.Scale(stat), Is.EqualTo(1f), stat.ToString());
            Assert.That(session.MetaNodes, Is.Empty);

            // 잠긴 줄이 표에 있어도, 열지 않은 판은 그 줄이 없던 때와 같은 보상(같은 시드 = 같은 보상)
            foreach (uint seed in new uint[] { 1u, 7u, 0xC0FFEEu, 123456u, 99999u })
                for (int dungeon = 0; dungeon < 3; dungeon++)
                    Assert.That(PlanText(seed, dungeon, Table(true), session.Stats.Upgrades), Is.EqualTo(PlanText(seed, dungeon, Table(false), new UpgradeSheet())), $"시드 {seed} 던전 {dungeon}");
        }

        static int MetaApply(RunSession session, MetaProgress progress, MetaTree tree)
        {
            int applied = 0;
            foreach (MetaNode node in progress.OwnedNodes(tree)) if (session.ApplyMeta(node)) applied++;
            return applied;
        }

        [Test]
        public void ApplyMeta_AddsEffectsOnce_NotTendency()
        {
            MetaTree tree = Tree();
            var progress = new MetaProgress();
            progress.DebugOwnAll(tree);
            var session = new RunSession(Seed, 2);
            Assert.That(MetaApply(session, progress, tree), Is.EqualTo(4));
            Assert.That(MetaApply(session, progress, tree), Is.EqualTo(0), "같은 노드를 두 번 걸지 않음");
            Assert.That(session.Stats.Upgrades.Bonus(UpgradeStat.MeleeDamage), Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(session.Stats.Upgrades.Bonus(UpgradeStat.MoveSpeed), Is.EqualTo(0.05f).Within(1e-5f));
            Assert.That(session.Stats.Upgrades.Bonus(UpgradeStat.PistolDamage), Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(session.Stats.Upgrades.IsRewardOpen("locked_rare"), Is.True);
            Assert.That(session.Stats.Beast, Is.EqualTo(0), "성향은 고른 보상으로만(핵심 설계 규칙 4)");
            Assert.That(session.Stats.Human, Is.EqualTo(0));
            Assert.That(session.Stats.RewardsTaken, Is.Empty);

            // 보상과 겹쳐 쌓인다
            session.Stats.TakeReward(new RewardDefinition("s", "s", "", Tendency.Beast, 1, RewardRarity.Common, new[] { new StatEffect(UpgradeStat.MeleeDamage, 20f) }));
            Assert.That(session.Stats.Upgrades.ScaleInt(UpgradeStat.MeleeDamage, 10), Is.EqualTo(13), "10 × 1.3");
        }

        [Test]
        public void OpenedReward_JoinsThePool()
        {
            var opened = new UpgradeSheet();
            opened.OpenReward("locked_rare");
            bool seen = false;
            for (uint seed = 1; seed <= 40 && !seen; seed++)
                seen = PlanText(seed, 0, Table(true), opened).Contains("locked_rare");
            Assert.That(seen, Is.True, "연 희귀는 보물방 선택지에 나온다");
            for (uint seed = 1; seed <= 40; seed++)
                Assert.That(PlanText(seed, 0, Table(true), new UpgradeSheet()), Does.Not.Contain("locked"), $"시드 {seed}: 열지 않으면 안 나옴");
        }

        [Test]
        public void IsUntouched_OnlyBeforeAnything()
        {
            Assert.That(new RunSession(Seed, 2).IsUntouched, Is.True);
            var killed = new RunSession(Seed, 2);
            killed.Stats.RecordKill();
            Assert.That(killed.IsUntouched, Is.False);
            var road = new RunSession(Seed, 2);
            road.World.ClearEncounter(0);
            Assert.That(road.IsUntouched, Is.False);
            var inside = new RunSession(Seed, 2);
            inside.EnterDungeon(0, 100);
            Assert.That(inside.IsUntouched, Is.False);
            var dead = new RunSession(Seed, 2);
            dead.Stats.Die("곰", 1f);
            Assert.That(dead.IsUntouched, Is.False);
        }

        [Test]
        public void MetaSave_RoundTripAndRejects()
        {
            MetaTree tree = Tree();
            var progress = new MetaProgress();
            progress.Bank(70);
            progress.Bank(15);
            progress.TryBuy(tree, "h", out _);
            progress.TryBuy(tree, "a", out _);
            MetaSaveData first = MetaSaveRules.Capture(progress, tree);
            Assert.That(first.version, Is.EqualTo(MetaSaveRules.FormatVersion));
            Assert.That(first.owned, Is.EqualTo(new[] { "a", "h" }), "표 순서");

            MetaSaveData read = JsonUtility.FromJson<MetaSaveData>(JsonUtility.ToJson(first));
            Assert.That(MetaSaveRules.TryRestore(read, tree, out MetaProgress back, out List<string> dropped, out string error), Is.True, error);
            Assert.That(dropped, Is.Empty);
            Assert.That(JsonUtility.ToJson(MetaSaveRules.Capture(back, tree)), Is.EqualTo(JsonUtility.ToJson(first)));
            Assert.That(back.Currency, Is.EqualTo(45));
            Assert.That(back.Runs, Is.EqualTo(2));
            Assert.That(back.Owns("a") && back.Owns("h"), Is.True);

            read.owned = new[] { "a", "gone" };
            Assert.That(MetaSaveRules.TryRestore(read, tree, out MetaProgress trimmed, out dropped, out _), Is.True, "표에서 지운 노드는 빼고 읽음");
            Assert.That(dropped, Is.EqualTo(new[] { "gone" }));
            Assert.That(trimmed.Owned, Is.EqualTo(new[] { "a" }));

            Assert.That(MetaSaveRules.TryRestore(null, tree, out _, out _, out _), Is.False);
            var bad = JsonUtility.FromJson<MetaSaveData>(JsonUtility.ToJson(first));
            bad.version = 99;
            Assert.That(MetaSaveRules.TryRestore(bad, tree, out _, out _, out _), Is.False, "형식 번호");
            bad = JsonUtility.FromJson<MetaSaveData>(JsonUtility.ToJson(first));
            bad.currency = -1;
            Assert.That(MetaSaveRules.TryRestore(bad, tree, out _, out _, out _), Is.False, "음수");
        }

        [Test]
        public void RunSave_KeepsMetaNodes_AndReadsFormat1()
        {
            MetaTree tree = Tree();
            var table = Table(true);
            var progress = new MetaProgress();
            progress.DebugOwnAll(tree);
            var session = new RunSession(Seed, 2);
            MetaApply(session, progress, tree);
            session.Stats.TakeReward(table[0]);

            RunSaveData data = RunSaveRules.Capture(session, 3f);
            Assert.That(data.version, Is.EqualTo(2));
            Assert.That(data.metaNodes, Is.EqualTo(new[] { "a", "b", "c", "h" }));
            RunSaveData read = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(data));
            Assert.That(RunSaveRules.TryRestore(read, table, 2, false, out RunSession back, out _, out string error, tree), Is.True, error);
            Assert.That(JsonUtility.ToJson(RunSaveRules.Capture(back, 3f)), Is.EqualTo(JsonUtility.ToJson(data)), "왕복 = 같은 글자");
            Assert.That(back.Stats.Upgrades.Bonus(UpgradeStat.MoveSpeed), Is.EqualTo(0.05f).Within(1e-5f), "판에 건 노드 그대로");
            Assert.That(back.Stats.Upgrades.IsRewardOpen("locked_rare"), Is.True);

            Assert.That(RunSaveRules.TryRestore(JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(data)), table, 2, false, out _, out _, out string noTree), Is.False, "트리 없이 노드가 있는 저장");
            Assert.That(noTree, Does.Contain("a"));

            // 형식 1(3-6) 파일: 트리 노드 칸이 없다 → 빈 목록으로 읽는다
            var old = new RunSession(Seed, 2);
            string v1 = JsonUtility.ToJson(RunSaveRules.Capture(old, 1f)).Replace("\"version\":2", "\"version\":1").Replace(",\"metaNodes\":[]", "");
            Assert.That(v1, Does.Contain("\"version\":1").And.Not.Contain("metaNodes"));
            Assert.That(RunSaveRules.TryRestore(JsonUtility.FromJson<RunSaveData>(v1), table, 2, false, out RunSession fromV1, out _, out error), Is.True, error);
            Assert.That(fromV1.MetaNodes, Is.Empty);
        }

        [Test]
        public void MetaStore_WriteReadBackUp_InTempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gorani_meta_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new MetaSaveStore(folder);
                Assert.That(store.TryRead(out _, out _), Is.EqualTo(RunSaveStore.ReadResult.None));
                var progress = new MetaProgress();
                progress.Bank(33);
                MetaSaveData data = MetaSaveRules.Capture(progress, Tree());
                store.Write(data);
                store.Write(data);
                Assert.That(store.TryRead(out MetaSaveData read, out string error), Is.EqualTo(RunSaveStore.ReadResult.Ok), error);
                Assert.That(read.currency, Is.EqualTo(33));
                Assert.That(File.ReadAllText(store.FilePath), Does.Contain("\"version\": " + MetaSaveRules.FormatVersion));
                Assert.That(Path.GetFileName(store.FilePath), Is.EqualTo("meta_progress.json"));
                Assert.That(store.FilePath, Is.Not.EqualTo(new RunSaveStore(folder).FilePath), "판 저장과 다른 파일");

                File.WriteAllText(store.FilePath, "{ broken");
                Assert.That(store.TryRead(out _, out _), Is.EqualTo(RunSaveStore.ReadResult.Broken));
                string backup = store.BackUpBroken(new DateTime(2026, 11, 27, 1, 2, 3, DateTimeKind.Utc));
                Assert.That(Path.GetFileName(backup), Is.EqualTo("meta_progress.broken-20261127-010203.json"));
                Assert.That(File.Exists(backup), Is.True);
                Assert.That(store.Exists, Is.False);
            }
            finally
            {
                if (Directory.Exists(folder) && folder.Contains("gorani_meta_test_")) Directory.Delete(folder, recursive: true);
            }
        }

        /// <summary>BALANCE.md 16번과 Data/MetaTree.asset · RewardTable의 잠긴 줄이 같은지.</summary>
        [Test]
        public void MetaTreeAsset_MatchesBalance()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MetaTreeTable>("Assets/Game/Data/MetaTree.asset");
            Assert.That(asset, Is.Not.Null);
            MetaTree tree = asset.ToTree();
            Assert.That(tree.Count, Is.EqualTo(8));
            Assert.That(tree.TotalCost, Is.EqualTo(320));
            MetaEarnings earn = asset.ToEarnings();
            Assert.That((earn.PerKill, earn.PerRoom, earn.PerDungeon, earn.PerRegionClear), Is.EqualTo((1, 3, 20, 30)));

            void Expect(string id, Tendency branch, int cost, string requires, string opens, params (UpgradeStat stat, float percent)[] effects)
            {
                Assert.That(tree.TryGet(id, out MetaNode node), Is.True, id);
                Assert.That(node.Branch, Is.EqualTo(branch), id);
                Assert.That(node.Cost, Is.EqualTo(cost), id);
                Assert.That(node.Requires, Is.EqualTo(requires), id);
                Assert.That(node.OpensReward, Is.EqualTo(opens), id);
                Assert.That(node.Effects.Select(e => (e.stat, e.percent)), Is.EqualTo(effects), id);
            }
            Expect("meta_beast_claw", Tendency.Beast, 20, null, null, (UpgradeStat.MeleeDamage, 10f));
            Expect("meta_beast_legs", Tendency.Beast, 40, "meta_beast_claw", null, (UpgradeStat.MoveSpeed, 5f));
            Expect("meta_beast_regen", Tendency.Beast, 40, "meta_beast_claw", null, (UpgradeStat.HealOnRoomClear, 10f));
            Expect("meta_beast_frenzy", Tendency.Beast, 60, "meta_beast_legs", "beast_frenzy");
            Expect("meta_human_grip", Tendency.Human, 20, null, null, (UpgradeStat.PistolDamage, 10f));
            Expect("meta_human_mag", Tendency.Human, 40, "meta_human_grip", null, (UpgradeStat.PistolMagazine, 10f));
            Expect("meta_human_aim", Tendency.Human, 40, "meta_human_grip", null, (UpgradeStat.AimTransition, 10f), (UpgradeStat.AimMoveSpeed, 10f));
            Expect("meta_human_hollow", Tendency.Human, 60, "meta_human_mag", "human_hollowpoint");

            // 트리가 여는 보상은 보상 표에 잠긴 줄로 있다
            var rewards = AssetDatabase.LoadAssetAtPath<RewardTable>("Assets/Game/Data/RewardTable.asset").ToDefinitions().ToDictionary(r => r.Id);
            foreach (MetaNode node in tree.Nodes.Where(n => n.OpensReward != null))
            {
                Assert.That(rewards.ContainsKey(node.OpensReward), Is.True, node.OpensReward);
                Assert.That(rewards[node.OpensReward].MetaLocked, Is.True, node.OpensReward);
            }
            Assert.That(rewards.Values.Count(r => r.MetaLocked), Is.EqualTo(2));
            Assert.That(rewards["beast_frenzy"].Rarity == RewardRarity.Rare && rewards["human_hollowpoint"].Rarity == RewardRarity.Rare, Is.True);
        }

        [Test]
        public void WorldTuning_LinksMetaTree()
        {
            var world = AssetDatabase.LoadAssetAtPath<WorldTuning>("Assets/Game/Data/WorldTuning.asset");
            Assert.That(world.metaTree, Is.SameAs(AssetDatabase.LoadAssetAtPath<MetaTreeTable>("Assets/Game/Data/MetaTree.asset")));
        }
    }
}
