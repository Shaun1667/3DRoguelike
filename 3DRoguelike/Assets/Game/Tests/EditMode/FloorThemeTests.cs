using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Game.Rules;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>던전 안 모양 테마(3-8 ⑤-2): 장식 자리 규칙 · 도시 테마 데이터 · 숲 던전이 그대로인지.</summary>
    public class FloorThemeTests
    {
        const string CityThemePath = "Assets/Game/Data/FloorThemes/CityShelter.asset";
        const string ForestThemePath = "Assets/Game/Data/FloorThemes/ForestKennel.asset";

        /// <summary>도시 테마와 같은 모양의 수치(바닥 자리는 도시 소품 크기).</summary>
        static DressingSettings CitySettings()
        {
            var kinds = new[]
            {
                new DressingKind(3f, 1.03f, 30f, DressingPlace.Wall, 0f, 6f),
                new DressingKind(2.25f, 1.9f, 25f, DressingPlace.Wall, 0f, 8f),
                new DressingKind(2.12f, 2.02f, 25f, DressingPlace.Wall, 0f, 8f),
                new DressingKind(1.89f, 4.6f, 12f, DressingPlace.Wall, 90f, 4f),
                new DressingKind(3f, 1.03f, 40f, DressingPlace.Open, 0f, 180f),
                new DressingKind(2.65f, 2.53f, 30f, DressingPlace.Open, 0f, 180f),
                new DressingKind(1.89f, 4.6f, 15f, DressingPlace.Open, 0f, 180f),
                new DressingKind(2.25f, 1.9f, 15f, DressingPlace.Open, 0f, 180f),
            };
            return new DressingSettings(24f, 1f, kinds, 4, 6, 1, 3, 3.2f, 4f, 2f, 4f, 2.5f, 0.4f, 5f);
        }

        static (FloorLayout layout, FloorEncounters encounters, FloorDressing dressing) Plan(uint seed, int floor)
        {
            FloorLayout layout = FloorGeneratorTests.Generate(seed, floor);
            var random = new RunRandom(new RunSeed(seed));
            SpecialRoomPlanner.AssignTreasure(layout, random.Create(RandomPurpose.Decor, floor));
            FloorEncounters encounters = EncounterPlanner.Plan(layout, EncounterPlannerTests.BalanceSettings(), random.Create(RandomPurpose.Enemy, floor));
            FloorDressing dressing = RoomDressingPlanner.Plan(layout, encounters, CitySettings(), random.Create(RandomPurpose.Dressing, floor));
            return (layout, encounters, dressing);
        }

        static string Describe(FloorDressing dressing) => string.Join(" ", dressing.All());

        [Test]
        public void Dressing_SameSeed_SamePlan()
        {
            Assert.That(Describe(Plan(0x12345678u, 1).dressing), Is.EqualTo(Describe(Plan(0x12345678u, 1).dressing)));
            Assert.That(Describe(Plan(0x12345678u, 1).dressing), Is.Not.EqualTo(Describe(Plan(7u, 1).dressing)));
        }

        [Test]
        public void Dressing_KeepsDoorsSpawnsCenterAndLanesClear()
        {
            DressingSettings settings = CitySettings();
            int total = 0, open = 0;
            for (uint seed = 0; seed < 150; seed++)
            {
                for (int floor = 0; floor < 2; floor++)
                {
                    (FloorLayout layout, FloorEncounters encounters, FloorDressing dressing) = Plan(seed, floor);
                    foreach (FloorRoom room in layout.Rooms)
                    {
                        (float cx, float cz) = FloorSpace.RoomCenter(layout, 24f, room);
                        float halfX = room.Width * 12f, halfZ = room.Height * 12f;
                        var doors = new List<(float x, float z)>();
                        foreach (FloorDoor door in layout.Doors)
                            if (door.RoomA == room.Id || door.RoomB == room.Id) doors.Add(FloorSpace.DoorCenter(layout, 24f, door));
                        var boxes = new List<(float x, float z, float ex, float ez)>();
                        foreach (DressingPlacement p in dressing.For(room.Id))
                        {
                            total++;
                            DressingKind kind = settings.Kinds[p.Kind];
                            (float ex, float ez) = RoomDressingPlanner.Extents(kind, p.Yaw);
                            string where = $"seed {seed} floor {floor} room {room.Id} {p}";
                            // 방 안(벽 안쪽 면 안)
                            Assert.That(Math.Abs(p.X - cx) + ex, Is.LessThanOrEqualTo(halfX - 0.5f + 1e-3f), where);
                            Assert.That(Math.Abs(p.Z - cz) + ez, Is.LessThanOrEqualTo(halfZ - 0.5f + 1e-3f), where);
                            foreach ((float x, float z) in doors) Assert.That(Distance(p.X, p.Z, ex, ez, x, z), Is.GreaterThanOrEqualTo(settings.DoorClearance - 1e-3f), "문 " + where);
                            foreach (SpawnPoint s in encounters.SpawnsFor(room.Id)) Assert.That(Distance(p.X, p.Z, ex, ez, s.X, s.Z), Is.GreaterThanOrEqualTo(settings.SpawnClearance - 1e-3f), "적 자리 " + where);
                            foreach (var b in boxes)
                                Assert.That(Math.Abs(p.X - b.x) >= ex + b.ex || Math.Abs(p.Z - b.z) >= ez + b.ez, Is.True, "겹침 " + where);
                            boxes.Add((p.X, p.Z, ex, ez));

                            if (kind.Place == DressingPlace.Wall)
                            {
                                // 벽 띠: 방 경계에서 띠 깊이 안(적 자리 벽 여백 4 m보다 얕음)
                                float depth = Math.Min(Math.Min(halfX - (p.X - cx) + ex, halfX + (p.X - cx) + ex), Math.Min(halfZ - (p.Z - cz) + ez, halfZ + (p.Z - cz) + ez));
                                Assert.That(depth, Is.LessThanOrEqualTo(settings.WallBandDepth + 1e-3f), "벽 띠 " + where);
                                Assert.That(settings.WallBandDepth, Is.LessThan(EncounterPlannerTests.BalanceSettings().WallMargin));
                            }
                            else
                            {
                                open++;
                                Assert.That(room.Kind == RoomKind.Battle || room.Kind == RoomKind.Boss, Is.True, "엄폐물은 전투 · 보스 방만 " + where);
                                Assert.That(Distance(p.X, p.Z, ex, ez, cx, cz), Is.GreaterThanOrEqualTo(settings.CenterClearance - 1e-3f), "가운데 " + where);
                                foreach ((float x, float z) in doors)
                                    for (float t = 0f; t <= 1f; t += 0.02f)
                                        Assert.That(Distance(p.X, p.Z, ex, ez, x + (cx - x) * t, z + (cz - z) * t), Is.GreaterThanOrEqualTo(settings.LaneHalfWidth - 0.06f), "문 → 가운데 길 " + where);
                            }
                        }
                    }
                }
            }
            Assert.That(total, Is.GreaterThan(300 * 20), "층마다 장식이 충분히 놓임");
            Assert.That(open, Is.GreaterThan(300), "엄폐물도 놓임");
        }

        [Test]
        public void Dressing_DoesNotTouchMapEnemyOrRewardRandom()
        {
            // 꾸미기 난수 키가 다른 용도와 겹치지 않음 → 테마가 있어도 같은 시드면 맵 · 적 · 보상이 같다
            var keys = new HashSet<string> { RandomPurpose.Map.Key, RandomPurpose.Decor.Key, RandomPurpose.Reward.Key, RandomPurpose.Enemy.Key, RandomPurpose.Combat.Key, RandomPurpose.Effects.Key, RandomPurpose.Road.Key };
            Assert.That(keys.Contains(RandomPurpose.Dressing.Key), Is.False);
            Assert.That(RandomPurpose.Dressing.Key, Is.EqualTo("dressing"));
        }

        [Test]
        public void Dressing_RejectsBadSettings()
        {
            var kinds = new[] { new DressingKind(1f, 1f, 1f, DressingPlace.Wall, 0f, 0f) };
            Assert.Throws<ArgumentOutOfRangeException>(() => new DressingSettings(24f, 1f, kinds, 3, 2, 0, 0, 3f, 4f, 2f, 4f, 2.5f, 0.4f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DressingSettings(24f, 1f, kinds, 0, 1, 0, 1, 0.4f, 4f, 2f, 4f, 2.5f, 0.4f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DressingKind(0f, 1f, 1f, DressingPlace.Wall, 0f, 0f));
        }

        [Test]
        public void CityTheme_MatchesBalance()
        {
            var world = AssetDatabase.LoadAssetAtPath<WorldTuning>("Assets/Game/Data/WorldTuning.asset");
            var theme = AssetDatabase.LoadAssetAtPath<FloorTheme>(CityThemePath);
            Assert.That(theme, Is.Not.Null);
            Assert.That(world.dungeons[0].interiorTheme, Is.SameAs(AssetDatabase.LoadAssetAtPath<FloorTheme>(ForestThemePath)), "숲 던전 1 = 무너진 사육장 테마(⑥)");
            Assert.That(world.dungeons[1].interiorTheme, Is.SameAs(theme), "도시 던전 = 무너진 대피소 테마");

            Assert.That(theme.floor.mainTexture.name, Is.EqualTo("City_Asphalt"));
            Assert.That(theme.startFloor.mainTexture.name, Is.EqualTo("City_WeedyPavement"));
            Assert.That(theme.bossFloor.mainTexture.name, Is.EqualTo("City_Asphalt"));
            Assert.That(theme.treasureFloor.mainTexture.name, Is.EqualTo("City_RubbleConcrete"));
            Assert.That(theme.wall.mainTexture.name, Is.EqualTo("City_RubbleConcrete"));
            Assert.That(theme.floorTile, Is.EqualTo(8f));
            Assert.That(theme.wallTile, Is.EqualTo(6f));
            Assert.That(theme.fog, Is.True);
            Assert.That(theme.fogStart, Is.EqualTo(14f));
            Assert.That(theme.fogEnd, Is.EqualTo(70f));
            Assert.That(theme.sunIntensity, Is.EqualTo(0.8f));

            Assert.That(theme.props.Length, Is.EqualTo(8));
            foreach (FloorTheme.Prop prop in theme.props)
            {
                Assert.That(prop.prefab, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(prop.prefab), Does.StartWith("Assets/Game/Prefabs/World/City/"), "이미 있는 도시 소품만");
                Assert.That(prop.prefab.GetComponent<BoxCollider>(), Is.Not.Null, prop.prefab.name);
            }
            Assert.That(theme.wallPropsPerCell, Is.EqualTo(new Vector2Int(4, 6)));
            Assert.That(theme.openPropsPerCell, Is.EqualTo(new Vector2Int(1, 3)));
            var floor = AssetDatabase.LoadAssetAtPath<FloorTuning>("Assets/Game/Data/FloorTuning.asset");
            Assert.That(theme.wallBandDepth, Is.LessThan(floor.spawnWallMargin), "벽 장식은 적 자리보다 벽 쪽");
            Assert.That(theme.doorClearance, Is.GreaterThan(floor.doorWidth * 0.5f));

            // 데이터 그대로 수치가 만들어지고, 벽 장식은 모두 띠 깊이 안에 들어갈 수 있다
            DressingSettings settings = theme.ToDressingSettings(floor);
            for (int i = 0; i < settings.Kinds.Count; i++)
            {
                DressingKind kind = settings.Kinds[i];
                Assert.That(kind.Weight, Is.GreaterThan(0f), theme.props[i].prefab.name);
                if (kind.Place != DressingPlace.Wall) continue;
                (float ex, float ez) = RoomDressingPlanner.Extents(kind, kind.YawOffset);
                Assert.That(floor.wallThickness * 0.5f + settings.Gap + 2f * ez, Is.LessThanOrEqualTo(settings.WallBandDepth), theme.props[i].prefab.name);
            }
        }

        /// <summary>숲 던전 1 "무너진 사육장" 테마 값(BALANCE 8-1). 이미 있는 숲 · 도시 에셋과 단순 도형 소품만, 벽 장식은 띠 안에 들어감.</summary>
        [Test]
        public void ForestTheme_MatchesBalance()
        {
            var theme = AssetDatabase.LoadAssetAtPath<FloorTheme>(ForestThemePath);
            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.displayName, Is.EqualTo("무너진 사육장"));
            Assert.That(theme.floor.mainTexture.name, Is.EqualTo("Ground_ForestFloor"));
            Assert.That(theme.startFloor.mainTexture.name, Is.EqualTo("Ground_DirtTrail"));
            Assert.That(theme.bossFloor.mainTexture.name, Is.EqualTo("Ground_DirtTrail"));
            Assert.That(theme.treasureFloor.mainTexture.name, Is.EqualTo("Ground_ForestFloor"));
            Assert.That(theme.wall.mainTexture.name, Is.EqualTo("City_RubbleConcrete"));
            // 시작 초록 · 보스 빨강 · 보물 노랑(회색 층과 같은 뜻)
            Color start = theme.startFloor.GetColor("_BaseColor"), boss = theme.bossFloor.GetColor("_BaseColor"), treasure = theme.treasureFloor.GetColor("_BaseColor");
            Assert.That(start.g, Is.GreaterThan(start.r));
            Assert.That(boss.r, Is.GreaterThan(boss.g));
            Assert.That(treasure.r, Is.GreaterThan(treasure.b));
            Assert.That(theme.floorTile, Is.EqualTo(6f));
            Assert.That(theme.wallTile, Is.EqualTo(6f));
            Assert.That(theme.fog, Is.True);
            Assert.That(theme.fogStart, Is.EqualTo(20f));
            Assert.That(theme.fogEnd, Is.EqualTo(90f));
            Assert.That(theme.sunIntensity, Is.EqualTo(1.05f));

            Assert.That(theme.props.Length, Is.EqualTo(13));
            foreach (FloorTheme.Prop prop in theme.props)
            {
                Assert.That(prop.prefab, Is.Not.Null);
                string path = AssetDatabase.GetAssetPath(prop.prefab);
                Assert.That(path.StartsWith("Assets/Game/Prefabs/World/Forest/") || path.StartsWith("Assets/Game/Prefabs/World/City/"), Is.True, path);
                Assert.That(prop.prefab.GetComponent<BoxCollider>(), Is.Not.Null, prop.prefab.name);
            }
            var floor = AssetDatabase.LoadAssetAtPath<FloorTuning>("Assets/Game/Data/FloorTuning.asset");
            Assert.That(theme.wallBandDepth, Is.LessThan(floor.spawnWallMargin), "벽 장식은 적 자리보다 벽 쪽");
            Assert.That(theme.doorClearance, Is.GreaterThan(floor.doorWidth * 0.5f));

            DressingSettings settings = theme.ToDressingSettings(floor);
            for (int i = 0; i < settings.Kinds.Count; i++)
            {
                DressingKind kind = settings.Kinds[i];
                Assert.That(kind.Weight, Is.GreaterThan(0f), theme.props[i].prefab.name);
                if (kind.Place == DressingPlace.Open)
                {
                    // 방 안 엄폐물은 낮게(곰 · 적이 가려지지 않게)
                    Assert.That(theme.props[i].prefab.GetComponent<BoxCollider>().size.y * theme.props[i].scale, Is.LessThanOrEqualTo(1.6f), theme.props[i].prefab.name);
                    continue;
                }
                (float ex, float ez) = RoomDressingPlanner.Extents(kind, kind.YawOffset);
                Assert.That(floor.wallThickness * 0.5f + settings.Gap + 2f * ez, Is.LessThanOrEqualTo(settings.WallBandDepth), theme.props[i].prefab.name);
            }
        }

        /// <summary>숲 테마로도 장식이 문 · 적 자리 · 가운데 · 문 → 가운데 길을 비운다(실제 테마 에셋 값, 던전 0).</summary>
        [Test]
        public void ForestTheme_Dressing_KeepsDoorsSpawnsCenterAndLanesClear()
        {
            var floor = AssetDatabase.LoadAssetAtPath<FloorTuning>("Assets/Game/Data/FloorTuning.asset");
            var roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>("Assets/Game/Data/EnemyRoster.asset");
            var theme = AssetDatabase.LoadAssetAtPath<FloorTheme>(ForestThemePath);
            DressingSettings settings = theme.ToDressingSettings(floor);
            float cell = floor.cellSize;
            int total = 0;
            for (uint seed = 0; seed < 60; seed++)
            {
                var random = new RunRandom(new RunSeed(seed * 7919u + 3u));
                FloorLayout layout = FloorGenerator.Generate(floor.ToSettings(), random.Create(RandomPurpose.Map, 0));
                SpecialRoomPlanner.AssignTreasure(layout, random.Create(RandomPurpose.Decor, 0));
                FloorEncounters encounters = EncounterPlanner.Plan(layout, floor.ToEncounterSettings(), roster.ToMix(), 0, random.Create(RandomPurpose.Enemy, 0));
                FloorDressing dressing = RoomDressingPlanner.Plan(layout, encounters, settings, random.Create(RandomPurpose.Dressing, 0));
                foreach (FloorRoom room in layout.Rooms)
                {
                    (float cx, float cz) = FloorSpace.RoomCenter(layout, cell, room);
                    var doors = new List<(float x, float z)>();
                    foreach (FloorDoor door in layout.Doors)
                        if (door.RoomA == room.Id || door.RoomB == room.Id) doors.Add(FloorSpace.DoorCenter(layout, cell, door));
                    foreach (DressingPlacement p in dressing.For(room.Id))
                    {
                        total++;
                        DressingKind kind = settings.Kinds[p.Kind];
                        (float ex, float ez) = RoomDressingPlanner.Extents(kind, p.Yaw);
                        string where = $"seed {seed} room {room.Id} {p}";
                        foreach ((float x, float z) in doors) Assert.That(Distance(p.X, p.Z, ex, ez, x, z), Is.GreaterThanOrEqualTo(settings.DoorClearance - 1e-3f), "문 " + where);
                        foreach (SpawnPoint s in encounters.SpawnsFor(room.Id)) Assert.That(Distance(p.X, p.Z, ex, ez, s.X, s.Z), Is.GreaterThanOrEqualTo(settings.SpawnClearance - 1e-3f), "적 자리 " + where);
                        if (kind.Place != DressingPlace.Open) continue;
                        Assert.That(Distance(p.X, p.Z, ex, ez, cx, cz), Is.GreaterThanOrEqualTo(settings.CenterClearance - 1e-3f), "가운데 " + where);
                        foreach ((float x, float z) in doors)
                            for (float t = 0f; t <= 1f; t += 0.02f)
                                Assert.That(Distance(p.X, p.Z, ex, ez, x + (cx - x) * t, z + (cz - z) * t), Is.GreaterThanOrEqualTo(settings.LaneHalfWidth - 0.06f), "문 → 가운데 길 " + where);
                    }
                }
            }
            Assert.That(total, Is.GreaterThan(60 * 40), "층마다 장식이 충분히 놓임");
        }


        /// <summary>
        /// 숲 던전 1의 층 도형(방 · 벽 · 문 자리) · 적 자리가 테마를 넣기 전과 같다. 기대값은 3-8 ⑤-2 전 코드로 플레이에서 잰 층과 같은 결과를 낸 값.
        /// 회색 기본 재질(`Art/Floor`)로 세우면 속성 블록이 없어야 한다(테마 없는 회색 층 그대로).
        /// ⑥에서 숲 던전 1에 무너진 사육장 테마를 붙였지만 테마는 재질 · 장식 · 분위기만 바꾸므로(맵 · 적 난수 그대로) 이 다이제스트도 그대로다.
        /// </summary>
        [Test]
        public void ForestDungeon_GeometryAndEnemiesUnchanged()
        {
            Assert.That(ForestDigest(0x5EEDu), Is.EqualTo(ExpectedForestDigest));
        }

        internal const string ExpectedForestDigest = "58D7A1290EEBFD6D";

        internal static string ForestDigest(uint seed)
        {
            var floor = AssetDatabase.LoadAssetAtPath<FloorTuning>("Assets/Game/Data/FloorTuning.asset");
            var roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>("Assets/Game/Data/EnemyRoster.asset");
            var random = new RunRandom(new RunSeed(seed));
            FloorLayout layout = FloorGenerator.Generate(floor.ToSettings(), random.Create(RandomPurpose.Map, 0));
            int treasure = SpecialRoomPlanner.AssignTreasure(layout, random.Create(RandomPurpose.Decor, 0));
            FloorEncounters encounters = EncounterPlanner.Plan(layout, floor.ToEncounterSettings(), roster.ToMix(), 0, random.Create(RandomPurpose.Enemy, 0));

            const string folder = "Assets/Game/Art/Floor/";
            var materials = new FloorGeometry.Materials
            {
                floor = AssetDatabase.LoadAssetAtPath<Material>(folder + "Floor.mat"),
                startFloor = AssetDatabase.LoadAssetAtPath<Material>(folder + "StartFloor.mat"),
                bossFloor = AssetDatabase.LoadAssetAtPath<Material>(folder + "BossFloor.mat"),
                wall = AssetDatabase.LoadAssetAtPath<Material>(folder + "Wall.mat"),
                lockedDoor = AssetDatabase.LoadAssetAtPath<Material>(folder + "LockedDoor.mat"),
                treasureFloor = AssetDatabase.LoadAssetAtPath<Material>(folder + "TreasureFloor.mat"),
            };
            var root = new GameObject("ForestDigest");
            try
            {
                FloorGeometry.Build(layout, floor, materials, root.transform);
                var text = new StringBuilder(layout.ToAscii()).Append(treasure).Append('\n');
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    text.Append(t.name).Append('|').Append(t.localPosition.ToString("F3")).Append(t.localScale.ToString("F3")).Append(t.gameObject.activeSelf);
                    var renderer = t.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        text.Append(renderer.sharedMaterial.name).Append(renderer.HasPropertyBlock());
                    }
                    text.Append('\n');
                }
                for (int i = 0; i < layout.Rooms.Count; i++)
                    foreach (SpawnPoint s in encounters.SpawnsFor(i)) text.Append(i).Append(s.X.ToString("F4")).Append(s.Z.ToString("F4")).Append(s.Kind.Species).Append(s.Kind.Grade).Append('\n');
                using (var md5 = MD5.Create())
                    return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").Substring(0, 16);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static float Distance(float x, float z, float ex, float ez, float px, float pz)
        {
            float dx = Math.Max(0f, Math.Abs(px - x) - ex);
            float dz = Math.Max(0f, Math.Abs(pz - z) - ez);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
