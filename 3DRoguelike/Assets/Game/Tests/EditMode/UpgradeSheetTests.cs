using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    /// <summary>3-2 강화 표: 합계 계산(규칙)과 RewardTable 에셋 값(BALANCE 12번).</summary>
    public class UpgradeSheetTests
    {
        static RewardDefinition Reward(params StatEffect[] effects) => new RewardDefinition("x", "x", "", Tendency.Beast, 1, RewardRarity.Common, effects);

        [Test]
        public void Empty_IsScaleOne()
        {
            var sheet = new UpgradeSheet();
            foreach (UpgradeStat stat in System.Enum.GetValues(typeof(UpgradeStat)))
            {
                Assert.That(sheet.Scale(stat), Is.EqualTo(1f));
                Assert.That(sheet.Bonus(stat), Is.EqualTo(0f));
            }
            Assert.That(sheet.ScaleInt(UpgradeStat.MeleeDamage, 10), Is.EqualTo(10));
        }

        [Test]
        public void SamePickTwice_Stacks()
        {
            var sheet = new UpgradeSheet();
            RewardDefinition strength = Reward(new StatEffect(UpgradeStat.MeleeDamage, 20f));
            sheet.Add(strength);
            Assert.That(sheet.ScaleInt(UpgradeStat.MeleeDamage, 10), Is.EqualTo(12));
            sheet.Add(strength);
            Assert.That(sheet.ScaleInt(UpgradeStat.MeleeDamage, 10), Is.EqualTo(14));
            Assert.That(sheet.Scale(UpgradeStat.MeleeDamage), Is.EqualTo(1.4f).Within(1e-5f));
        }

        [Test]
        public void ScaleInt_RoundsHalfUp_AndNeverBelowOne()
        {
            var sheet = new UpgradeSheet();
            sheet.Add(Reward(new StatEffect(UpgradeStat.PistolMagazine, 20f)));
            Assert.That(sheet.ScaleInt(UpgradeStat.PistolMagazine, 12), Is.EqualTo(14)); // 14.4
            Assert.That(sheet.ScaleInt(UpgradeStat.PistolDamage, 6), Is.EqualTo(6));
            sheet.Add(Reward(new StatEffect(UpgradeStat.PistolDamage, 25f)));
            Assert.That(sheet.ScaleInt(UpgradeStat.PistolDamage, 6), Is.EqualTo(8)); // 7.5
            sheet.Add(Reward(new StatEffect(UpgradeStat.MeleeDamage, -500f)));
            Assert.That(sheet.ScaleInt(UpgradeStat.MeleeDamage, 10), Is.EqualTo(1));
        }

        [Test]
        public void OneRewardWithTwoEffects_RaisesBoth_AndTellsOnce()
        {
            var sheet = new UpgradeSheet();
            int changed = 0;
            sheet.Changed += () => changed++;
            sheet.Add(Reward(new StatEffect(UpgradeStat.RollSpeed, 40f), new StatEffect(UpgradeStat.RollRecovery, 40f)));
            sheet.Add(Reward()); // 효과 없는 보상은 알리지 않는다

            Assert.That(sheet.Scale(UpgradeStat.RollSpeed), Is.EqualTo(1.4f).Within(1e-5f));
            Assert.That(sheet.Scale(UpgradeStat.RollRecovery), Is.EqualTo(1.4f).Within(1e-5f));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void EveryStat_HasALabel()
        {
            foreach (UpgradeStat stat in System.Enum.GetValues(typeof(UpgradeStat)))
                Assert.That(UpgradeStats.Label(stat), Is.Not.EqualTo(stat.ToString()), stat.ToString());
            Assert.That(UpgradeStats.Describe(new StatEffect(UpgradeStat.MeleeDamage, 20f)), Is.EqualTo("근접 피해 +20%"));
        }

        /// <summary>BALANCE.md 12번과 RewardTable 에셋이 같은지.</summary>
        [Test]
        public void RewardTable_MatchesBalance()
        {
            var table = AssetDatabase.LoadAssetAtPath<RewardTable>("Assets/Game/Data/RewardTable.asset");
            Assert.That(table, Is.Not.Null);
            Dictionary<string, RewardDefinition> byId = table.ToDefinitions().ToDictionary(r => r.Id);

            void Expect(string id, params (UpgradeStat stat, float percent)[] effects)
            {
                RewardDefinition reward = byId[id];
                Assert.That(reward.ComingLater, Is.False, id);
                Assert.That(reward.Effects.Select(e => (e.stat, e.percent)), Is.EqualTo(effects), id);
            }

            Expect("beast_strength", (UpgradeStat.MeleeDamage, 20f));
            Expect("beast_move", (UpgradeStat.MoveSpeed, 10f));
            Expect("beast_regen", (UpgradeStat.HealOnRoomClear, 20f));
            Expect("human_pistol", (UpgradeStat.PistolDamage, 20f));
            Expect("human_magazine", (UpgradeStat.PistolMagazine, 20f));
            Expect("human_aim", (UpgradeStat.AimTransition, 20f), (UpgradeStat.AimMoveSpeed, 20f));
            Expect("beast_dodge", (UpgradeStat.RollSpeed, 40f), (UpgradeStat.RollRecovery, 40f));
            foreach (string later in new[] { "beast_charge", "human_shotgun", "human_explosive" })
            {
                Assert.That(byId[later].ComingLater, Is.True, later);
                Assert.That(byId[later].Effects, Is.Empty, later);
            }
            Assert.That(byId.Count, Is.EqualTo(10));
        }
    }
}
