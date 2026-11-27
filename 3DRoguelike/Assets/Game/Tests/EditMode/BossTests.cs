using System;
using System.Collections.Generic;
using Game.Rules;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    /// <summary>숲 보스(3-5): 패턴 순서 규칙 · 결정성 · 강화 · 모양 계산 · 까치 수치 데이터 · 던전마다 보스 고르기.</summary>
    public class BossTests
    {
        const string TuningPath = "Assets/Game/Data/Bosses/MagpieBossTuning.asset";
        const string WorldTuningPath = "Assets/Game/Data/WorldTuning.asset";

        static BossPatternSettings Balance() => new BossPatternSettings(1.2f, 2f, 0.6f, 1.1f, 0.5f);

        static BossPatternPlanner Planner(uint seed, int dungeon = 0)
        {
            return new BossPatternPlanner(Balance(), new RunRandom(new RunSeed(seed)).Create(RandomPurpose.Boss, dungeon));
        }

        static List<BossPattern> Patterns(BossPatternPlanner planner, int count, int enrageAt = -1)
        {
            var list = new List<BossPattern>();
            for (int i = 0; i < count; i++)
            {
                if (i == enrageAt) planner.UpdateHealth(150, 300);
                list.Add(planner.Next().Pattern);
            }
            return list;
        }

        [Test]
        public void SameSeed_SamePatternOrderAndIdle()
        {
            BossPatternPlanner a = Planner(12345u), b = Planner(12345u);
            for (int i = 0; i < 40; i++)
            {
                BossStep x = a.Next(), y = b.Next();
                Assert.That(y.Pattern, Is.EqualTo(x.Pattern), $"차례 {i}");
                Assert.That(y.IdleSeconds, Is.EqualTo(x.IdleSeconds), $"차례 {i}");
            }
        }

        [Test]
        public void DifferentSeedOrDungeon_DifferentOrder()
        {
            List<BossPattern> baseline = Patterns(Planner(12345u), 20);
            Assert.That(Patterns(Planner(54321u), 20), Is.Not.EqualTo(baseline), "다른 시드");
            Assert.That(Patterns(Planner(12345u, 1), 20), Is.Not.EqualTo(baseline), "다른 던전 순서");
        }

        [Test]
        public void FixedSeed_GivesFixedOrder()
        {
            // 이 값이 바뀌면 같은 시드의 보스 패턴 순서가 바뀐 것이다(난수 키 boss · 규칙 확인)
            List<BossPattern> order = Patterns(new BossPatternPlanner(Balance(), new RunRandom(RunSeed.TryParse("9TNJ-W2Z5", out RunSeed seed) ? seed : default).Create(RandomPurpose.Boss, 0)), 6);
            Assert.That(order, Is.EqualTo(FixedOrder9TNJ));
        }

        /// <summary>시드 9TNJ-W2Z5 · 던전 1(순서 0)의 처음 6패턴 — 플레이로 본 순서와 같다.</summary>
        static readonly BossPattern[] FixedOrder9TNJ =
        {
            BossPattern.Charge, BossPattern.Slam, BossPattern.Charge, BossPattern.Slam, BossPattern.Charge, BossPattern.FeatherVolley,
        };

        [Test]
        public void NeverSamePatternTwiceInARow_AllPatternsAppear()
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                List<BossPattern> order = Patterns(Planner(seed), 30);
                for (int i = 1; i < order.Count; i++) Assert.That(order[i], Is.Not.EqualTo(order[i - 1]), $"시드 {seed} 차례 {i}");
                foreach (BossPattern pattern in Enum.GetValues(typeof(BossPattern))) Assert.That(order, Does.Contain(pattern), $"시드 {seed}");
            }
        }

        [Test]
        public void EnrageTiming_DoesNotChangePatternOrder()
        {
            List<BossPattern> calm = Patterns(Planner(777u), 25);
            Assert.That(Patterns(Planner(777u), 25, enrageAt: 3), Is.EqualTo(calm));
            Assert.That(Patterns(Planner(777u), 25, enrageAt: 11), Is.EqualTo(calm));
        }

        [Test]
        public void IdleTime_InRange_ShorterWhenEnraged()
        {
            BossPatternPlanner planner = Planner(99u);
            for (int i = 0; i < 50; i++)
            {
                float idle = planner.Next().IdleSeconds;
                Assert.That(idle, Is.InRange(1.2f, 2f));
            }
            planner.UpdateHealth(100, 300);
            for (int i = 0; i < 50; i++)
            {
                float idle = planner.Next().IdleSeconds;
                Assert.That(idle, Is.InRange(0.6f, 1.1f));
            }
        }

        [Test]
        public void Enrage_AtHalfHealth_Once_NotWhenDead()
        {
            BossPatternPlanner planner = Planner(1u);
            Assert.That(planner.UpdateHealth(151, 300), Is.False);
            Assert.That(planner.Enraged, Is.False);
            Assert.That(planner.UpdateHealth(150, 300), Is.True, "절반이면 강화");
            Assert.That(planner.UpdateHealth(100, 300), Is.False, "한 번만");
            Assert.That(planner.Enraged, Is.True);

            BossPatternPlanner killed = Planner(1u);
            Assert.That(killed.UpdateHealth(0, 300), Is.False, "한 번에 죽으면 강화 없음");
        }

        [Test]
        public void FanAngles_SpreadEvenly_OffsetHalfFillsGaps()
        {
            float[] seven = BossShapes.FanAngles(7, 70f, false);
            Assert.That(seven.Length, Is.EqualTo(7));
            Assert.That(seven[0], Is.EqualTo(-35f).Within(1e-4f));
            Assert.That(seven[3], Is.EqualTo(0f).Within(1e-4f), "가운데 = 조준선");
            Assert.That(seven[6], Is.EqualTo(35f).Within(1e-4f));

            float[] shifted = BossShapes.FanAngles(7, 70f, true);
            for (int i = 0; i < 6; i++) Assert.That(shifted[i], Is.EqualTo((seven[i] + seven[i + 1]) * 0.5f).Within(1e-4f), "엇갈린 연사는 틈 가운데");

            Assert.That(BossShapes.FanAngles(1, 70f, false), Is.EqualTo(new[] { 0f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => BossShapes.FanAngles(0, 70f, false));
        }

        [Test]
        public void DistanceToEdge_StopsAtRoomWall()
        {
            Assert.That(BossShapes.DistanceToEdge(0f, 0f, 1f, 0f, 10f, 10f, 100f), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(BossShapes.DistanceToEdge(0f, 0f, 1f, 0f, 10f, 10f, 4f), Is.EqualTo(4f).Within(1e-4f), "최대 거리");
            float d = (float)Math.Sqrt(0.5);
            Assert.That(BossShapes.DistanceToEdge(5f, 0f, d, d, 10f, 10f, 100f), Is.EqualTo(5f / d).Within(1e-3f), "대각선은 먼저 닿는 벽");
            Assert.That(BossShapes.DistanceToEdge(0f, -10.001f, 0f, 1f, 10f, 10f, 100f), Is.EqualTo(20f).Within(1e-3f), "벽에 붙어 안쪽으로");
            Assert.That(BossShapes.DistanceToEdge(0f, -10f, 0f, -1f, 10f, 10f, 100f), Is.EqualTo(0f).Within(1e-4f), "벽 쪽으로는 못 감");
        }

        [Test]
        public void Settings_RejectBadValues()
        {
            Assert.Throws<ArgumentException>(() => new BossPatternPlanner(new BossPatternSettings(2f, 1f, 0.6f, 1.1f, 0.5f), new Pcg32(1, 1)));
            Assert.Throws<ArgumentException>(() => new BossPatternPlanner(new BossPatternSettings(1f, 2f, 0.6f, 1.1f, 1.5f), new Pcg32(1, 1)));
            Assert.Throws<ArgumentNullException>(() => new BossPatternPlanner(Balance(), null));
        }

        [Test]
        public void BossRandom_IsOwnPurpose()
        {
            var keys = new HashSet<string>();
            foreach (RandomPurpose purpose in new[] { RandomPurpose.Map, RandomPurpose.Decor, RandomPurpose.Reward, RandomPurpose.Enemy, RandomPurpose.Combat, RandomPurpose.Effects, RandomPurpose.Road, RandomPurpose.Dressing, RandomPurpose.Boss })
            {
                Assert.That(keys.Add(purpose.Key), Is.True, purpose.Key);
            }
            Assert.That(RandomPurpose.Boss.Key, Is.EqualTo("boss"));
        }

        [Test]
        public void MagpieTuning_MatchesBalance()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<BossTuning>(TuningPath);
            Assert.That(tuning, Is.Not.Null, "Setup Floor 실행");
            Assert.That(tuning.displayName, Is.EqualTo("까치"));
            Assert.That(tuning.enragedLabel, Is.EqualTo("격노"));
            Assert.That(tuning.maxHealth, Is.EqualTo(300));
            Assert.That(tuning.enrageHealthRatio, Is.EqualTo(0.5f));
            Assert.That(tuning.idleTime.x, Is.EqualTo(1.2f)); Assert.That(tuning.idleTime.y, Is.EqualTo(2f));
            Assert.That(tuning.enragedIdleTime.x, Is.EqualTo(0.6f)); Assert.That(tuning.enragedIdleTime.y, Is.EqualTo(1.1f));
            Assert.That(tuning.chargeWindup, Is.EqualTo(0.9f));
            Assert.That(tuning.chargeSpeed, Is.EqualTo(10.9f));
            Assert.That(tuning.chargeDistance, Is.EqualTo(14.5f));
            Assert.That(tuning.chargeDamage, Is.EqualTo(20));
            Assert.That(tuning.featherCount, Is.EqualTo(7)); Assert.That(tuning.enragedFeatherCount, Is.EqualTo(11));
            Assert.That(tuning.volleys, Is.EqualTo(1)); Assert.That(tuning.enragedVolleys, Is.EqualTo(2));
            Assert.That(tuning.featherDamage, Is.EqualTo(10));
            Assert.That(tuning.slamLockTime, Is.EqualTo(0.45f));
            Assert.That(tuning.slamFallTime, Is.EqualTo(0.22f));
            Assert.That(tuning.slamRadius, Is.EqualTo(2.9f));
            Assert.That(tuning.slamDamage, Is.EqualTo(30));
            Assert.That(tuning.prefab, Is.Not.Null, "보스 프리팹 연결");
            Assert.That(tuning.featherPrefab, Is.Not.Null, "깃털 프리팹 연결");
            Assert.That(tuning.prefab.Tuning, Is.SameAs(tuning));
        }

        [Test]
        public void Dungeons_ChooseBossPerDungeon_ForestMagpie_CityNone()
        {
            var world = AssetDatabase.LoadAssetAtPath<WorldTuning>(WorldTuningPath);
            var tuning = AssetDatabase.LoadAssetAtPath<BossTuning>(TuningPath);
            Assert.That(world.dungeons[0].boss, Is.SameAs(tuning), "숲 던전 1 = 까치");
            Assert.That(world.dungeons[1].boss, Is.Null, "도시 던전은 이번 범위 아님(보스 방 일반 적 그대로)");
        }
    }
}
