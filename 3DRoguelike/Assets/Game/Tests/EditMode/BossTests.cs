using System;
using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>보스(3-5 까치 · 2026-11-27 반달가슴곰): 패턴 순서 규칙 · 결정성 · 강화 · 모양 계산 · 보스 수치 데이터 · 던전마다 보스 고르기.</summary>
    public class BossTests
    {
        const string TuningPath = "Assets/Game/Data/Bosses/MagpieBossTuning.asset";
        const string BearTuningPath = "Assets/Game/Data/Bosses/BearBossTuning.asset";
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
                foreach (BossPattern pattern in BossPatternSets.Magpie) Assert.That(order, Does.Contain(pattern), $"시드 {seed}");
                foreach (BossPattern pattern in order) Assert.That(BossPatternSets.Magpie, Does.Contain(pattern), "까치 패턴만");
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
            var tuning = AssetDatabase.LoadAssetAtPath<MagpieBossTuning>(TuningPath);
            Assert.That(tuning, Is.Not.Null, "Setup Floor 실행");
            Assert.That(tuning.displayName, Is.EqualTo("까치"));
            Assert.That(tuning.enragedLabel, Is.EqualTo("격노"));
            Assert.That(tuning.maxHealth, Is.EqualTo(500), "2026-11-27 사용자 결정(예전 300)");
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
            Assert.That(tuning.Patterns, Is.EqualTo(new[] { BossPattern.Charge, BossPattern.FeatherVolley, BossPattern.Slam }), "예전 순서 그대로");
            Assert.That(tuning.prefab, Is.Not.Null, "보스 프리팹 연결");
            Assert.That(tuning.prefab, Is.InstanceOf<MagpieBoss>());
            Assert.That(tuning.featherPrefab, Is.Not.Null, "깃털 프리팹 연결");
            Assert.That(tuning.prefab.Tuning, Is.SameAs(tuning));
        }

        [Test]
        public void Dungeons_ChooseBossPerDungeon_ForestBear_CityMagpie()
        {
            var world = AssetDatabase.LoadAssetAtPath<WorldTuning>(WorldTuningPath);
            Assert.That(world.dungeons[0].boss, Is.SameAs(AssetDatabase.LoadAssetAtPath<BossTuning>(BearTuningPath)), "숲 던전 1 = 반달가슴곰");
            Assert.That(world.dungeons[1].boss, Is.SameAs(AssetDatabase.LoadAssetAtPath<BossTuning>(TuningPath)), "도시 던전 = 까치");
            Assert.That(world.dungeons[1].boss.intro, Is.Not.Null, "까치는 등장 연출 그대로");
            Assert.That(world.dungeons[0].boss.intro, Is.Null, "곰은 짧은 등장");
        }
    

        [Test]
        public void InFan_RadiusAndAngle()
        {
            Assert.That(BossShapes.InFan(0f, 4f, 0f, 1f, 4.6f, 120f), Is.True, "정면");
            Assert.That(BossShapes.InFan(0f, 4.7f, 0f, 1f, 4.6f, 120f), Is.False, "반지름 밖");
            float s = Mathf.Sin(59f * Mathf.Deg2Rad) * 3f, c = Mathf.Cos(59f * Mathf.Deg2Rad) * 3f;
            Assert.That(BossShapes.InFan(s, c, 0f, 1f, 4.6f, 120f), Is.True, "59도 안");
            s = Mathf.Sin(62f * Mathf.Deg2Rad) * 3f;
            c = Mathf.Cos(62f * Mathf.Deg2Rad) * 3f;
            Assert.That(BossShapes.InFan(s, c, 0f, 1f, 4.6f, 120f), Is.False, "62도 밖");
            Assert.That(BossShapes.InFan(0f, -2f, 0f, 1f, 4.6f, 120f), Is.False, "뒤");
            Assert.That(BossShapes.InFan(0f, -1f, 0f, 1f, 4.6f, 120f, nearRadius: 1.5f), Is.True, "몸에 붙으면 방향 무관");
        }


        [Test]
        public void Planner_RejectsBadPatternLists()
        {
            var random = new Pcg32(1, 1);
            Assert.Throws<ArgumentException>(() => new BossPatternPlanner(Balance(), new[] { BossPattern.Swipe }, random));
            Assert.Throws<ArgumentException>(() => new BossPatternPlanner(Balance(), new[] { BossPattern.Swipe, BossPattern.Swipe }, random));
            Assert.Throws<ArgumentException>(() => new BossPatternPlanner(Balance(), (BossPattern[])null, random));
        }


        [Test]
        public void BearPlanner_OnlyBearPatterns_NeverTwiceInARow_AllAppear()
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                List<BossPattern> order = Patterns(BearPlanner(seed), 30, enrageAt: 9);
                for (int i = 1; i < order.Count; i++) Assert.That(order[i], Is.Not.EqualTo(order[i - 1]), $"시드 {seed} 차례 {i}");
                foreach (BossPattern pattern in BossPatternSets.Bear) Assert.That(order, Does.Contain(pattern), $"시드 {seed}");
                foreach (BossPattern pattern in order) Assert.That(BossPatternSets.Bear, Does.Contain(pattern), "곰 패턴만");
            }
            Assert.That(BossPatternSets.Bear.Intersect(BossPatternSets.Magpie), Is.Empty, "까치와 곰은 패턴을 같이 쓰지 않는다");
        }


        [Test]
        public void MagpieList_SameAsOldConstructor()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                BossPatternPlanner old = Planner(seed, 1);
                var listed = new BossPatternPlanner(Balance(), BossPatternSets.Magpie, new RunRandom(new RunSeed(seed)).Create(RandomPurpose.Boss, 1));
                for (int i = 0; i < 20; i++)
                {
                    BossStep a = old.Next(), b = listed.Next();
                    Assert.That(b.Pattern, Is.EqualTo(a.Pattern));
                    Assert.That(b.IdleSeconds, Is.EqualTo(a.IdleSeconds));
                }
            }
        }


        static BossPatternPlanner BearPlanner(uint seed, int dungeon = 0)
        {
            return new BossPatternPlanner(new BossPatternSettings(1.3f, 2.1f, 0.7f, 1.2f, 0.5f), BossPatternSets.Bear, new RunRandom(new RunSeed(seed)).Create(RandomPurpose.Boss, dungeon));
        }


        /// <summary>프리팹을 잠깐 꺼내 스킨 렌더러 경계 크기를 잰다(프리팹 에셋은 경계가 없음).</summary>
        /// <summary>프리팹을 잠깐 꺼내 스킨 메시를 구워(저장된 자세 그대로) 정점 경계 크기를 잰다 — 렌더러 경계는 그리기 전엔 바인드 자세 기준이다.</summary>
        static Vector3 RenderedSize(GameObject prefab)
        {
            GameObject probe = UnityEngine.Object.Instantiate(prefab);
            var baked = new Mesh();
            try
            {
                SkinnedMeshRenderer[] renderers = probe.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(0), "리그 모델");
                bool first = true;
                Bounds bounds = default;
                foreach (SkinnedMeshRenderer r in renderers)
                {
                    r.BakeMesh(baked, true); // 렌더러 자기 좌표(부모 크기 빠짐) → localToWorld로 한 번
                    Matrix4x4 toWorld = r.transform.localToWorldMatrix;
                    foreach (Vector3 v in baked.vertices)
                    {
                        Vector3 p = toWorld.MultiplyPoint3x4(v);
                        if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                        else bounds.Encapsulate(p);
                    }
                }
                return bounds.size;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }


        [Test]
        public void BearPrefab_BigRigWithStatesAndShake()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<BearBossTuning>(BearTuningPath);
            Assert.That(tuning.prefab, Is.InstanceOf<BearBoss>());
            Assert.That(tuning.prefab.Tuning, Is.SameAs(tuning));
            var bear = (BearBoss)tuning.prefab;
            Assert.That(bear.Head, Is.Not.SameAs(bear.transform), "머리 뼈");
            Assert.That(bear.LaneWarning, Is.Not.Null);
            Assert.That(bear.SlamWarning, Is.Not.Null);
            Assert.That(bear.SwipeWarning.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(bear.HowlWarning, Is.Not.Null, "하울링 원 예고");
            Assert.That(bear.GetComponents<Component>().Any(c => c != null && c.GetType().Name == "CinemachineImpulseSource"), Is.True, "화면 흔들기");

            Vector3 size = RenderedSize(bear.gameObject);
            Assert.That(size.y, Is.GreaterThan(1.85f * 1.4f), "네 발로 서도 고라니(1.85 m)보다 훨씬 큼");
            Assert.That(size.z, Is.EqualTo(tuning.modelScale).Within(tuning.modelScale * 0.1f), "앞뒤 길이 = modelScale");

            var controller = bear.GetComponentInChildren<Animator>(true).runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            string[] names = { BearBoss.LocomotionState, BearBoss.SwipeState, BearBoss.SlamWindupState, BearBoss.SlamState, BearBoss.ChargeWindupState, BearBoss.ChargeState, BearBoss.StunState, BearBoss.RoarState, BearBoss.HowlState, BearBoss.DeathState };
            foreach (string name in names)
            {
                Assert.That(states.Any(s => s.state.name == name && s.state.motion != null), Is.True, name);
            }
            Assert.That(controller.parameters.First(p => p.name == BearBoss.TempoParameter).defaultFloat, Is.EqualTo(1f));
            Assert.That(controller.layers[BearBoss.HitLayer].blendingMode, Is.EqualTo(AnimatorLayerBlendingMode.Additive));
            AnimatorState swipe = states.First(s => s.state.name == BearBoss.SwipeState).state;
            Assert.That(tuning.swipeHitTime / swipe.speed, Is.EqualTo(tuning.swipeWindup).Within(1e-4f), "맞는 순간 = 예고 끝");
            AnimatorState howl = states.First(s => s.state.name == BearBoss.HowlState).state;
            Assert.That(tuning.howlHitTime / howl.speed, Is.EqualTo(tuning.howlWindup).Within(1e-4f), "포효 순간 = 예고 끝");
            Assert.That(howl.motion.name, Is.EqualTo(BearBoss.RoarState), "하울링은 포효 동작");
        }


        [Test]
        public void BearTelegraphs_CanBeDodged()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<BearBossTuning>(BearTuningPath);
            var player = AssetDatabase.LoadAssetAtPath<PlayerTuning>("Assets/Game/Data/PlayerTuning.asset");
            float tempo = tuning.enragedTempo;
            float sprint = player.moveSpeed * player.sprintMultiplier;
            float rollGuard = player.rollDuration * player.rollInvulnerableRatio;
            Assert.That(rollGuard, Is.GreaterThan(0.2f), "구르기 무적이 맞는 순간을 덮음");
            float[] telegraphs = { tuning.swipeWindup / tempo, (tuning.slamWindup + tuning.slamDropTime) / tempo, tuning.chargeWindup / tempo, tuning.howlWindup / tempo };
            foreach (float telegraph in telegraphs) Assert.That(telegraph, Is.GreaterThanOrEqualTo(0.55f), "예고를 보고 구를 시간");
            Assert.That(sprint * telegraphs[1], Is.GreaterThan(tuning.slamRadius * tuning.enragedSlamRadiusMultiplier), "내려찍기 원 가운데에서 달려 나감");
            Assert.That(sprint * telegraphs[2], Is.GreaterThan(tuning.chargeLaneWidth * 0.5f + tuning.bodyRadius), "돌진 길에서 옆으로 달려 나감");
            Assert.That(sprint * telegraphs[3], Is.GreaterThan(tuning.howlRadius * tuning.enragedHowlRadiusMultiplier - tuning.bodyRadius), "곰 몸에 붙어 있어도 하울링 원 밖으로 달려 나감(격노)");
        }


        [Test]
        public void BearTuning_MatchesBalance()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<BearBossTuning>(BearTuningPath);
            Assert.That(tuning, Is.Not.Null, "Setup Floor 실행");
            Assert.That(tuning.displayName, Is.EqualTo("반달가슴곰"));
            Assert.That(tuning.enragedLabel, Is.EqualTo("격노"));
            Assert.That(tuning.maxHealth, Is.EqualTo(350));
            Assert.That(tuning.enrageHealthRatio, Is.EqualTo(0.5f));
            Assert.That(tuning.modelScale, Is.EqualTo(4.5f), "길이 4.5 m");
            Assert.That(tuning.bodyRadius, Is.EqualTo(1.8f));
            Assert.That(tuning.swipeWindup, Is.EqualTo(0.75f));
            Assert.That(tuning.swipeRadius, Is.EqualTo(4.6f));
            Assert.That(tuning.swipeAngle, Is.EqualTo(120f));
            Assert.That(tuning.swipeDamage, Is.EqualTo(20));
            Assert.That(tuning.slamWindup, Is.EqualTo(1.1f));
            Assert.That(tuning.slamRadius, Is.EqualTo(3.6f));
            Assert.That(tuning.slamDamage, Is.EqualTo(30));
            Assert.That(tuning.slamPush, Is.EqualTo(20f));
            Assert.That(tuning.contactPush, Is.EqualTo(8f));
            Assert.That(tuning.chargeWindup, Is.EqualTo(1.0f));
            Assert.That(tuning.chargeSpeed, Is.EqualTo(8.5f));
            Assert.That(tuning.chargeDamage, Is.EqualTo(25));
            Assert.That(tuning.stunTime, Is.EqualTo(2.5f));
            Assert.That(tuning.enragedTempo, Is.EqualTo(1.25f));
            Assert.That(tuning.enragedSwipes, Is.EqualTo(2));
            Assert.That(tuning.Patterns, Is.EqualTo(new[] { BossPattern.Swipe, BossPattern.RearSlam, BossPattern.GroundCharge, BossPattern.Howl }));
            Assert.That(tuning.howlWindup, Is.EqualTo(1.2f));
            Assert.That(tuning.howlHitTime, Is.EqualTo(0.7f));
            Assert.That(tuning.howlRadius, Is.EqualTo(6.5f));
            Assert.That(tuning.enragedHowlRadiusMultiplier, Is.EqualTo(1.2f));
            Assert.That(tuning.howlDamage, Is.EqualTo(20));
            Assert.That(tuning.howlPush, Is.EqualTo(14f));
            Assert.That(tuning.howlRecover, Is.EqualTo(1.0f));
            Assert.That(tuning.intro, Is.Null, "긴 연출 없음");
            Assert.That(tuning.roarOnEntrance, Is.True);
        }


        [Test]
        public void BearFixedSeed_GivesFixedOrder()
        {
            // 이 값이 바뀌면 같은 시드의 곰 패턴 순서가 바뀐 것이다(시드 9TNJ-W2Z5 · 숲 던전 1)
            var tuning = AssetDatabase.LoadAssetAtPath<BearBossTuning>(BearTuningPath);
            RunSeed.TryParse("9TNJ-W2Z5", out RunSeed seed);
            var planner = new BossPatternPlanner(tuning.ToPatternSettings(), tuning.Patterns, new RunRandom(seed).Create(RandomPurpose.Boss, 0));
            Assert.That(Patterns(planner, 6), Is.EqualTo(new[]
            {
                BossPattern.Swipe, BossPattern.GroundCharge, BossPattern.Swipe, BossPattern.GroundCharge, BossPattern.Howl, BossPattern.RearSlam,
            }));
        }
}
}
