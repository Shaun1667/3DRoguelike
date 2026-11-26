using System.Linq;
using Game.Rules;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    /// <summary>BALANCE.md 9 · 14번과 적 데이터 에셋 값이 같은지 검사한다. 값을 바꾸면 여기도 함께 고친다.</summary>
    public class EnemyTuningTests
    {
        static T Load<T>(string name) where T : EnemyTuning
        {
            var tuning = AssetDatabase.LoadAssetAtPath<T>($"Assets/Game/Data/Enemies/{name}.asset");
            Assert.That(tuning, Is.Not.Null, name);
            return tuning;
        }

        static void AssertCommon(EnemyTuning tuning, int maxHealth, float moveSpeed, float turnSpeed)
        {
            Assert.That(tuning.maxHealth, Is.EqualTo(maxHealth));
            Assert.That(tuning.evolvedHealthMultiplier, Is.EqualTo(1.8f));
            Assert.That(tuning.moveSpeed, Is.EqualTo(moveSpeed));
            Assert.That(tuning.turnSpeed, Is.EqualTo(turnSpeed));
            Assert.That(tuning.repathInterval, Is.EqualTo(0.2f));
            Assert.That(tuning.spawnTime, Is.EqualTo(1f));
            Assert.That(tuning.extraAttackEvery, Is.EqualTo(2));
            Assert.That(tuning.hitFlashTime, Is.EqualTo(0.08f));
            Assert.That(tuning.corpseTime, Is.EqualTo(1f));
            Assert.That(tuning.HealthFor(EnemyGrade.Mutant), Is.EqualTo(maxHealth));
            Assert.That(tuning.HealthFor(EnemyGrade.Evolved), Is.EqualTo((int)System.Math.Round(maxHealth * 1.8, System.MidpointRounding.ToEven)));
        }

        /// <summary>오소리 Mutant 값은 2-4 Mutant(BALANCE 9번 예전 표) 그대로.</summary>
        [Test]
        public void BadgerTuning_MatchesBalance()
        {
            var t = Load<BadgerTuning>("BadgerTuning");
            AssertCommon(t, 30, 3f, 540f);
            Assert.That(t.windupLeanAngle, Is.EqualTo(25f));
            Assert.That(t.strikeLeanAngle, Is.EqualTo(20f));
            Assert.That(t.attackStartRange, Is.EqualTo(1.8f));
            Assert.That(t.windupTime, Is.EqualTo(1f));
            Assert.That(t.hitRange, Is.EqualTo(2.2f));
            Assert.That(t.hitAngle, Is.EqualTo(100f));
            Assert.That(t.damage, Is.EqualTo(10));
            Assert.That(t.recoverTime, Is.EqualTo(1.3f));
            Assert.That(t.comboHits, Is.EqualTo(3));
            Assert.That(t.comboInterval, Is.EqualTo(0.55f));
            Assert.That(t.comboDamage, Is.EqualTo(8));
            Assert.That(t.comboStep, Is.EqualTo(0.6f));
            Assert.That(t.comboRecoverTime, Is.EqualTo(1.6f));
        }

        [Test]
        public void BoarTuning_MatchesBalance()
        {
            var t = Load<BoarTuning>("BoarTuning");
            AssertCommon(t, 70, 2.6f, 360f);
            Assert.That(t.chargeStartRange, Is.EqualTo(12f));
            Assert.That(t.windupTime, Is.EqualTo(1.2f));
            Assert.That(t.chargeSpeed, Is.EqualTo(9f));
            Assert.That(t.chargeDistance, Is.EqualTo(15f));
            Assert.That(t.hitRadius, Is.EqualTo(1.2f));
            Assert.That(t.damage, Is.EqualTo(20));
            Assert.That(t.knockbackSpeed, Is.EqualTo(12f));
            Assert.That(t.stunTime, Is.EqualTo(2f));
            Assert.That(t.recoverTime, Is.EqualTo(1f));
            Assert.That(t.slamWindupTime, Is.EqualTo(0.5f));
            Assert.That(t.slamRadius, Is.EqualTo(3f));
            Assert.That(t.slamDamage, Is.EqualTo(15));
            Assert.That(t.slamRecoverTime, Is.EqualTo(1.2f));
        }

        [Test]
        public void HareTuning_MatchesBalance()
        {
            var t = Load<HareTuning>("HareTuning");
            AssertCommon(t, 12, 4.2f, 540f);
            Assert.That(t.leapStartRange, Is.EqualTo(5f));
            Assert.That(t.crouchTime, Is.EqualTo(1f));
            Assert.That(t.leapDistance, Is.EqualTo(6f));
            Assert.That(t.leapTime, Is.EqualTo(0.4f));
            Assert.That(t.leapHeight, Is.EqualTo(0.8f));
            Assert.That(t.hitRadius, Is.EqualTo(0.9f));
            Assert.That(t.damage, Is.EqualTo(6));
            Assert.That(t.recoverTime, Is.EqualTo(2f));
            Assert.That(t.slamCrouchTime, Is.EqualTo(0.8f));
            Assert.That(t.slamLeapDistance, Is.EqualTo(8f));
            Assert.That(t.slamLeapTime, Is.EqualTo(0.8f));
            Assert.That(t.slamLeapHeight, Is.EqualTo(3f));
            Assert.That(t.slamRadius, Is.EqualTo(2f));
            Assert.That(t.slamDamage, Is.EqualTo(10));
            Assert.That(t.slamRecoverTime, Is.EqualTo(1f));
        }

        [Test]
        public void ToadTuning_MatchesBalance()
        {
            var t = Load<ToadTuning>("ToadTuning");
            AssertCommon(t, 24, 2.2f, 540f);
            Assert.That(t.keepMin, Is.EqualTo(8f));
            Assert.That(t.keepMax, Is.EqualTo(14f));
            Assert.That(t.retreatStep, Is.EqualTo(4f));
            Assert.That(t.windupTime, Is.EqualTo(1.2f));
            Assert.That(t.flightTime, Is.EqualTo(0.9f));
            Assert.That(t.arcHeight, Is.EqualTo(3f));
            Assert.That(t.splashRadius, Is.EqualTo(1.5f));
            Assert.That(t.damage, Is.EqualTo(8));
            Assert.That(t.recoverTime, Is.EqualTo(1f));
            Assert.That(t.attackInterval, Is.EqualTo(2.5f));
            Assert.That(t.puddleRadius, Is.EqualTo(2f));
            Assert.That(t.puddleTime, Is.EqualTo(4f));
            Assert.That(t.puddleDamagePerSecond, Is.EqualTo(4));
            Assert.That(t.puddleTickInterval, Is.EqualTo(1f));
            Assert.That(t.fanShots, Is.EqualTo(3));
            Assert.That(t.fanAngle, Is.EqualTo(25f));
        }

        /// <summary>BALANCE.md 14번. EnemyMixTests.BalanceMix와도 같아야 한다.</summary>
        [Test]
        public void EnemyRoster_MatchesBalance_AndHasAllPrefabs()
        {
            var roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>("Assets/Game/Data/EnemyRoster.asset");
            Assert.That(roster, Is.Not.Null);

            (EnemySpecies species, int slots, int min, int max, float spread)[] expected =
            {
                (EnemySpecies.Badger, 1, 1, 1, 0f),
                (EnemySpecies.Boar, 2, 1, 1, 0f),
                (EnemySpecies.Hare, 2, 3, 5, 1.5f),
                (EnemySpecies.Toad, 1, 1, 1, 0f),
            };
            Assert.That(roster.species.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                EnemyRoster.SpeciesEntry entry = roster.species[i];
                Assert.That(entry.species, Is.EqualTo(expected[i].species));
                Assert.That(entry.slots, Is.EqualTo(expected[i].slots));
                Assert.That(entry.groupMin, Is.EqualTo(expected[i].min));
                Assert.That(entry.groupMax, Is.EqualTo(expected[i].max));
                Assert.That(entry.groupSpread, Is.EqualTo(expected[i].spread));
                Assert.That(entry.mutantPrefab, Is.Not.Null, $"{entry.species} Mutant 프리팹");
                Assert.That(entry.evolvedPrefab, Is.Not.Null, $"{entry.species} E 프리팹");
                Assert.That(entry.mutantPrefab.GetComponent<Enemy>().Grade, Is.EqualTo(EnemyGrade.Mutant));
                Assert.That(entry.evolvedPrefab.GetComponent<Enemy>().Grade, Is.EqualTo(EnemyGrade.Evolved));
            }

            (float evolved, (EnemySpecies, int)[] weights)[] stages =
            {
                (0f, new[] { (EnemySpecies.Badger, 3), (EnemySpecies.Hare, 1), (EnemySpecies.Boar, 1), (EnemySpecies.Toad, 1) }),
                (0.25f, new[] { (EnemySpecies.Badger, 3), (EnemySpecies.Hare, 1), (EnemySpecies.Boar, 1), (EnemySpecies.Toad, 1) }),
                (0.5f, new[] { (EnemySpecies.Badger, 2), (EnemySpecies.Hare, 1), (EnemySpecies.Boar, 2), (EnemySpecies.Toad, 2) }),
            };
            Assert.That(roster.stages.Length, Is.EqualTo(stages.Length));
            for (int i = 0; i < stages.Length; i++)
            {
                Assert.That(roster.stages[i].evolvedChance, Is.EqualTo(stages[i].evolved));
                Assert.That(roster.stages[i].weights.Select(w => (w.species, w.weight)), Is.EqualTo(stages[i].weights));
            }
            // 던전 1 앞 방(시작 방에서 1) · 길 위 무리는 오소리 3 · 멧토끼 1(결정 2026-11-25, 길 위는 그대로), 단계 2 · 3은 따로 없음
            var light = new[] { (EnemySpecies.Badger, 3), (EnemySpecies.Hare, 1) };
            Assert.That(roster.stages[0].nearRoomDistance, Is.EqualTo(1));
            Assert.That(roster.stages[0].nearWeights.Select(w => (w.species, w.weight)), Is.EqualTo(light));
            Assert.That(roster.stages[0].roadWeights.Select(w => (w.species, w.weight)), Is.EqualTo(light));
            for (int i = 1; i < stages.Length; i++)
            {
                Assert.That(roster.stages[i].nearRoomDistance, Is.EqualTo(0), $"단계 {i + 1}");
                Assert.That(roster.stages[i].nearWeights == null || roster.stages[i].nearWeights.Length == 0, $"단계 {i + 1}");
                Assert.That(roster.stages[i].roadWeights == null || roster.stages[i].roadWeights.Length == 0, $"단계 {i + 1}");
            }
            Assert.DoesNotThrow(() => roster.ToMix());
        }
    }
}
