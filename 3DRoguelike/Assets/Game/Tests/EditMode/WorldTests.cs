using Game.Rules;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    /// <summary>월드 길 · 월드 진행 · 판 상태 한 곳(결정 2026-11-11: 고정 월드 + 던전만 무작위, 죽으면 길 시작부터).</summary>
    public class WorldTests
    {
        private static readonly RunSeed Seed = new RunSeed(1234u);

        private static WorldRoute LShape() => new WorldRoute(new[] { (0f, 0f), (0f, 100f), (50f, 100f) });

        [Test]
        public void Route_LengthAndPoints()
        {
            WorldRoute route = LShape();

            Assert.That(route.Length, Is.EqualTo(150f).Within(0.001f));
            Assert.That(route.PointAt(50f), Is.EqualTo((0f, 50f)));
            Assert.That(route.PointAt(125f).x, Is.EqualTo(25f).Within(0.001f));
            Assert.That(route.PointAt(-5f), Is.EqualTo((0f, 0f)));
            Assert.That(route.PointAt(999f), Is.EqualTo((50f, 100f)));
        }

        [Test]
        public void Route_BesideIsRightOfTravel()
        {
            WorldRoute route = LShape();

            // 북쪽(+z)으로 갈 때 오른쪽은 +x
            (float x, float z) = route.BesideAt(50f, 10f);
            Assert.That(x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(z, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void Route_DistanceFromFindsNearestSegment()
        {
            WorldRoute route = LShape();

            Assert.That(route.DistanceFrom(-8f, 40f, out float along), Is.EqualTo(8f).Within(0.001f));
            Assert.That(along, Is.EqualTo(40f).Within(0.001f));
            Assert.That(route.DistanceFrom(30f, 110f, out along), Is.EqualTo(10f).Within(0.001f));
            Assert.That(along, Is.EqualTo(130f).Within(0.001f));
        }

        [Test]
        public void Progress_DungeonsInOrderOpenGates()
        {
            var world = new WorldProgress(3);

            Assert.That(world.NextDungeon, Is.EqualTo(0));
            Assert.That(world.CanEnter(1), Is.False);
            Assert.That(world.Clear(1), Is.False);
            Assert.That(world.IsGateOpen(0), Is.False);

            Assert.That(world.Clear(0), Is.True);
            Assert.That(world.IsGateOpen(0), Is.True);
            Assert.That(world.Clear(0), Is.False);
            Assert.That(world.NextDungeon, Is.EqualTo(1));

            world.Clear(1);
            world.Clear(2);
            Assert.That(world.AllCleared, Is.True);
            Assert.That(world.NextDungeon, Is.EqualTo(-1));
        }

        [Test]
        public void Progress_EncounterClearedOnce()
        {
            var world = new WorldProgress(1);

            Assert.That(world.IsEncounterCleared(2), Is.False);
            Assert.That(world.ClearEncounter(2), Is.True);
            Assert.That(world.ClearEncounter(2), Is.False);
            Assert.That(world.IsEncounterCleared(2), Is.True);
        }

        [Test]
        public void Session_EnterFinishReturnsToEntrance()
        {
            var session = new RunSession(Seed, 2);

            Assert.That(session.InDungeon, Is.False);
            Assert.That(session.ReturnDungeon, Is.EqualTo(-1));
            Assert.That(session.PlayerHealth, Is.EqualTo(RunSession.FullHealth));
            Assert.That(session.EnterDungeon(1, 100), Is.False, "순서대로만");
            Assert.That(session.EnterDungeon(0, 80), Is.True);
            Assert.That(session.CurrentDungeon, Is.EqualTo(0));
            Assert.That(session.PlayerHealth, Is.EqualTo(80));

            session.Stats.AddBattleRooms(7);
            Assert.That(session.FinishDungeon(55, 60f), Is.False, "아직 던전이 남았다");
            Assert.That(session.InDungeon, Is.False);
            Assert.That(session.ReturnDungeon, Is.EqualTo(0));
            Assert.That(session.PlayerHealth, Is.EqualTo(55));
            Assert.That(session.World.IsGateOpen(0), Is.True);
            Assert.That(session.Stats.DungeonsCleared, Is.EqualTo(1));
            Assert.That(session.Stats.BattleRooms, Is.EqualTo(7));
        }

        [Test]
        public void Session_LastDungeonClearsRegion()
        {
            var session = new RunSession(Seed, 2);
            session.EnterDungeon(0, 100);
            session.FinishDungeon(100, 10f);
            session.EnterDungeon(1, 100);

            Assert.That(session.FinishDungeon(90, 300f), Is.True);
            Assert.That(session.Stats.Outcome, Is.EqualTo(RunOutcome.RegionCleared));
            Assert.That(session.Stats.ElapsedSeconds, Is.EqualTo(300f));
            Assert.That(session.Stats.DungeonsCleared, Is.EqualTo(2));
            Assert.That(session.EnterDungeon(0, 100), Is.False, "판이 끝났다");
        }

        [Test]
        public void Session_StatsCarryAcrossDungeons()
        {
            var session = new RunSession(Seed, 3);
            var reward = new RewardDefinition("strength", "근력", "", Tendency.Beast, 1, RewardRarity.Common);
            session.Stats.TakeReward(reward);
            session.EnterDungeon(0, 100);
            session.FinishDungeon(100, 1f);

            Assert.That(session.Stats.Beast, Is.EqualTo(1));
            Assert.That(session.Stats.Dungeons, Is.EqualTo(3));
        }

        [Test]
        public void HealthPool_SetCurrentClamps()
        {
            var pool = new HealthPool(100);

            pool.SetCurrent(40);
            Assert.That(pool.Current, Is.EqualTo(40));
            pool.SetCurrent(500);
            Assert.That(pool.Current, Is.EqualTo(100));
            pool.SetCurrent(-3);
            Assert.That(pool.Current, Is.EqualTo(0));
        }

        /// <summary>BALANCE.md 13번.</summary>
        [Test]
        public void WorldTuning_MatchesBalance()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<WorldTuning>("Assets/Game/Data/WorldTuning.asset");

            Assert.That(tuning, Is.Not.Null);
            Assert.That(tuning.route.Length, Is.EqualTo(9));
            Assert.That(tuning.ToRoute().Length, Is.EqualTo(1055f).Within(2f));
            Assert.That(tuning.valleyHalfWidth, Is.EqualTo(55f));
            Assert.That(tuning.passHalfWidth, Is.EqualTo(6f));
            Assert.That(tuning.passLength, Is.EqualTo(30f));
            Assert.That(tuning.passBlend, Is.EqualTo(45f));
            Assert.That(tuning.cliffHeight, Is.EqualTo(28f));
            Assert.That(tuning.cliffWidth, Is.EqualTo(10f));
            Assert.That(tuning.dungeons.Length, Is.EqualTo(3));
            Assert.That(tuning.dungeons[0].routeDistance, Is.EqualTo(300f));
            Assert.That(tuning.dungeons[1].routeDistance, Is.EqualTo(600f));
            Assert.That(tuning.dungeons[2].routeDistance, Is.EqualTo(900f));
            Assert.That(tuning.gateAfterEntrance, Is.EqualTo(70f));
            Assert.That(tuning.entranceRadius, Is.EqualTo(2.5f));
            Assert.That(tuning.encounters.Length, Is.EqualTo(6));
            Assert.That(tuning.encounterTriggerRange, Is.EqualTo(22f));
            Assert.That(tuning.treeCount, Is.EqualTo(900));
            Assert.That(tuning.treeKinds.Length, Is.EqualTo(3));
            Assert.That(tuning.treeKinds[0].id, Is.EqualTo("Oak"));
            Assert.That(tuning.treeKinds[0].weight, Is.EqualTo(50f));
            Assert.That(tuning.treeKinds[1].id, Is.EqualTo("Pine"));
            Assert.That(tuning.treeKinds[1].weight, Is.EqualTo(35f));
            Assert.That(tuning.treeKinds[2].id, Is.EqualTo("Mutant"));
            Assert.That(tuning.treeKinds[2].weight, Is.EqualTo(15f));
            Assert.That(tuning.treeKinds[2].weightNearDungeon, Is.EqualTo(35f));
            Assert.That(tuning.treeNearDungeonRadius, Is.EqualTo(45f));
            Assert.That(tuning.treeScaleRange, Is.EqualTo(new UnityEngine.Vector2(0.85f, 1.15f)));
            // 바닥 · 분위기 (BALANCE 13번)
            Assert.That(tuning.forestFloorTile, Is.EqualTo(5f));
            Assert.That(tuning.dirtTrailTile, Is.EqualTo(4f));
            Assert.That(tuning.pollutedSoilTile, Is.EqualTo(5f));
            Assert.That(tuning.groundSmoothness, Is.EqualTo(0.05f));
            Assert.That(tuning.trailSmoothness, Is.EqualTo(0.1f));
            Assert.That(tuning.pollutedRadius, Is.EqualTo(28f));
            Assert.That(tuning.pollutedFade, Is.EqualTo(8f));
            Assert.That(tuning.pollutedEdgeNoise, Is.EqualTo(6f));
            Assert.That(tuning.pollutedNoiseScale, Is.EqualTo(0.08f));
            Assert.That(tuning.forestDirtMix, Is.EqualTo(0.15f));
            Assert.That(tuning.forestDirtNoiseScale, Is.EqualTo(0.05f));
            Assert.That(tuning.ambientColor, Is.EqualTo(new UnityEngine.Color(0.42f, 0.42f, 0.38f)));
            Assert.That(tuning.fogColor, Is.EqualTo(new UnityEngine.Color(0.5f, 0.55f, 0.48f)));
            Assert.That(tuning.fogStart, Is.EqualTo(70f));
            Assert.That(tuning.fogEnd, Is.EqualTo(320f));
            Assert.That(tuning.skyExposure, Is.EqualTo(1.1f));

            // 입구는 골짜기가 좁아지기 전(관문 고개 밖)에 있어야 한다
            foreach (WorldTuning.DungeonSite site in tuning.dungeons)
            {
                Assert.That(tuning.ValleyHalfWidthAt(site.routeDistance), Is.GreaterThan(System.Math.Abs(site.sideOffset) + 10f), site.displayName);
            }
        }
    }
}
