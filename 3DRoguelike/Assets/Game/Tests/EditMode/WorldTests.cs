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

        /// <summary>결정 2026-11-25(사용자): 마지막 던전 뒤에 다음 지역이 있으면 깨도 판이 끝나지 않고 관문만 열린다.</summary>
        [Test]
        public void Session_LastDungeonOpeningNextRegion_KeepsPlaying()
        {
            var session = new RunSession(Seed, 1, lastDungeonOpensNextRegion: true);
            session.EnterDungeon(0, 100);

            Assert.That(session.FinishDungeon(70, 120f), Is.False, "다음 지역이 열릴 뿐");
            Assert.That(session.Stats.Outcome, Is.EqualTo(RunOutcome.Playing));
            Assert.That(session.World.AllCleared, Is.True);
            Assert.That(session.World.IsGateOpen(0), Is.True);
            Assert.That(session.World.NextDungeon, Is.EqualTo(-1));
            Assert.That(session.ReturnDungeon, Is.EqualTo(0));
            Assert.That(session.PlayerHealth, Is.EqualTo(70));
            Assert.That(session.Stats.DungeonsCleared, Is.EqualTo(1));
            // 죽으면 처음부터(규칙 그대로)
            Assert.That(session.Stats.Die("test", 200f), Is.True);
            Assert.That(session.Stats.Outcome, Is.EqualTo(RunOutcome.Died));
        }

        /// <summary>
        /// 지금 월드(숲 던전 1 → 폐허 도시 → 도시 던전): 숲 던전 1을 깨면 판이 이어지고 관문 1이 열리며,
        /// 도시 던전을 깨면 지역 클리어 — 결과 화면 제목은 "폐허 도시 클리어"(결정 2026-11-25, 사용자).
        /// </summary>
        [Test]
        public void World_ForestThenCityDungeon_ClearsCity()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<WorldTuning>("Assets/Game/Data/WorldTuning.asset");
            var session = new RunSession(Seed, tuning.dungeons.Length, tuning.LastDungeonOpensRegion);

            Assert.That(session.EnterDungeon(0, 100), Is.True);
            Assert.That(session.FinishDungeon(90, 100f), Is.False, "숲 던전 1: 도시가 열릴 뿐");
            Assert.That(session.World.IsGateOpen(0), Is.True);
            Assert.That(session.World.NextDungeon, Is.EqualTo(1));
            Assert.That(tuning.RegionOfDungeon(session.World.NextDungeon), Is.EqualTo(tuning.RegionOpenedBy(0)), "다음 던전은 열린 지역에");

            Assert.That(session.EnterDungeon(1, 90), Is.True);
            Assert.That(session.FinishDungeon(60, 400f), Is.True, "도시 던전: 다음 지역이 없어 클리어");
            Assert.That(session.Stats.Outcome, Is.EqualTo(RunOutcome.RegionCleared));
            Assert.That(session.Stats.DungeonsCleared, Is.EqualTo(2));
            Assert.That(RunResultScreen.TitleFor(session.Stats.Outcome, tuning.ClearedRegionName(session.ReturnDungeon)), Is.EqualTo("폐허 도시 클리어"));
        }

        [Test]
        public void ResultTitle_ByOutcomeAndRegion()
        {
            Assert.That(RunResultScreen.TitleFor(RunOutcome.Died, "폐허 도시"), Is.EqualTo("쓰러졌다"));
            Assert.That(RunResultScreen.TitleFor(RunOutcome.RegionCleared, "숲"), Is.EqualTo("숲 클리어"));
            Assert.That(RunResultScreen.TitleFor(RunOutcome.RegionCleared, null), Is.EqualTo("지역 클리어"));
            Assert.That(RunResultScreen.TitleFor(RunOutcome.FloorCleared, "폐허 도시"), Is.EqualTo("층 클리어"), "층 씬만 시험할 때");
        }

        /// <summary>지역 표가 정하는 판의 끝: 던전 뒤 관문 너머에서 새 지역이 시작하면 그 던전이 그 지역을 연다. 마지막 지역 던전이 있으면 그것을 깨야 클리어.</summary>
        [Test]
        public void WorldTuning_RegionOpenedByDungeon()
        {
            var tuning = UnityEngine.ScriptableObject.CreateInstance<WorldTuning>();
            tuning.gateAfterEntrance = 70f;
            tuning.regions = new[]
            {
                new WorldTuning.Region { id = "A", startDistance = 0f, blendDistance = 1f },
                new WorldTuning.Region { id = "B", startDistance = 385f, blendDistance = 40f },
                new WorldTuning.Region { id = "C", startDistance = 900f, blendDistance = 40f },
            };
            // 지금 월드처럼 던전이 A에만: 던전 1이 B를 연다 → 깨도 판은 이어짐
            tuning.dungeons = new[] { new WorldTuning.DungeonSite { displayName = "a1", routeDistance = 300f } };
            Assert.That(tuning.RegionOpenedBy(0), Is.EqualTo(1));
            Assert.That(tuning.LastDungeonOpensRegion, Is.True);
            // A에 둘(첫 던전은 지역 안 관문) + B 던전 하나(B 끝이 C를 연다)
            tuning.dungeons = new[]
            {
                new WorldTuning.DungeonSite { displayName = "a1", routeDistance = 150f },
                new WorldTuning.DungeonSite { displayName = "a2", routeDistance = 300f },
                new WorldTuning.DungeonSite { displayName = "b1", routeDistance = 800f },
            };
            Assert.That(tuning.RegionOpenedBy(0), Is.EqualTo(-1), "지역 안 관문");
            Assert.That(tuning.RegionOpenedBy(1), Is.EqualTo(1));
            Assert.That(tuning.RegionOpenedBy(2), Is.EqualTo(2));
            Assert.That(tuning.LastDungeonOpensRegion, Is.True);
            // 마지막 지역(C)에 던전이 생기면 그것을 깨면 클리어
            tuning.dungeons = new[]
            {
                new WorldTuning.DungeonSite { displayName = "a1", routeDistance = 300f },
                new WorldTuning.DungeonSite { displayName = "b1", routeDistance = 800f },
                new WorldTuning.DungeonSite { displayName = "c1", routeDistance = 1000f },
            };
            Assert.That(tuning.RegionOpenedBy(2), Is.EqualTo(-1));
            Assert.That(tuning.LastDungeonOpensRegion, Is.False);
            var session = new RunSession(Seed, tuning.dungeons.Length, tuning.LastDungeonOpensRegion);
            for (int i = 0; i < 2; i++)
            {
                session.EnterDungeon(i, 100);
                Assert.That(session.FinishDungeon(100, 10f * (i + 1)), Is.False, $"던전 {i + 1}");
            }
            session.EnterDungeon(2, 100);
            Assert.That(session.FinishDungeon(100, 99f), Is.True, "다음 지역이 없는 마지막 던전 = 클리어");
            Assert.That(session.Stats.Outcome, Is.EqualTo(RunOutcome.RegionCleared));
            UnityEngine.Object.DestroyImmediate(tuning);
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
            // 길(결정 2026-11-25, 사용자: 숲은 1구간만): 숲 4점(관문 1 뒤 약 396 m) + 폐허 도시 1점(북쪽으로 320 m), 합 약 716 m
            Assert.That(tuning.route.Length, Is.EqualTo(5));
            Assert.That(tuning.ToRoute().Length, Is.EqualTo(715.6f).Within(1f));
            // 나무 자리 고정 기준 = 예전 숲 9점 길(약 1,055 m) · 관문 3 · 비운 자리 17곳
            Assert.That(tuning.treeReference.route.Length, Is.EqualTo(9));
            Assert.That(tuning.treeReference.passDistances, Is.EqualTo(new[] { 370f, 670f, 970f }));
            Assert.That(tuning.treeReference.keepOut.Length, Is.EqualTo(17));
            for (int i = 0; i < 4; i++) Assert.That(tuning.treeReference.route[i], Is.EqualTo(tuning.route[i]), $"숲 1구간 길 점 {i}은 나무 기준 길과 같다");
            Assert.That(tuning.valleyHalfWidth, Is.EqualTo(55f));
            Assert.That(tuning.passHalfWidth, Is.EqualTo(6f));
            Assert.That(tuning.passLength, Is.EqualTo(30f));
            Assert.That(tuning.passBlend, Is.EqualTo(45f));
            Assert.That(tuning.cliffHeight, Is.EqualTo(28f));
            Assert.That(tuning.cliffWidth, Is.EqualTo(10f));
            // 던전 2개: 숲 던전 1(300 m, 바위 입구) · 도시 던전(630 m 오른쪽 12.5 m, 대피소 프리팹) — 3-8 ⑤
            Assert.That(tuning.dungeons.Length, Is.EqualTo(2));
            Assert.That(tuning.dungeons[0].routeDistance, Is.EqualTo(300f));
            Assert.That(tuning.dungeons[0].HasEntrancePrefab, Is.False, "숲 던전은 바위 입구");
            Assert.That(tuning.dungeons[1].routeDistance, Is.EqualTo(630f));
            Assert.That(tuning.dungeons[1].sideOffset, Is.EqualTo(12.5f));
            Assert.That(tuning.dungeons[1].entrancePrefab, Is.EqualTo("Assets/Game/Prefabs/World/City/City_Shelter.prefab"));
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(tuning.dungeons[1].entrancePrefab), Is.Not.Null);
            Assert.That(tuning.gateAfterEntrance, Is.EqualTo(70f));
            Assert.That(tuning.entranceRadius, Is.EqualTo(2.5f));
            // 길 위 무리: 숲 1구간 5곳(결정 2026-11-17 자리 그대로, 자리 2·2·3·3·3) + 폐허 도시 4곳(3-8 ⑤, 자리 3·3·3·4, 도시 던전 단계)
            float[] encounterDistances = { 38f, 90f, 150f, 205f, 255f, 455f, 510f, 560f, 600f };
            int[] encounterCounts = { 2, 2, 3, 3, 3, 3, 3, 3, 4 };
            int[] encounterStages = { 0, 0, 0, 0, 0, 1, 1, 1, 1 };
            Assert.That(tuning.encounters.Length, Is.EqualTo(9));
            int slots = 0;
            for (int i = 0; i < tuning.encounters.Length; i++)
            {
                Assert.That(tuning.encounters[i].routeDistance, Is.EqualTo(encounterDistances[i]), $"무리 {i}");
                Assert.That(tuning.encounters[i].count, Is.EqualTo(encounterCounts[i]), $"무리 {i}");
                Assert.That(tuning.DungeonAhead(i), Is.EqualTo(encounterStages[i]), $"무리 {i} 단계");
                slots += tuning.encounters[i].count;
            }
            Assert.That(slots, Is.EqualTo(26));
            Assert.That(tuning.encounterTriggerRange, Is.EqualTo(26f));
            Assert.That(tuning.encounterSpread, Is.EqualTo(3f));
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
            Assert.That(tuning.regions[0].ambientColor, Is.EqualTo(new UnityEngine.Color(0.42f, 0.42f, 0.38f)));
            Assert.That(tuning.regions[0].fogColor, Is.EqualTo(new UnityEngine.Color(0.5f, 0.55f, 0.48f)));
            Assert.That(tuning.regions[0].fogStart, Is.EqualTo(70f));
            Assert.That(tuning.regions[0].fogEnd, Is.EqualTo(320f));
            Assert.That(tuning.skyExposure, Is.EqualTo(1.1f));

            // 길 위 무리: 던전 입구 앞뒤 20 m 밖, 좌우 5~10 m, 관문 고개 밖(골짜기가 옆 자리 + 10 m보다 넓음), 다음 지역 무리는 그 관문 뒤
            for (int i = 0; i < tuning.encounters.Length; i++)
            {
                WorldTuning.EncounterSite site = tuning.encounters[i];
                float side = System.Math.Abs(site.sideOffset);
                Assert.That(side, Is.InRange(5f, 10f), $"무리 {i} 옆");
                Assert.That(tuning.ValleyHalfWidthAt(site.routeDistance), Is.GreaterThan(side + tuning.encounterSpread + 10f), $"무리 {i} 골짜기 폭");
                foreach (WorldTuning.DungeonSite dungeon in tuning.dungeons)
                {
                    Assert.That(System.Math.Abs(site.routeDistance - dungeon.routeDistance), Is.GreaterThan(20f), $"무리 {i} · {dungeon.displayName}");
                }
                int stage = tuning.DungeonAhead(i);
                if (stage > 0) Assert.That(site.routeDistance, Is.GreaterThan(tuning.GateDistance(stage - 1)), $"무리 {i}는 관문 {stage} 뒤");
                if (i > 0) Assert.That(site.sideOffset * tuning.encounters[i - 1].sideOffset, Is.LessThan(0f), $"무리 {i} 좌우 번갈아");
            }
            // 시작 자리(길 6 m)에서 첫 무리까지 트리거 거리 + 5 m 넘게 — 시작하자마자 나타나지 않게
            WorldRoute worldRoute = tuning.ToRoute();
            (float startX, float startZ) = worldRoute.PointAt(6f);
            (float firstX, float firstZ) = worldRoute.BesideAt(tuning.encounters[0].routeDistance, tuning.encounters[0].sideOffset);
            float toFirst = (float)System.Math.Sqrt((firstX - startX) * (firstX - startX) + (firstZ - startZ) * (firstZ - startZ));
            Assert.That(toFirst, Is.GreaterThan(tuning.encounterTriggerRange + 5f));

            // 입구는 골짜기가 좁아지기 전(관문 고개 밖)에 있어야 한다
            foreach (WorldTuning.DungeonSite site in tuning.dungeons)
            {
                Assert.That(tuning.ValleyHalfWidthAt(site.routeDistance), Is.GreaterThan(System.Math.Abs(site.sideOffset) + 10f), site.displayName);
            }
        }

        /// <summary>BALANCE.md 13-1 지역 표(숲 → 폐허 도시).</summary>
        [Test]
        public void WorldTuning_RegionsTable()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<WorldTuning>("Assets/Game/Data/WorldTuning.asset");
            WorldRoute route = tuning.ToRoute();

            Assert.That(tuning.regions.Length, Is.EqualTo(2));
            WorldTuning.Region forest = tuning.regions[0];
            WorldTuning.Region city = tuning.regions[1];
            Assert.That(forest.id, Is.EqualTo("Forest"));
            Assert.That(forest.startDistance, Is.EqualTo(0f));
            Assert.That(forest.HasOwnGround, Is.False, "숲은 기본 골짜기 지형");
            Assert.That(forest.treeDensity, Is.EqualTo(1f));
            Assert.That(city.id, Is.EqualTo("City"));
            Assert.That(city.startDistance, Is.EqualTo(385f));
            Assert.That(city.blendDistance, Is.EqualTo(40f));
            Assert.That(city.treeDensity, Is.EqualTo(0f));
            Assert.That(city.ambientColor, Is.EqualTo(new UnityEngine.Color(0.44f, 0.44f, 0.45f)));
            Assert.That(city.fogColor, Is.EqualTo(new UnityEngine.Color(0.58f, 0.56f, 0.52f)));
            Assert.That(city.fogStart, Is.EqualTo(30f));
            Assert.That(city.fogEnd, Is.EqualTo(200f));
            Assert.That(city.roadHalfWidth, Is.EqualTo(7f));
            Assert.That(city.sideWidth, Is.EqualTo(4f));
            Assert.That(city.roadWear, Is.EqualTo(0.45f));
            Assert.That(city.mixAmount, Is.EqualTo(0.55f));
            Assert.That(city.groundBumps, Is.EqualTo(0.15f));
            Assert.That(city.outerRise, Is.EqualTo(9f));
            Assert.That(city.outerRiseStartWidth, Is.EqualTo(new UnityEngine.Vector2(44f, 36f)));
            Assert.That(city.outerRiseStartWidth.x, Is.GreaterThan(city.streetRows[1].frontOffset + 9f + city.streetRows[1].offsetJitter), "둔덕은 뒷줄 건물(깊이 약 9 m) 뒤에서");
            Assert.That(city.boundaryHalfWidth, Is.EqualTo(16f));
            Assert.That(city.clearLaneHalfWidth, Is.EqualTo(2.5f));
            Assert.That(city.layoutSeed, Is.EqualTo(11));
            Assert.That(city.streetRows.Length, Is.EqualTo(2));
            Assert.That(city.streetRows[0].frontOffset, Is.EqualTo(12.5f));
            Assert.That(city.streetRows[1].frontOffset, Is.EqualTo(28f));
            Assert.That(city.props.Length, Is.EqualTo(3));

            // 순서 · 자리: 길 순서, 도시는 마지막 관문(숲 던전 3) 고개 뒤에서 시작, 길 안에서 끝난다
            for (int i = 1; i < tuning.regions.Length; i++)
            {
                Assert.That(tuning.regions[i].startDistance, Is.GreaterThan(tuning.regions[i - 1].startDistance + tuning.regions[i - 1].blendDistance), $"지역 {i} 순서");
                Assert.That(tuning.regions[i].startDistance, Is.LessThan(route.Length - 100f), $"지역 {i}");
            }
            Assert.That(tuning.GateBefore(1), Is.EqualTo(0), "도시 앞 관문 = 관문 1(숲 던전 1 뒤)");
            // 결정 2026-11-25(사용자): 숲 던전 1을 깨면 관문 1이 열려 도시로(결과 화면 없음), 도시 던전을 깨면 다음이 열린다 —
            // 다음 지역이 아직 없어 도시 던전이 마지막 던전이고, 깨면 "폐허 도시 클리어"
            Assert.That(tuning.RegionOpenedBy(0), Is.EqualTo(1));
            Assert.That(tuning.RegionOpenedBy(1), Is.EqualTo(-1), "도시 던전 뒤에는 아직 지역이 없다");
            Assert.That(tuning.LastDungeonOpensRegion, Is.False, "도시 던전을 깨면 판이 끝난다(클리어)");
            Assert.That(tuning.RegionOfDungeon(0), Is.EqualTo(0));
            Assert.That(tuning.RegionOfDungeon(1), Is.EqualTo(1));
            Assert.That(tuning.ClearedRegionName(1), Is.EqualTo("폐허 도시"));
            Assert.That(city.startDistance, Is.GreaterThanOrEqualTo(tuning.GateDistance(0) + tuning.passLength * 0.5f), "고개를 지난 뒤 도시");
            Assert.That(route.Length - city.startDistance, Is.InRange(300f, 360f), "도시 길 약 330 m(결정 2026-11-25: 예전 990 m의 1/3)");

            // 도시 던전 입구: 도시 배치 구간 안 · 관문(입구 + 70 m)은 길 끝 앞, 입구는 보도 밖 앞 줄 건물 자리(경계 벽 안)에서 길을 본다
            WorldTuning.DungeonSite cityDungeon = tuning.dungeons[1];
            Assert.That(cityDungeon.routeDistance, Is.InRange(city.startDistance + city.layoutMargin.x, route.Length - city.layoutMargin.y));
            Assert.That(tuning.GateDistance(1), Is.LessThan(route.Length - 5f), "도시 던전 뒤 관문이 길 안에");
            Assert.That(cityDungeon.sideOffset - tuning.entranceRadius, Is.InRange(city.roadHalfWidth, city.roadHalfWidth + city.sideWidth + 0.5f), "들어서는 자리는 보도에서 닿는다");
            Assert.That(cityDungeon.sideOffset, Is.LessThan(city.boundaryHalfWidth));
            Assert.That(cityDungeon.doorwaySize.x * cityDungeon.doorwaySize.y, Is.GreaterThan(0f), "대피소 문 면(깬 던전은 회색)");

            // 도시 길 위 무리: 관문 1 검문소 · 섞는 거리 뒤(+ 나무 비우는 14 m — 숲 나무 자리가 그대로이게), 입구 앞 20 m 밖, 다니는 띠(길 + 보도) 안
            int cityEncounters = 0;
            for (int i = 0; i < tuning.encounters.Length; i++)
            {
                WorldTuning.EncounterSite site = tuning.encounters[i];
                if (site.routeDistance < city.startDistance) continue;
                cityEncounters++;
                Assert.That(site.routeDistance, Is.GreaterThan(city.startDistance + city.blendDistance + 14f), $"도시 무리 {i}");
                Assert.That(site.routeDistance, Is.LessThan(cityDungeon.routeDistance - 20f), $"도시 무리 {i}는 입구 앞");
                Assert.That(tuning.TreeDensityAt(site.routeDistance), Is.EqualTo(0f), $"도시 무리 {i}");
                Assert.That(System.Math.Abs(site.sideOffset) + tuning.encounterSpread, Is.LessThanOrEqualTo(city.roadHalfWidth + city.sideWidth), $"도시 무리 {i}는 길 · 보도 위");
                Assert.That(tuning.DungeonAhead(i), Is.EqualTo(1), $"도시 무리 {i}는 도시 던전 단계");
            }
            Assert.That(cityEncounters, Is.EqualTo(4));

            // 섞기: 무게 합 1, 숲 → 도시로 고르게, 끝 값은 지역 값 그대로
            var weights = new float[tuning.regions.Length];
            float previous = -1f;
            for (float d = 0f; d <= route.Length; d += 5f)
            {
                tuning.RegionWeights(d, weights);
                Assert.That(weights[0] + weights[1], Is.EqualTo(1f).Within(1e-5f), $"길 {d} m");
                Assert.That(weights[1], Is.GreaterThanOrEqualTo(previous), $"길 {d} m");
                previous = weights[1];
            }
            Assert.That(tuning.AtmosphereAt(city.startDistance - 1f).fogEnd, Is.EqualTo(forest.fogEnd).Within(1e-3f));
            Assert.That(tuning.AtmosphereAt(city.startDistance + city.blendDistance).fogColor, Is.EqualTo(city.fogColor));
            float half = tuning.AtmosphereAt(city.startDistance + city.blendDistance * 0.5f).fogStart;
            Assert.That(half, Is.EqualTo((forest.fogStart + city.fogStart) * 0.5f).Within(1e-3f));
            Assert.That(tuning.TreeDensityAt(300f), Is.EqualTo(1f), "숲 던전 1 둘레 나무는 그대로");
            Assert.That(tuning.TreeDensityAt(city.startDistance + city.blendDistance), Is.EqualTo(0f));
            Assert.That(tuning.RegionAt(200f), Is.EqualTo(0));
            Assert.That(tuning.RegionAt(600f), Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAssetAtPath<VillageTuning>("Assets/Game/Data/VillageTuning.asset").placeInWorld, Is.False, "마을은 나중에 다시 배치(결정 2026-11-25, 사용자)");

            // 길가 지형 레이어 4개 · 프리팹이 있고, 건물은 보도 밖 · 소품은 비운 길 밖 · 경계 벽 안
            Assert.That(city.groundLayers.Length, Is.EqualTo(4));
            foreach (string layer in city.groundLayers) Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.TerrainLayer>(layer), Is.Not.Null, layer);
            foreach (WorldTuning.StreetRow row in city.streetRows)
            {
                Assert.That(row.frontOffset - row.offsetJitter, Is.GreaterThan(city.roadHalfWidth + city.sideWidth), "건물은 보도 밖");
                foreach (WorldTuning.Pick kind in row.kinds) Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"{city.prefabFolder}/{kind.id}.prefab"), Is.Not.Null, kind.id);
            }
            Assert.That(city.streetRows[0].frontOffset - city.streetRows[0].offsetJitter, Is.LessThan(city.boundaryHalfWidth), "앞 줄 건물이 경계 벽에 걸쳐 틈을 막는다");
            foreach (WorldTuning.PropScatter scatter in city.props)
            {
                Assert.That(scatter.side.x, Is.GreaterThan(city.clearLaneHalfWidth), "길 가운데는 비움");
                Assert.That(scatter.side.y, Is.LessThan(city.boundaryHalfWidth));
                foreach (WorldTuning.Pick kind in scatter.kinds) Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"{city.prefabFolder}/{kind.id}.prefab"), Is.Not.Null, kind.id);
            }
            foreach (WorldTuning.PlacedProp piece in city.gateProps) Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"{city.prefabFolder}/{piece.id}.prefab"), Is.Not.Null, piece.id);
            foreach (WorldTuning.PlacedProp piece in city.endProps) Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"{city.prefabFolder}/{piece.id}.prefab"), Is.Not.Null, piece.id);
        }

        /// <summary>지역 섞기 규칙(표 값과 무관): 시작 전 0, 섞는 거리 끝 1, 세 지역이면 뒤 지역이 앞 지역을 덮는다.</summary>
        [Test]
        public void RegionWeights_BlendInOrder()
        {
            var tuning = UnityEngine.ScriptableObject.CreateInstance<WorldTuning>();
            tuning.regions = new[]
            {
                new WorldTuning.Region { id = "A", startDistance = 0f, blendDistance = 1f, fogEnd = 100f, treeDensity = 1f },
                new WorldTuning.Region { id = "B", startDistance = 100f, blendDistance = 20f, fogEnd = 200f, treeDensity = 0f },
                new WorldTuning.Region { id = "C", startDistance = 300f, blendDistance = 40f, fogEnd = 300f, treeDensity = 0.5f },
            };
            var w = new float[3];

            tuning.RegionWeights(50f, w);
            Assert.That(w, Is.EqualTo(new[] { 1f, 0f, 0f }));
            tuning.RegionWeights(110f, w);
            Assert.That(w[0], Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(w[1], Is.EqualTo(0.5f).Within(1e-5f));
            tuning.RegionWeights(200f, w);
            Assert.That(w, Is.EqualTo(new[] { 0f, 1f, 0f }));
            tuning.RegionWeights(500f, w);
            Assert.That(w, Is.EqualTo(new[] { 0f, 0f, 1f }));
            Assert.That(tuning.AtmosphereAt(320f).fogEnd, Is.EqualTo(250f).Within(1e-3f));
            Assert.That(tuning.TreeDensityAt(320f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(tuning.RegionAt(115f), Is.EqualTo(1));
            Assert.That(tuning.RegionAt(105f), Is.EqualTo(0));
            Assert.That(tuning.RegionEnd(1, 1000f), Is.EqualTo(300f));
            Assert.That(tuning.RegionEnd(2, 1000f), Is.EqualTo(1000f));
            UnityEngine.Object.DestroyImmediate(tuning);
        }
    }
}
