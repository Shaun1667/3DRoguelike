using System.Collections.Generic;
using Game.Rules;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 월드 씬을 만든다(메뉴 Tools/Game/Setup World). 월드는 고정이라 지형 타일(지역마다 하나) · 흙길 · 나무 · 던전 입구 · 관문 · 적 무리 자리 ·
    /// 지역 배치(길가 건물 · 소품 · 검문소 · 경계 벽)를 <see cref="WorldTuning"/> 값으로 씬에 세우고 NavMesh도 여기서 굽는다.
    /// 여러 번 실행해도 결과가 같다(나무 · 배치도 고정 시드). 수치 에셋은 없을 때만 만들고 값을 덮어쓰지 않는다.
    /// 새 지역 = WorldTuning.regions 한 줄(+ 길 점) — 종류별 코드 없음.
    /// </summary>
    public static class WorldSetup
    {
        private const string TuningPath = "Assets/Game/Data/WorldTuning.asset";
        private const string ArtFolder = "Assets/Game/Art/World";
        private const string TerrainDataPath = ArtFolder + "/WorldTerrain.asset";
        private const string GroundFolder = ArtFolder + "/Ground";
        private const string SkyPath = ArtFolder + "/WorldSky.mat";
        private static readonly Color RockColor = new Color(0.42f, 0.41f, 0.39f);
        private const string NavMeshPath = ArtFolder + "/WorldNavMesh.asset";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Player.prefab";
        private const float SiteFlatRadius = 14f;

        [MenuItem("Tools/Game/Setup World")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null || AssetDatabase.LoadAssetAtPath<EnemyRoster>(EnemySetup.RosterPath) == null)
            {
                TestRoomSetup.Run(); // 플레이어 프리팹 · 숲 적 4종 · 구성 표를 만든다
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CurrentRun.FloorScene) == null) FloorSetup.Run(); // 던전(층) 씬
            if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Game/Art", "World");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 새 씬을 연 뒤에 불러온다(FloorSetup과 같은 이유: 앞에서 만든 에셋 참조가 끊기지 않게)
            WorldTuning tuning = LoadOrCreateTuning();
            WorldRoute route = tuning.ToRoute();
            VillageTuning village = VillageSetup.LoadOrCreateTuning();
            // 마을은 VillageTuning.placeInWorld가 켜져 있을 때만 월드에 둔다(꺼져 있으면 지형 · 나무 · NavMesh에 마을 자리가 없다)
            VillageSetup.Area villageArea = village.placeInWorld ? VillageSetup.AreaOf(village, route) : null;
            if (HasRegionLayouts(tuning)) CitySetup.Run(); // 도시 소품 프리팹 · 바닥 레이어(TreeSetup처럼 매번 같은 결과)
            TestRoomSetup.BuildLighting();
            BuildAtmosphere(tuning); // BuildLighting의 앰비언트를 월드용으로 덮어쓴다(시험 방은 그대로)

            var root = new GameObject("World");
            Ground ground = BuildTerrains(tuning, route, villageArea, root.transform);
            Terrain terrain = ground.Tiles[0]; // 숲(첫 지역) 타일 — 마을 · 입구가 여기에 선다
            var sites = new List<Vector3>();

            Transform[] entrances = new Transform[tuning.dungeons.Length];
            Renderer[] doorways = new Renderer[tuning.dungeons.Length];
            TMP_FontAsset font = HudSetup.LoadOrCreateFontAsset();
            for (int i = 0; i < tuning.dungeons.Length; i++)
            {
                entrances[i] = BuildEntrance(tuning, route, ground, root.transform, i, font, out doorways[i]);
                sites.Add(entrances[i].position);
            }

            Transform start = new GameObject("StartPoint").transform;
            start.SetParent(root.transform, false);
            start.position = OnGround(ground, route.PointAt(6f));
            start.rotation = Quaternion.LookRotation(Dir(route.DirectionAt(6f)));
            sites.Add(start.position);

            Transform[] encounterSites = BuildEncounterSites(tuning, route, ground, root.transform);
            foreach (Transform site in encounterSites) sites.Add(site.position);

            BuildTrees(tuning, route, ground, root.transform, sites, entrances, villageArea);
            if (villageArea != null) VillageSetup.Build(village, villageArea, tuning, route, terrain, root.transform, font); // 마을 집 · 울타리 · 주민도 NavMesh에 구멍을 낸다
            int regionProps = BuildRegionLayouts(tuning, route, ground, root.transform);

            // 관문을 세우기 전에 굽는다 — 관문이 열리면 지나갈 길이 이미 NavMesh에 있게
            BakeNavMesh(root);
            GameObject[] gates = BuildGates(tuning, route, ground, font);
            BuildBounds(ground, root.transform);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            player.transform.SetPositionAndRotation(start.position + Vector3.up * 0.1f, start.rotation);
            Canvas hud = TestRoomSetup.BuildCamera(player);
            var vitals = player.GetComponent<PlayerVitals>();
            var vitalsFields = new SerializedObject(vitals);
            vitalsFields.FindProperty("restartOnR").boolValue = false; // 다시 시작은 결과 화면이 맡는다
            vitalsFields.ApplyModifiedPropertiesWithoutUndo();

            HudSetup.BuildHud(hud, vitals, floor: null);
            RunResultScreen resultScreen = HudSetup.BuildResultScreen(hud);
            TMP_Text objective = HudSetup.BuildObjective(hud);

            var run = new GameObject("WorldRun").AddComponent<WorldRun>();
            var fields = new SerializedObject(run);
            fields.FindProperty("tuning").objectReferenceValue = tuning;
            fields.FindProperty("player").objectReferenceValue = vitals;
            fields.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyRoster>(EnemySetup.RosterPath);
            fields.FindProperty("resultScreen").objectReferenceValue = resultScreen;
            fields.FindProperty("objective").objectReferenceValue = objective;
            fields.FindProperty("startPoint").objectReferenceValue = start;
            fields.FindProperty("clearedDoorwayMaterial").objectReferenceValue = Lit("DoorwayCleared", new Color(0.5f, 0.5f, 0.5f));
            SetArray(fields.FindProperty("entrances"), entrances);
            SetArray(fields.FindProperty("doorways"), doorways);
            SetArray(fields.FindProperty("gates"), gates);
            SetArray(fields.FindProperty("encounterSites"), encounterSites);
            SetArray(fields.FindProperty("terrains"), ground.Tiles);
            fields.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, CurrentRun.WorldScene);
            BuildSceneList.Ensure(CurrentRun.WorldScene, first: true);
            BuildSceneList.Ensure(CurrentRun.FloorScene, first: false);
            AssetDatabase.SaveAssets();
            var tiles = new System.Text.StringBuilder();
            foreach (Terrain tile in ground.Tiles) tiles.Append($" · {tile.name} {tile.terrainData.size.x:0} × {tile.terrainData.size.z:0} m");
            Debug.Log($"[WorldSetup] 월드 씬 생성: {CurrentRun.WorldScene} · 길 {route.Length:0} m · 지역 {tuning.regions.Length}개 · 던전 {tuning.dungeons.Length}개 · 지역 배치 프리팹 {regionProps}개{tiles}");
        }

        public static WorldTuning LoadOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<WorldTuning>(TuningPath);
            if (tuning == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<WorldTuning>(), TuningPath);
                AssetDatabase.SaveAssets();
                tuning = AssetDatabase.LoadAssetAtPath<WorldTuning>(TuningPath);
            }
            // 이어 하기(3-6)가 고른 보상을 찾는 표 — 던전(FloorRun)과 같은 에셋
            if (tuning.rewardTable == null) tuning.rewardTable = AssetDatabase.LoadAssetAtPath<RewardTable>(RewardTablePath);
            // 판 밖 성장 트리 · 영구 재화 표(3-7) — 없으면 기본 표로 만든다(있으면 에셋 값이 기준)
            if (tuning.metaTree == null) tuning.metaTree = LoadOrCreateMetaTree();
            EditorUtility.SetDirty(tuning); // 새로 생긴 필드를 파일에 남긴다
            return tuning;
        }

        private const string RewardTablePath = "Assets/Game/Data/RewardTable.asset";
        public const string MetaTreePath = "Assets/Game/Data/MetaTree.asset";

        public static MetaTreeTable LoadOrCreateMetaTree()
        {
            var table = AssetDatabase.LoadAssetAtPath<MetaTreeTable>(MetaTreePath);
            if (table != null) return table;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MetaTreeTable>(), MetaTreePath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<MetaTreeTable>(MetaTreePath);
        }

        /// <summary>씬 기본 분위기 = 첫 지역(길 시작). 플레이 중에는 WorldRun이 길 거리로 지역 값을 섞는다.</summary>
        private static void BuildAtmosphere(WorldTuning tuning)
        {
            WorldTuning.Atmosphere air = tuning.AtmosphereAt(0f);
            RenderSettings.skybox = WorldSky(tuning.skyExposure);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = air.ambientColor;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = air.fogColor;
            RenderSettings.fogStartDistance = air.fogStart;
            RenderSettings.fogEndDistance = air.fogEnd;
        }

        private static bool HasRegionLayouts(WorldTuning tuning)
        {
            foreach (WorldTuning.Region region in tuning.regions)
                if (region.HasOwnGround || (region.streetRows != null && region.streetRows.Length > 0)) return true;
            return false;
        }

        /// <summary>기본 Procedural 하늘을 복사한 월드 하늘. 매번 기본값에서 다시 맞춰 여러 번 실행해도 같다.</summary>
        private static Material WorldSky(float exposure)
        {
            var source = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null)
            {
                sky = new Material(source) { name = "WorldSky" };
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            else
            {
                sky.shader = source.shader;
                sky.CopyPropertiesFromMaterial(source);
            }
            sky.SetFloat("_Exposure", exposure);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        // ---------- 지형 ----------

        /// <summary>
        /// 지형 타일(지역마다 하나, 남 → 북). 모든 타일이 x 범위 · 높이 격자 · 높이(m)를 같이 쓰고, 지역 i 타일은 지역 i 시작 자리에서
        /// 다음 지역 시작 자리까지(z). 높이는 한 함수(<see cref="HeightAt"/>)라 이음매가 맞는다. 타일 이웃은 WorldRun이 플레이 때 잇는다.
        /// </summary>
        internal sealed class Ground
        {
            public Terrain[] Tiles;

            public Terrain At(float z)
            {
                for (int i = Tiles.Length - 1; i > 0; i--)
                    if (z >= Tiles[i].transform.position.z) return Tiles[i];
                return Tiles[0];
            }

            public float HeightAt(float x, float z)
            {
                Terrain tile = At(z);
                return tile.SampleHeight(new Vector3(x, 0f, z)) + tile.transform.position.y;
            }

            /// <summary>모든 타일을 덮는 x · z 범위.</summary>
            public Rect Area
            {
                get
                {
                    Vector3 min = Tiles[0].transform.position;
                    Terrain last = Tiles[Tiles.Length - 1];
                    Vector3 max = last.transform.position + last.terrainData.size;
                    return Rect.MinMaxRect(min.x, min.z, max.x, max.z);
                }
            }
        }

        /// <summary>길 전체를 둘러싼 범위(골짜기 + 절벽 + 여백). 나무 자리는 숲 길로 잰 범위를 따로 쓴다.</summary>
        private static Rect RouteArea(WorldTuning tuning, WorldRoute route)
        {
            float reach = tuning.valleyHalfWidth + tuning.cliffWidth + tuning.terrainMargin;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < route.PointCount; i++)
            {
                (float x, float z) = route.Point(i);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
            }
            return Rect.MinMaxRect(minX - reach, minZ - reach, maxX + reach, maxZ + reach);
        }

        private static WorldRoute RouteOf(Vector2[] points)
        {
            var list = new (float x, float z)[points.Length];
            for (int i = 0; i < points.Length; i++) list[i] = (points[i].x, points[i].y);
            return new WorldRoute(list);
        }

        private static Ground BuildTerrains(WorldTuning tuning, WorldRoute route, VillageSetup.Area village, Transform parent)
        {
            Rect area = RouteArea(tuning, route);
            float height = tuning.cliffHeight + tuning.groundBumps + 1f;
            var tiles = new Terrain[tuning.regions.Length];
            float tileStart = area.yMin;
            for (int i = 0; i < tiles.Length; i++)
            {
                float tileEnd = i + 1 < tiles.Length ? route.PointAt(tuning.regions[i + 1].startDistance).z : area.yMax;
                tiles[i] = BuildTile(tuning, route, village, parent, i, area.xMin, tileStart, area.width, tileEnd - tileStart, height);
                tileStart = tileEnd;
            }
            return new Ground { Tiles = tiles };
        }

        private static string TerrainDataPathOf(WorldTuning tuning, int region) =>
            region == 0 ? TerrainDataPath : $"{ArtFolder}/WorldTerrain_{tuning.regions[region].id}.asset";

        private static Terrain BuildTile(WorldTuning tuning, WorldRoute route, VillageSetup.Area village, Transform parent, int region,
            float minX, float minZ, float width, float length, float height)
        {
            WorldTuning.Region data = tuning.regions[region];
            string path = TerrainDataPathOf(tuning, region);
            // 에셋을 먼저 만든 뒤 높이 · 레이어 · 칠하기를 넣는다 — 메모리에서 칠한 뒤 CreateAsset하면 칠한 값(SplatAlpha)이 파일에 남지 않았다
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(path) != null) AssetDatabase.DeleteAsset(path);
            var terrainData = new TerrainData { heightmapResolution = tuning.heightmapResolution };
            terrainData.size = new Vector3(width, height, length);
            AssetDatabase.CreateAsset(terrainData, path);

            if (data.HasOwnGround)
            {
                // 지역 표의 레이어 4개(길 · 길 옆 · 바깥 바탕 · 섞는 것)
                var layers = new TerrainLayer[data.groundLayers.Length];
                for (int k = 0; k < layers.Length; k++)
                {
                    layers[k] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(data.groundLayers[k]);
                    if (layers[k] == null) Debug.LogError($"[WorldSetup] {data.displayName}: 지형 레이어가 없습니다: {data.groundLayers[k]}");
                }
                terrainData.terrainLayers = layers;
                terrainData.alphamapResolution = Mathf.Max(16, data.alphamapResolution);
            }
            else
            {
                // 레이어 순서 = alphamap 채널: 0 숲 바닥 · 1 흙길 · 2 절벽 바위 · 3 오염 토양(URP Terrain/Lit 한 패스 4층)
                terrainData.terrainLayers = new[]
                {
                    Layer("ForestFloor", GroundTexture("Ground_ForestFloor"), tuning.forestFloorTile, tuning.groundSmoothness),
                    Layer("DirtTrail", GroundTexture("Ground_DirtTrail"), tuning.dirtTrailTile, tuning.trailSmoothness),
                    Layer("Rock", SolidTexture("Rock", RockColor), 4f, tuning.groundSmoothness),
                    Layer("PollutedSoil", GroundTexture("Ground_PollutedSoil"), tuning.pollutedSoilTile, tuning.groundSmoothness),
                };
                terrainData.alphamapResolution = 512;
            }
            FillTerrain(tuning, route, village, terrainData, minX, minZ, width, length, height, region);
            EditorUtility.SetDirty(terrainData);
            AssetDatabase.SaveAssetIfDirty(terrainData);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = region == 0 ? "Terrain" : $"Terrain_{data.id}";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = new Vector3(minX, 0f, minZ);
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.materialTemplate = TerrainMaterial();
            return terrain;
        }

        /// <summary>
        /// 높이 · 바닥 칠하기. Setup World가 새 타일에, Setup Village가 열린 숲 타일에 부른다(같은 값이면 같은 결과).
        /// width · length · height는 타일 크기(m), minX · minZ는 타일 원점. region은 이 타일의 지역(바닥 칠하기 방식).
        /// </summary>
        internal static void FillTerrain(WorldTuning tuning, WorldRoute route, VillageSetup.Area village, TerrainData data, float minX, float minZ, float width, float length, float height, int region = 0)
        {
            var flatSites = new List<Vector2>();
            foreach (WorldTuning.DungeonSite site in tuning.dungeons)
            {
                (float x, float z) = route.BesideAt(site.routeDistance, site.sideOffset);
                flatSites.Add(new Vector2(x, z));
            }
            var weights = new float[tuning.regions.Length];

            int res = data.heightmapResolution;
            var heights = new float[res, res];
            for (int zi = 0; zi < res; zi++)
            {
                float z = minZ + length * zi / (res - 1);
                for (int xi = 0; xi < res; xi++)
                {
                    float x = minX + width * xi / (res - 1);
                    heights[zi, xi] = HeightAt(tuning, route, flatSites, village, weights, x, z) / height;
                }
            }
            data.SetHeights(0, 0, heights);

            WorldTuning.Region own = tuning.regions[region];
            int alphaRes = data.alphamapResolution;
            var alpha = new float[alphaRes, alphaRes, 4];
            for (int zi = 0; zi < alphaRes; zi++)
            {
                float z = minZ + length * (zi + 0.5f) / alphaRes;
                for (int xi = 0; xi < alphaRes; xi++)
                {
                    float x = minX + width * (xi + 0.5f) / alphaRes;
                    float c0, c1, c2, c3;
                    if (own.HasOwnGround) StreetWeights(own, route, x, z, out c0, out c1, out c2, out c3);
                    else GroundWeights(tuning, route, flatSites, village, x, z, out c0, out c1, out c2, out c3);
                    alpha[zi, xi, 0] = c0;
                    alpha[zi, xi, 1] = c1;
                    alpha[zi, xi, 2] = c2;
                    alpha[zi, xi, 3] = c3;
                }
            }
            data.SetAlphamaps(0, 0, alpha);
        }

        /// <summary>
        /// 땅 높이(m). 지역마다 높이를 구해 길 거리 무게(<see cref="WorldTuning.RegionWeights"/>)로 섞는다 —
        /// 기본 지역(숲)은 골짜기 + 절벽, 길가 지역은 거의 평평. 그래서 관문 고개를 지나면 절벽이 낮아지며 도시 바닥이 된다.
        /// </summary>
        private static float HeightAt(WorldTuning tuning, WorldRoute route, List<Vector2> flatSites, VillageSetup.Area village, float[] weights, float x, float z)
        {
            float d = route.DistanceFrom(x, z, out float along);
            tuning.RegionWeights(along, weights);
            float result = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f) continue;
                WorldTuning.Region region = tuning.regions[i];
                float h = region.HasOwnGround ? StreetHeight(region, d, x, z) : ValleyHeight(tuning, flatSites, village, d, along, x, z);
                result += h * weights[i];
            }
            return result;
        }

        private static float ValleyHeight(WorldTuning tuning, List<Vector2> flatSites, VillageSetup.Area village, float d, float along, float x, float z)
        {
            float half = tuning.ValleyHalfWidthAt(along);
            float cliff = Smooth01((d - half) / tuning.cliffWidth) * tuning.cliffHeight;

            float nearSite = float.MaxValue;
            foreach (Vector2 site in flatSites) nearSite = Mathf.Min(nearSite, Vector2.Distance(site, new Vector2(x, z)));
            // 길 · 입구 근처는 평평하게, 골짜기 안쪽만 완만한 굴곡
            float weight = Smooth01((d - tuning.pathHalfWidth - 2f) / 12f) * Smooth01((nearSite - SiteFlatRadius) / 10f);
            if (village != null) weight *= village.FlatWeight(x, z); // 마을 안은 평지
            float bumps = Mathf.PerlinNoise(x * tuning.bumpScale + 100f, z * tuning.bumpScale + 100f) * tuning.groundBumps * weight;
            return cliff + bumps;
        }

        /// <summary>길가 지역 높이: 길 · 길 옆 띠는 평평, 그 밖은 아주 작은 굴곡(groundBumps).</summary>
        private static float StreetHeight(WorldTuning.Region region, float d, float x, float z)
        {
            float outside = Smooth01((d - region.roadHalfWidth - region.sideWidth - 1f) / 4f);
            float bumps = Mathf.PerlinNoise(x * 0.05f + 900f, z * 0.05f + 900f) * region.groundBumps * outside;
            if (region.outerRise <= 0f) return bumps;
            // 다니는 띠 바깥 잔해 둔덕: 높이를 노이즈로 0.6~1.4배 흔들어 무너진 더미처럼
            float rise = Smooth01((d - region.outerRiseStartWidth.x) / Mathf.Max(1f, region.outerRiseStartWidth.y));
            float heap = 0.6f + 0.8f * Mathf.PerlinNoise(x * 0.03f + 1700f, z * 0.03f + 1700f);
            return bumps + rise * region.outerRise * heap;
        }

        /// <summary>
        /// 바닥 레이어 무게(합 1). 절벽은 바위, 길은 흙길, 던전 입구 둘레(pollutedRadius, 경계는 노이즈로 들쭉날쭉)는 오염 토양,
        /// 나머지는 숲 바닥에 약한 노이즈로 흙을 조금(forestDirtMix) 섞는다. 길 위는 오염보다 흙길이 먼저다.
        /// </summary>
        private static void GroundWeights(WorldTuning tuning, WorldRoute route, List<Vector2> flatSites, VillageSetup.Area village, float x, float z,
            out float forest, out float trail, out float rock, out float polluted)
        {
            float d = route.DistanceFrom(x, z, out float along);
            float half = tuning.ValleyHalfWidthAt(along);
            float path = 1f - Smooth01((d - tuning.pathHalfWidth) / 1.5f);
            rock = Smooth01((d - half - 1f) / 3f);
            trail = path * (1f - rock);
            float rest = Mathf.Max(0f, 1f - rock - trail);

            float nearSite = float.MaxValue;
            foreach (Vector2 site in flatSites) nearSite = Mathf.Min(nearSite, Vector2.Distance(site, new Vector2(x, z)));
            float edge = (Mathf.PerlinNoise(x * tuning.pollutedNoiseScale + 300f, z * tuning.pollutedNoiseScale + 300f) - 0.5f) * 2f * tuning.pollutedEdgeNoise;
            float pollution = 1f - Smooth01((nearSite - tuning.pollutedRadius - edge) / tuning.pollutedFade);
            polluted = rest * pollution;

            float ground = rest - polluted;
            float dirtSpots = Smooth01((Mathf.PerlinNoise(x * tuning.forestDirtNoiseScale + 500f, z * tuning.forestDirtNoiseScale + 500f) - 0.35f) / 0.4f) * tuning.forestDirtMix;
            if (village != null) dirtSpots = Mathf.Max(dirtSpots, village.DirtAt(x, z)); // 마을 안은 흙바닥 · 광장
            trail += ground * dirtSpots;
            forest = ground * (1f - dirtSpots);

            float sum = forest + trail + rock + polluted;
            if (sum <= 0f) { forest = 1f; return; }
            forest /= sum; trail /= sum; rock /= sum; polluted /= sum;
        }

        /// <summary>
        /// 길가 지역 바닥 무게(합 1, 채널 [길 · 길 옆 · 바탕 · 섞는 것]). 길(roadHalfWidth)은 낡은 얼룩(roadWear)만큼 바탕 · 섞는 것이 덮어
        /// 길 텍스처의 되풀이 무늬를 끊고, 길 옆 띠(sideWidth)는 틈마다 섞는 것(잡초), 그 밖은 바탕에 섞는 것을 노이즈로(mixAmount).
        /// 지역 시작에서 blendDistance 절반 동안 길 · 길 옆이 서서히 나타나 앞 지역 바닥과의 이음매를 누그러뜨린다.
        /// </summary>
        private static void StreetWeights(WorldTuning.Region r, WorldRoute route, float x, float z, out float road, out float side, out float rest, out float mix)
        {
            float d = route.DistanceFrom(x, z, out float along);
            float fadeIn = Smooth01((along - r.startDistance) / Mathf.Max(1f, r.blendDistance * 0.5f));
            float roadBand = 1f - Smooth01((d - r.roadHalfWidth) / 0.8f);
            float sideBand = 1f - Smooth01((d - r.roadHalfWidth - r.sideWidth) / 0.8f);
            road = roadBand * fadeIn;
            side = Mathf.Max(0f, sideBand - roadBand) * fadeIn;

            float wear = Smooth01((Mathf.PerlinNoise(x * r.wearNoiseScale + 1100f, z * r.wearNoiseScale + 1100f) - 0.4f) / 0.35f) * r.roadWear;
            float worn = road * wear;
            road -= worn;
            float cracks = Smooth01((Mathf.PerlinNoise(x * 0.15f + 1300f, z * 0.15f + 1300f) - 0.55f) / 0.3f) * 0.5f;
            float weedy = side * cracks;
            side -= weedy;

            float open = Mathf.Max(0f, 1f - road - side - weedy);
            float spots = Smooth01((Mathf.PerlinNoise(x * r.mixNoiseScale + 1500f, z * r.mixNoiseScale + 1500f) - 0.35f) / 0.4f) * r.mixAmount;
            mix = open * spots + weedy;
            rest = open * (1f - spots);

            float sum = road + side + rest + mix;
            if (sum <= 0f) { rest = 1f; return; }
            road /= sum; side /= sum; rest /= sum; mix /= sum;
        }



        // ---------- 바닥 레이어 ----------

        /// <summary>지형 레이어 파일을 만들거나 매번 모든 값을 덮어쓴다. 매끄러움은 텍스처 알파가 아니라 상수(알파가 1이면 거울처럼 하늘이 비친다).</summary>
        private static TerrainLayer Layer(string name, Texture2D texture, float tile, float smoothness)
        {
            string layerPath = $"{GroundFolder}/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = texture;
            layer.normalMapTexture = null;
            layer.maskMapTexture = null;
            layer.tileSize = new Vector2(tile, tile);
            layer.tileOffset = Vector2.zero;
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = Vector4.one;
            layer.smoothnessSource = TerrainLayerSmoothnessSource.Constant;
            layer.smoothness = smoothness;
            layer.metallic = 0f;
            layer.specular = Color.black;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// <summary>바닥 텍스처(VARCO, 이음매 없는 타일) 가져오기 설정을 맞추고 불러온다.</summary>
        private static Texture2D GroundTexture(string fileName)
        {
            string path = $"{GroundFolder}/{fileName}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[WorldSetup] 바닥 텍스처가 없습니다: {path}");
                return null;
            }
            bool changed = importer.textureType != TextureImporterType.Default || !importer.sRGBTexture || !importer.mipmapEnabled
                || importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 4
                || importer.alphaSource != TextureImporterAlphaSource.None;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>텍스처가 없는 레이어(절벽 바위)용 4×4 단색. 매번 색을 다시 칠한다.</summary>
        private static Texture2D SolidTexture(string name, Color color)
        {
            string path = $"{GroundFolder}/{name}.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = name };
                AssetDatabase.CreateAsset(texture, path);
            }
            var pixels = new Color[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Material TerrainMaterial()
        {
            string path = $"{ArtFolder}/TerrainLit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[WorldSetup] URP Terrain/Lit 셰이더를 찾지 못해 기본 지형 재질을 씁니다.");
                return null;
            }
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------- 입구 · 관문 · 적 무리 ----------

        /// <returns>입구 자리(앞 = 길 쪽). 플레이어가 이 자리 가까이 오면 던전에 들어간다.</returns>
        private static Transform BuildEntrance(WorldTuning tuning, WorldRoute route, Ground terrain, Transform parent, int index, TMP_FontAsset font, out Renderer doorway)
        {
            WorldTuning.DungeonSite site = tuning.dungeons[index];
            Vector3 position = OnGround(terrain, route.BesideAt(site.routeDistance, site.sideOffset));
            Vector3 toPath = OnGround(terrain, route.PointAt(site.routeDistance)) - position;
            toPath.y = 0f;

            var dungeon = new GameObject($"Dungeon{index + 1}").transform;
            dungeon.SetParent(parent, false);
            dungeon.SetPositionAndRotation(position, Quaternion.LookRotation(toPath.normalized));

            float labelHeight = 8.2f;
            GameObject building = site.HasEntrancePrefab ? AssetDatabase.LoadAssetAtPath<GameObject>(site.entrancePrefab) : null;
            if (site.HasEntrancePrefab && building == null) Debug.LogError($"[WorldSetup] {site.displayName}: 입구 프리팹이 없어 바위 입구로 세웁니다: {site.entrancePrefab}");
            BoxCollider box = building != null ? building.GetComponent<BoxCollider>() : null;
            if (box != null)
            {
                // 입구 건물(예: 대피소): 정면(-Z)이 길을 보게 돌려 정면이 입구 자리(z 0)에 오게. 상자 충돌은 그대로(들어서는 판정은 거리로)
                var shell = ((GameObject)PrefabUtility.InstantiatePrefab(building, dungeon)).transform;
                shell.name = building.name;
                shell.SetLocalPositionAndRotation(new Vector3(box.center.x, 0f, box.center.z - box.size.z * 0.5f), Quaternion.Euler(0f, 180f, 0f));
                GameObject door = Block(shell, "Doorway", site.doorwayCenter, new Vector3(site.doorwaySize.x, site.doorwaySize.y, 0.02f), Lit("Doorway", new Color(0.05f, 0.04f, 0.06f)), collide: false);
                door.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                doorway = door.GetComponent<Renderer>();
                labelHeight = box.center.y + box.size.y * 0.5f + 1.8f;
            }
            else
            {
                // 바위 덩어리 + 어두운 입구(입구 면에는 충돌이 없다 — 들어서는 판정은 거리로)
                Block(dungeon, "Rock", new Vector3(0f, 3f, -3.5f), new Vector3(11f, 8f, 6f), Lit("EntranceRock", new Color(0.36f, 0.34f, 0.32f)), collide: true);
                GameObject door = Block(dungeon, "Doorway", new Vector3(0f, 2f, -0.45f), new Vector3(3.4f, 4f, 0.2f), Lit("Doorway", new Color(0.05f, 0.04f, 0.06f)), collide: false);
                doorway = door.GetComponent<Renderer>();
            }
            GameObject glow = Block(dungeon, "EnterMark", new Vector3(0f, 0.03f, 1.2f), new Vector3(tuning.entranceRadius * 1.6f, 0.02f, tuning.entranceRadius * 1.6f), Lit("EnterMark", new Color(0.35f, 0.6f, 0.9f)), collide: false, PrimitiveType.Cylinder);
            glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Label(dungeon, font, site.displayName, new Vector3(0f, labelHeight, 0f), Quaternion.Euler(0f, 180f, 0f));

            var entrance = new GameObject("Entrance").transform;
            entrance.SetParent(dungeon, false);
            entrance.localPosition = new Vector3(0f, 0f, 1.2f);
            return entrance;
        }

        /// <summary>던전마다 그 뒤 좁은 고개를 막는 관문. 관문 너머에서 새 지역이 시작하면 그 지역 이름을 단다.</summary>
        private static GameObject[] BuildGates(WorldTuning tuning, WorldRoute route, Ground terrain, TMP_FontAsset font)
        {
            var parent = new GameObject("Gates").transform;
            var gates = new GameObject[tuning.dungeons.Length];
            Material material = TestRoomSetup.LoadOrCreateLit("Assets/Game/Art/Floor/LockedDoor.mat", new Color(0.75f, 0.2f, 0.15f));
            float span = terrain.Area.width;
            for (int i = 0; i < gates.Length; i++)
            {
                float distance = tuning.GateDistance(i);
                Vector3 center = OnGround(terrain, route.PointAt(distance));
                Vector3 forward = Dir(route.DirectionAt(distance));
                var gate = new GameObject($"Gate{i + 1}").transform;
                gate.SetParent(parent, false);
                gate.SetPositionAndRotation(center, Quaternion.LookRotation(forward));
                float wall = tuning.passHalfWidth * 2f + 10f; // 양쪽 절벽 속까지
                Block(gate, "Wall", new Vector3(0f, 2.5f, 0f), new Vector3(wall, 7f, 1.5f), material, collide: true);
                // 보이지 않는 벽: 절벽을 점프로 타고 올라 관문을 돌아가지 못하게 지형 끝까지 막는다(길은 남→북으로만 나아가서 다른 골짜기를 자르지 않는다)
                var barrier = new GameObject("Barrier").AddComponent<BoxCollider>();
                barrier.transform.SetParent(gate, false);
                barrier.center = new Vector3(0f, 40f, 0f);
                barrier.size = new Vector3(span * 2.5f, 100f, 1.5f);
                Label(gate, font, GateLabel(tuning, i), new Vector3(0f, 7.5f, -1f), Quaternion.identity);
                gates[i] = gate.gameObject;
            }
            return gates;
        }

        private static string GateLabel(WorldTuning tuning, int gate)
        {
            string dungeon = tuning.dungeons[gate].displayName;
            for (int r = 1; r < tuning.regions.Length; r++)
                if (tuning.GateBefore(r) == gate) return $"{tuning.regions[r].displayName} — {dungeon}을 깨면 열린다";
            return gate == tuning.dungeons.Length - 1 ? tuning.nextRegionLabel : $"관문 — {dungeon}을 깨면 열린다";
        }

        /// <summary>적 무리 자리마다 자식으로 적이 나올 자리(둥글게).</summary>
        private static Transform[] BuildEncounterSites(WorldTuning tuning, WorldRoute route, Ground terrain, Transform parent)
        {
            var sites = new Transform[tuning.encounters.Length];
            for (int i = 0; i < sites.Length; i++)
            {
                WorldTuning.EncounterSite data = tuning.encounters[i];
                var site = new GameObject($"Encounter{i + 1}").transform;
                site.SetParent(parent, false);
                site.position = OnGround(terrain, route.BesideAt(data.routeDistance, data.sideOffset));
                for (int k = 0; k < data.count; k++)
                {
                    float angle = k * Mathf.PI * 2f / data.count;
                    float radius = data.count > 1 ? tuning.encounterSpread : 0f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    var point = new GameObject($"Spawn{k + 1}").transform;
                    point.SetParent(site, false);
                    Vector3 world = site.position + offset;
                    point.position = OnGround(terrain, (world.x, world.z));
                }
                sites[i] = site;
            }
            return sites;
        }

        // ---------- 나무 ----------

        /// <summary>
        /// 골짜기 안 흙길 밖에 나무(VARCO 모델 프리팹, 줄기 충돌). 종류는 treeKinds 무게로 섞고, 던전 입구 둘레는 weightNearDungeon.
        /// 고정 시드라 매번 같은 자리. 프리팹이 없는 종류는 회색 도형으로 그린다.
        /// 자리는 나무 고정 기준(treeReference: 예전 숲 길 · 관문 · 비운 자리)과 그 길로 잰 범위에서 고른다 — 길 · 던전 · 무리를 바꿔도 남는 나무 자리가 그대로. 지역 나무 비율(treeDensity)이
        /// 1보다 작은 곳(도시 경계 앞뒤)은 나무 번호 해시로 그만큼 빼고, 마을 안처럼 난수 · 번호는 그대로 쓰고 지나간다.
        /// </summary>
        private static void BuildTrees(WorldTuning tuning, WorldRoute route, Ground terrain, Transform parent, List<Vector3> sites, Transform[] entrances, VillageSetup.Area village)
        {
            TreeSetup.Run();
            var forest = new GameObject("Trees").transform;
            forest.SetParent(parent, false);
            var prefabs = new GameObject[tuning.treeKinds.Length];
            for (int i = 0; i < prefabs.Length; i++) prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(TreeSetup.PrefabPath(tuning.treeKinds[i].id));
            // 나무가 없는 지역(도시)의 입구 · 무리 자리는 나무 자리 고르기에 넣지 않는다 — 넣으면 그 둘레 후보가 건너뛰어져
            // 난수 흐름이 어긋나 숲 나무 자리까지 바뀐다(그 둘레 나무는 어차피 지역 나무 비율 0으로 빠진다)
            sites = WithTrees(tuning, route, sites);
            var dungeonSpots = new List<Vector3>();
            foreach (Transform entrance in entrances) dungeonSpots.Add(entrance.position);
            dungeonSpots = WithTrees(tuning, route, dungeonSpots);

            WorldTuning.TreeLayoutReference reference = tuning.treeReference;
            WorldRoute treeRoute = reference.route != null && reference.route.Length >= 2 ? RouteOf(reference.route) : route;
            var keepOut = new List<Vector3>();
            if (reference.keepOut != null) foreach (Vector2 spot in reference.keepOut) keepOut.Add(new Vector3(spot.x, 0f, spot.y));
            Rect area = RouteArea(tuning, treeRoute);
            var random = new System.Random(tuning.treeSeed);
            var taken = new HashSet<Vector2Int>();
            var counts = new int[prefabs.Length];
            int placed = 0, cleared = 0, faded = 0;
            for (int attempt = 0; attempt < tuning.treeCount * 8 && placed < tuning.treeCount; attempt++)
            {
                float x = area.xMin + (float)random.NextDouble() * area.width;
                float z = area.yMin + (float)random.NextDouble() * area.height;
                float d = treeRoute.DistanceFrom(x, z, out float along);
                if (d < tuning.treePathClearance || d > tuning.ValleyHalfWidthAt(along, reference.passDistances) - 2f) continue;
                if (NearAny(sites, x, z, SiteFlatRadius) || NearAny(keepOut, x, z, SiteFlatRadius)) continue;
                var cell = new Vector2Int(Mathf.FloorToInt(x / tuning.treeSpacing), Mathf.FloorToInt(z / tuning.treeSpacing));
                if (!taken.Add(cell)) continue;

                // 나무마다 난수 3번(종류 · 크기 · 회전) — 예전 도형 나무와 같은 횟수라 자리가 그대로다
                bool nearDungeon = NearAny(dungeonSpots, x, z, tuning.treeNearDungeonRadius);
                int kind = PickTreeKind(tuning.treeKinds, nearDungeon, random.NextDouble());
                float scale = Mathf.Lerp(tuning.treeScaleRange.x, tuning.treeScaleRange.y, (float)random.NextDouble());
                Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                // 마을 안 나무는 세우지 않는다. 난수 · 칸 · 번호는 그대로 쓰고 지나가 마을 밖 나무 자리는 마을이 없을 때와 같다(Setup Village가 열린 씬에서 지우는 것과 같은 결과)
                if (village != null && village.ClearsTree(x, z))
                {
                    placed++;
                    cleared++;
                    continue;
                }
                // 다른 지역으로 넘어가는 곳: 지역 나무 비율만큼만 남긴다(전체 길 거리로 잼 — 숲 길 안에서는 숲 길과 같다)
                route.DistanceFrom(x, z, out float routeAlong);
                float density = tuning.TreeDensityAt(routeAlong);
                if (density < 1f && Hash01(placed) >= density)
                {
                    placed++;
                    faded++;
                    continue;
                }
                Vector3 ground = OnGround(terrain, (x, z));

                Transform tree;
                if (kind >= 0 && prefabs[kind] != null)
                {
                    tree = ((GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind], forest)).transform;
                    counts[kind]++;
                }
                else
                {
                    tree = PlaceholderTree(forest);
                }
                tree.name = $"Tree{placed}";
                tree.SetPositionAndRotation(ground, rotation);
                tree.localScale = Vector3.one * scale;
                placed++;
            }
            var summary = new System.Text.StringBuilder();
            for (int i = 0; i < counts.Length; i++) summary.Append($" · {tuning.treeKinds[i].id} {counts[i]}");
            Debug.Log($"[WorldSetup] 나무 {placed - cleared - faded}그루{summary} · 마을 자리라 안 세운 나무 {cleared}그루 · 다른 지역이라 뺀 나무 {faded}그루");
        }

        /// <summary>자리 가운데 지역 나무 비율이 0보다 큰 자리만.</summary>
        private static List<Vector3> WithTrees(WorldTuning tuning, WorldRoute route, List<Vector3> spots)
        {
            var kept = new List<Vector3>();
            foreach (Vector3 spot in spots)
            {
                route.DistanceFrom(spot.x, spot.z, out float along);
                if (tuning.TreeDensityAt(along) > 0f) kept.Add(spot);
            }
            return kept;
        }

        /// <summary>정수 → 0~1 고정 해시(나무 빼기 · 배치용, 난수 흐름과 따로).</summary>
        private static float Hash01(int i)
        {
            unchecked
            {
                uint h = (uint)i * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static int PickTreeKind(WorldTuning.TreeKind[] kinds, bool nearDungeon, double roll)
        {
            float total = 0f;
            foreach (WorldTuning.TreeKind kind in kinds) total += nearDungeon ? kind.weightNearDungeon : kind.weight;
            if (total <= 0f) return -1;
            float pick = (float)roll * total;
            for (int i = 0; i < kinds.Length; i++)
            {
                pick -= nearDungeon ? kinds[i].weightNearDungeon : kinds[i].weight;
                if (pick < 0f) return i;
            }
            return kinds.Length - 1;
        }

        /// <summary>프리팹이 없을 때의 회색 도형 나무(줄기 충돌 · 수관). 크기는 부르는 쪽이 정한다.</summary>
        private static Transform PlaceholderTree(Transform parent)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(parent, false);
            Block(tree, "Trunk", new Vector3(0f, 3f, 0f), new Vector3(0.6f, 3f, 0.6f), Lit("TreeTrunk", new Color(0.32f, 0.24f, 0.17f)), collide: true, PrimitiveType.Cylinder);
            Block(tree, "Crown", new Vector3(0f, 7.2f, 0f), new Vector3(4f, 4.8f, 4f), Lit("TreeLeaves", new Color(0.18f, 0.32f, 0.16f)), collide: false, PrimitiveType.Sphere);
            return tree;
        }

        private static bool NearAny(List<Vector3> sites, float x, float z, float radius)
        {
            foreach (Vector3 site in sites)
            {
                float dx = site.x - x, dz = site.z - z;
                if (dx * dx + dz * dz < radius * radius) return true;
            }
            return false;
        }

        /// <summary>모든 지형 타일 가장자리 보이지 않는 벽(떨어지지 않게).</summary>
        private static void BuildBounds(Ground terrain, Transform parent)
        {
            var bounds = new GameObject("Bounds").transform;
            bounds.SetParent(parent, false);
            Rect area = terrain.Area;
            Vector3 center = new Vector3(area.center.x, 40f, area.center.y);
            Vector3 size = new Vector3(area.width, 0f, area.height);
            void Wall(string name, Vector3 offset, Vector3 extent)
            {
                var wall = new GameObject(name).AddComponent<BoxCollider>();
                wall.transform.SetParent(bounds, false);
                wall.transform.position = center + offset;
                wall.size = extent;
            }
            Wall("North", new Vector3(0f, 0f, size.z * 0.5f), new Vector3(size.x, 100f, 1f));
            Wall("South", new Vector3(0f, 0f, -size.z * 0.5f), new Vector3(size.x, 100f, 1f));
            Wall("East", new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(1f, 100f, size.z));
            Wall("West", new Vector3(-size.x * 0.5f, 0f, 0f), new Vector3(1f, 100f, size.z));
        }

        private static void BakeNavMesh(GameObject root)
        {
            NavMeshSurface surface = TestRoomSetup.AddNavMeshSurface(root);
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.25f; // 넓은 지형이라 조금 거칠게(굽는 시간)
            surface.BuildNavMesh();
            if (AssetDatabase.LoadAssetAtPath<Object>(NavMeshPath) != null) AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        }

        /// <summary>
        /// 열린 씬의 월드 NavMesh를 같은 에셋(WorldNavMesh.asset)에 다시 굽는다(Setup Village). 에셋을 새로 만들면 GUID가 바뀌어
        /// 저장 안 한 씬과 어긋나므로 내용만 바꾼다. Setup World와 같이 관문 · 가장자리 벽은 빼고 굽는다(관문은 World 밖이라 안 모인다).
        /// </summary>
        internal static void UpdateNavMeshInPlace(GameObject root)
        {
            var surface = root.GetComponent<NavMeshSurface>();
            if (surface == null || surface.navMeshData == null)
            {
                Debug.LogError("[WorldSetup] World에 NavMeshSurface · NavMesh 에셋이 없습니다. Tools/Game/Setup World로 다시 만드세요.");
                return;
            }
            Transform bounds = root.transform.Find("Bounds");
            bool boundsActive = bounds != null && bounds.gameObject.activeSelf;
            if (bounds != null) bounds.gameObject.SetActive(false);
            try
            {
                var sources = new List<UnityEngine.AI.NavMeshBuildSource>();
                UnityEngine.AI.NavMeshBuilder.CollectSources(root.transform, surface.layerMask, surface.useGeometry, surface.defaultArea,
                    new List<UnityEngine.AI.NavMeshBuildMarkup>(), sources);
                AppendModifierVolumes(root, surface.agentTypeID, sources); // 안전 지대(마을) NavMesh 구멍 — surface.BuildNavMesh와 같게
                bool ok = UnityEngine.AI.NavMeshBuilder.UpdateNavMeshData(surface.navMeshData, surface.GetBuildSettings(), sources, surface.navMeshData.sourceBounds);
                if (!ok) Debug.LogError("[WorldSetup] NavMesh를 다시 굽지 못했습니다.");
                EditorUtility.SetDirty(surface.navMeshData);
                surface.RemoveData();
                surface.AddData();
            }
            finally
            {
                if (bounds != null) bounds.gameObject.SetActive(boundsActive);
            }
        }

        /// <summary>root 아래 NavMeshModifierVolume을 굽기 소스로 넣는다(NavMeshSurface가 Children으로 모을 때와 같은 모양).</summary>
        private static void AppendModifierVolumes(GameObject root, int agentTypeID, List<UnityEngine.AI.NavMeshBuildSource> sources)
        {
            foreach (NavMeshModifierVolume volume in root.GetComponentsInChildren<NavMeshModifierVolume>())
            {
                if (!volume.isActiveAndEnabled || !volume.AffectsAgentType(agentTypeID)) continue;
                Transform t = volume.transform;
                Vector3 scale = t.lossyScale;
                sources.Add(new UnityEngine.AI.NavMeshBuildSource
                {
                    shape = UnityEngine.AI.NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(t.TransformPoint(volume.center), t.rotation, Vector3.one),
                    size = new Vector3(volume.size.x * Mathf.Abs(scale.x), volume.size.y * Mathf.Abs(scale.y), volume.size.z * Mathf.Abs(scale.z)),
                    area = volume.area,
                });
            }
        }

        // ---------- 지역 배치 ----------

        /// <summary>바닥에 놓인 프리팹 하나의 발자국(위에서 본 돌린 사각형, 프리팹 BoxCollider).</summary>
        private struct Footprint
        {
            public Vector2 Center;
            public Vector2 Right;
            public Vector2 Forward;
            public Vector2 Half;

            public Vector2 Corner(int k) => Center + Right * (Half.x * ((k & 1) == 0 ? -1f : 1f)) + Forward * (Half.y * ((k & 2) == 0 ? -1f : 1f));
        }

        /// <summary>지역 하나를 세우는 동안 쓰는 것(프리팹 캐시 · 차지한 자리 · 수).</summary>
        private sealed class Layout
        {
            public WorldTuning.Region Region;
            public WorldRoute Route;
            public Ground Ground;
            public readonly List<Footprint> Taken = new List<Footprint>();
            public readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
            public readonly SortedDictionary<string, int> Counts = new SortedDictionary<string, int>();
            public int Placed;

            public GameObject Prefab(string id)
            {
                if (Prefabs.TryGetValue(id, out GameObject prefab)) return prefab;
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Region.prefabFolder}/{id}.prefab");
                if (prefab == null) Debug.LogError($"[WorldSetup] {Region.displayName}: 프리팹이 없습니다: {Region.prefabFolder}/{id}.prefab");
                Prefabs[id] = prefab;
                return prefab;
            }

            public void Place(GameObject prefab, Transform parent, Vector2 position, float yaw, Footprint footprint)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.SetPositionAndRotation(new Vector3(position.x, Ground.HeightAt(position.x, position.y), position.y), Quaternion.Euler(0f, yaw, 0f));
                Counts.TryGetValue(prefab.name, out int n);
                Counts[prefab.name] = n + 1;
                instance.name = $"{prefab.name}_{n}";
                Taken.Add(footprint);
                Placed++;
            }
        }

        /// <returns>놓은 프리팹 수(모든 지역)</returns>
        private static int BuildRegionLayouts(WorldTuning tuning, WorldRoute route, Ground ground, Transform parent)
        {
            int total = 0;
            for (int i = 0; i < tuning.regions.Length; i++)
            {
                WorldTuning.Region region = tuning.regions[i];
                int pieces = (region.streetRows?.Length ?? 0) + (region.props?.Length ?? 0) + (region.gateProps?.Length ?? 0) + (region.endProps?.Length ?? 0);
                if (pieces == 0 && region.boundaryHalfWidth <= 0f) continue;
                var root = new GameObject($"Region_{region.id}").transform;
                root.SetParent(parent, false);
                total += BuildRegion(tuning, route, ground, root, i);
            }
            return total;
        }

        /// <summary>
        /// 지역 하나의 배치(고정 시드 layoutSeed): 앞 관문 검문소(gateProps) → 지역 끝 막기(endProps) → 길가 건물 줄(streetRows, 길 양옆, 정면이 길) →
        /// 길 위 · 옆 소품(props, 길 가운데 clearLaneHalfWidth는 비움) → 경계 보이지 않는 벽. 먼저 놓인 것과 겹치면 건너뛴다.
        /// </summary>
        private static int BuildRegion(WorldTuning tuning, WorldRoute route, Ground ground, Transform root, int index)
        {
            WorldTuning.Region r = tuning.regions[index];
            float regionEnd = tuning.RegionEnd(index, route.Length);
            bool last = index == tuning.regions.Length - 1;
            var random = new System.Random(r.layoutSeed);
            var layout = new Layout { Region = r, Route = route, Ground = ground };

            ReserveSites(tuning, route, layout, r.startDistance, regionEnd);
            int gate = tuning.GateBefore(index);
            if (gate >= 0 && r.gateProps != null && r.gateProps.Length > 0) PlaceFixed(layout, r.gateProps, tuning.GateDistance(gate), Group(root, "Checkpoint"));
            if (r.endProps != null && r.endProps.Length > 0) PlaceFixed(layout, r.endProps, regionEnd, Group(root, "RoadEnd"));

            float from = r.startDistance + r.layoutMargin.x;
            float to = regionEnd - r.layoutMargin.y;
            int skipped = 0;
            if (r.streetRows != null)
            {
                Transform buildings = Group(root, "Buildings");
                foreach (WorldTuning.StreetRow row in r.streetRows)
                {
                    foreach (float side in new[] { 1f, -1f })
                    {
                        float s = from + (float)random.NextDouble() * row.gap.y;
                        for (int guard = 0; s < to && guard < 10000; guard++)
                        {
                            // 건물마다 난수 5번(종류 · 빈 터 · 앞뒤 · 각도 · 틈) — 건너뛰어도 같은 수라 뒤 자리가 흔들리지 않는다
                            string id = PickId(row.kinds, random.NextDouble());
                            bool empty = random.NextDouble() < row.emptyChance;
                            float push = ((float)random.NextDouble() * 2f - 1f) * row.offsetJitter;
                            float turn = ((float)random.NextDouble() * 2f - 1f) * row.yawJitter;
                            float gap = Mathf.Lerp(row.gap.x, row.gap.y, (float)random.NextDouble());
                            GameObject prefab = id != null ? layout.Prefab(id) : null;
                            BoxCollider box = prefab != null ? prefab.GetComponent<BoxCollider>() : null;
                            if (box == null) { s += 5f + gap; continue; }
                            float width = box.size.x;
                            float mid = s + width * 0.5f;
                            if (mid + width * 0.5f > to) break;
                            if (!empty)
                            {
                                (float dx, float dz) = route.DirectionAt(mid);
                                Vector2 outward = new Vector2(dz, -dx) * side; // 길에서 멀어지는 쪽 = 건물 앞(+Z), 정면(-Z)은 길
                                float lateral = row.frontOffset + push + box.size.z * 0.5f - box.center.z;
                                (float px, float pz) = route.BesideAt(mid, side * lateral);
                                float yaw = Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg + turn;
                                Footprint footprint = FootprintOf(new Vector2(px, pz), yaw, box);
                                if (!Overlaps(layout.Taken, footprint, 0.3f)) layout.Place(prefab, buildings, new Vector2(px, pz), yaw, footprint);
                                else skipped++;
                            }
                            s += width + gap;
                        }
                    }
                }
            }

            if (r.props != null)
            {
                Transform props = Group(root, "Props");
                foreach (WorldTuning.PropScatter scatter in r.props)
                {
                    foreach (float side in new[] { 1f, -1f })
                    {
                        float s = from + (float)random.NextDouble() * scatter.every;
                        for (int guard = 0; s < to && guard < 10000; guard++)
                        {
                            // 소품마다 난수 5번(종류 · 옆 · 뒤집기 · 각도 · 다음 간격)
                            string id = PickId(scatter.kinds, random.NextDouble());
                            float lateral = Mathf.Lerp(scatter.side.x, scatter.side.y, (float)random.NextDouble());
                            bool flip = random.NextDouble() < scatter.flipChance;
                            float turn = ((float)random.NextDouble() * 2f - 1f) * scatter.yawJitter;
                            float step = scatter.every * (0.5f + (float)random.NextDouble());
                            GameObject prefab = id != null ? layout.Prefab(id) : null;
                            BoxCollider box = prefab != null ? prefab.GetComponent<BoxCollider>() : null;
                            if (box != null)
                            {
                                (float dx, float dz) = route.DirectionAt(s);
                                float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg + (flip ? 180f : 0f) + turn;
                                (float px, float pz) = route.BesideAt(s, side * lateral);
                                Footprint footprint = FootprintOf(new Vector2(px, pz), yaw, box);
                                if (InnerOffset(footprint, route, s, side) >= r.clearLaneHalfWidth && !Overlaps(layout.Taken, footprint, 0.2f))
                                    layout.Place(prefab, props, new Vector2(px, pz), yaw, footprint);
                                else skipped++;
                            }
                            s += step;
                        }
                    }
                }
            }

            if (r.boundaryHalfWidth > 0f) BuildRegionWalls(r, route, regionEnd, last, Group(root, "Boundary"));

            var summary = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, int> pair in layout.Counts) summary.Append($" · {pair.Key} {pair.Value}");
            Debug.Log($"[WorldSetup] {r.displayName}(길 {r.startDistance:0} ~ {regionEnd:0} m): 프리팹 {layout.Placed}개{summary} · 겹쳐서 건너뛴 자리 {skipped}");
            return layout.Placed;
        }

        /// <summary>
        /// 지역 안 던전 입구 건물(+ 앞 4 m 다가가는 자리) · 관문 벽 자리를 차지한 자리로 남겨 건물 · 소품이 거기 오지 않게 한다.
        /// 난수는 쓰지 않아 다른 자리는 그대로다(거기 걸리던 것만 빠진다).
        /// </summary>
        private static void ReserveSites(WorldTuning tuning, WorldRoute route, Layout layout, float from, float to)
        {
            for (int i = 0; i < tuning.dungeons.Length; i++)
            {
                WorldTuning.DungeonSite site = tuning.dungeons[i];
                if (site.routeDistance >= from && site.routeDistance <= to && site.HasEntrancePrefab)
                {
                    var building = AssetDatabase.LoadAssetAtPath<GameObject>(site.entrancePrefab);
                    BoxCollider box = building != null ? building.GetComponent<BoxCollider>() : null;
                    if (box != null)
                    {
                        (float x, float z) = route.BesideAt(site.routeDistance, site.sideOffset);
                        (float px, float pz) = route.PointAt(site.routeDistance);
                        Vector2 forward = new Vector2(px - x, pz - z).normalized; // 길 쪽
                        const float Approach = 4f;
                        float back = box.size.z + 0.5f;
                        layout.Taken.Add(new Footprint
                        {
                            Center = new Vector2(x, z) + forward * ((Approach - back) * 0.5f),
                            Right = new Vector2(forward.y, -forward.x),
                            Forward = forward,
                            Half = new Vector2(box.size.x * 0.5f + 0.5f, (Approach + back) * 0.5f),
                        });
                    }
                }
                float gate = tuning.GateDistance(i);
                if (gate < from || gate > to) continue;
                (float gx, float gz) = route.PointAt(gate);
                (float dx, float dz) = route.DirectionAt(gate);
                layout.Taken.Add(new Footprint
                {
                    Center = new Vector2(gx, gz),
                    Right = new Vector2(dz, -dx),
                    Forward = new Vector2(dx, dz),
                    Half = new Vector2(tuning.passHalfWidth + 5f, 1.75f), // 관문 벽(BuildGates) 앞뒤 1 m
                });
            }
        }

        /// <summary>길 거리 distance의 길 가운데를 기준으로(x = 오른쪽, y = 앞) 정해 둔 소품을 놓는다. 겹침은 안 보고 차지한 자리로 남긴다.</summary>
        private static void PlaceFixed(Layout layout, WorldTuning.PlacedProp[] pieces, float distance, Transform parent)
        {
            (float cx, float cz) = layout.Route.PointAt(distance);
            (float dx, float dz) = layout.Route.DirectionAt(distance);
            var forward = new Vector2(dx, dz);
            var right = new Vector2(dz, -dx);
            float roadYaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            foreach (WorldTuning.PlacedProp piece in pieces)
            {
                GameObject prefab = layout.Prefab(piece.id);
                BoxCollider box = prefab != null ? prefab.GetComponent<BoxCollider>() : null;
                if (box == null) continue;
                Vector2 position = new Vector2(cx, cz) + right * piece.position.x + forward * piece.position.y;
                float yaw = roadYaw + piece.yaw;
                layout.Place(prefab, parent, position, yaw, FootprintOf(position, yaw, box));
            }
        }

        /// <summary>
        /// 지역 경계 보이지 않는 벽: 길 양옆 boundaryHalfWidth에 20 m 조각으로(길을 따라 꺾임), 마지막 지역이면 길 끝을 가로지르는 벽.
        /// 골짜기 절벽 대신 건물 줄 · 이 벽이 다닐 수 있는 넓이를 정한다. NavMesh를 굽기 전에 세워 적도 넘지 않는다.
        /// </summary>
        private static void BuildRegionWalls(WorldTuning.Region r, WorldRoute route, float regionEnd, bool last, Transform parent)
        {
            const float Piece = 20f;
            int n = 0;
            for (float s = r.startDistance; s < regionEnd - 0.01f; s += Piece)
            {
                float length = Mathf.Min(Piece, regionEnd - s);
                float mid = s + length * 0.5f;
                (float dx, float dz) = route.DirectionAt(mid);
                foreach (float side in new[] { 1f, -1f })
                {
                    (float x, float z) = route.BesideAt(mid, side * r.boundaryHalfWidth);
                    var wall = new GameObject($"Wall{n++}").AddComponent<BoxCollider>();
                    wall.transform.SetParent(parent, false);
                    wall.transform.SetPositionAndRotation(new Vector3(x, 40f, z), Quaternion.LookRotation(new Vector3(dx, 0f, dz)));
                    wall.size = new Vector3(1f, 100f, length + 1f);
                }
            }
            if (!last) return;
            (float ex, float ez) = route.PointAt(regionEnd);
            (float fx, float fz) = route.DirectionAt(regionEnd);
            var end = new GameObject("WallEnd").AddComponent<BoxCollider>();
            end.transform.SetParent(parent, false);
            end.transform.SetPositionAndRotation(new Vector3(ex + fx * 1.5f, 40f, ez + fz * 1.5f), Quaternion.LookRotation(new Vector3(fx, 0f, fz)));
            end.size = new Vector3(r.boundaryHalfWidth * 2f + 2f, 100f, 1f);
        }

        private static string PickId(WorldTuning.Pick[] kinds, double roll)
        {
            if (kinds == null || kinds.Length == 0) return null;
            float total = 0f;
            foreach (WorldTuning.Pick kind in kinds) total += kind.weight;
            if (total <= 0f) return kinds[0].id;
            float pick = (float)roll * total;
            foreach (WorldTuning.Pick kind in kinds)
            {
                pick -= kind.weight;
                if (pick < 0f) return kind.id;
            }
            return kinds[kinds.Length - 1].id;
        }

        private static Footprint FootprintOf(Vector2 pivot, float yaw, BoxCollider box)
        {
            float a = yaw * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            var right = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
            return new Footprint
            {
                Center = pivot + right * box.center.x + forward * box.center.z,
                Right = right,
                Forward = forward,
                Half = new Vector2(box.size.x * 0.5f, box.size.z * 0.5f),
            };
        }

        /// <summary>발자국이 길 가운데에 가장 가까운 옆 거리(그 쪽으로 +). 가운데를 넘으면 음수.</summary>
        private static float InnerOffset(Footprint footprint, WorldRoute route, float distance, float side)
        {
            (float cx, float cz) = route.PointAt(distance);
            (float dx, float dz) = route.DirectionAt(distance);
            var right = new Vector2(dz, -dx);
            float inner = float.MaxValue;
            for (int k = 0; k < 4; k++) inner = Mathf.Min(inner, Vector2.Dot(footprint.Corner(k) - new Vector2(cx, cz), right) * side);
            return inner;
        }

        /// <summary>두 돌린 사각형이 margin보다 가까이 겹치는지(분리축).</summary>
        private static bool Overlaps(List<Footprint> taken, Footprint a, float margin)
        {
            foreach (Footprint b in taken)
            {
                if ((a.Center - b.Center).sqrMagnitude > (a.Half.magnitude + b.Half.magnitude + margin) * (a.Half.magnitude + b.Half.magnitude + margin)) continue;
                bool separated = false;
                Vector2 d = b.Center - a.Center;
                foreach (Vector2 axis in new[] { a.Right, a.Forward, b.Right, b.Forward })
                {
                    float ra = Mathf.Abs(Vector2.Dot(a.Right * a.Half.x, axis)) + Mathf.Abs(Vector2.Dot(a.Forward * a.Half.y, axis));
                    float rb = Mathf.Abs(Vector2.Dot(b.Right * b.Half.x, axis)) + Mathf.Abs(Vector2.Dot(b.Forward * b.Half.y, axis));
                    if (Mathf.Abs(Vector2.Dot(d, axis)) > ra + rb + margin) { separated = true; break; }
                }
                if (!separated) return true;
            }
            return false;
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        // ---------- 도움 ----------

        private static Vector3 OnGround(Ground terrain, (float x, float z) point) => new Vector3(point.x, terrain.HeightAt(point.x, point.z), point.z);

        private static Vector3 Dir((float x, float z) direction) => new Vector3(direction.x, 0f, direction.z);

        private static GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material, bool collide, PrimitiveType shape = PrimitiveType.Cube)
        {
            GameObject block = GameObject.CreatePrimitive(shape);
            if (!collide) Object.DestroyImmediate(block.GetComponent<Collider>());
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        /// <summary>월드에 떠 있는 글자. TextMeshPro는 +z를 바라보는 쪽에서 읽힌다.</summary>
        internal static void Label(Transform parent, TMP_FontAsset font, string text, Vector3 localPosition, Quaternion localRotation)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            label.rectTransform.sizeDelta = new Vector2(20f, 3f);
            label.font = font;
            label.fontSize = 10f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 220);
            label.text = text;
        }

        private static Material Lit(string name, Color color) => TestRoomSetup.LoadOrCreateLit($"{ArtFolder}/{name}.mat", color);

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
