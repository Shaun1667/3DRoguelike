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
    /// 숲 월드 씬을 만든다(메뉴 Tools/Game/Setup World). 월드는 고정이라 지형 · 흙길 · 나무 · 던전 입구 · 관문 · 적 무리 자리를
    /// <see cref="WorldTuning"/> 값으로 씬에 세우고 NavMesh도 여기서 굽는다. 여러 번 실행해도 결과가 같다(나무도 고정 시드).
    /// 수치 에셋은 없을 때만 만들고 값을 덮어쓰지 않는다.
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
            VillageSetup.Area villageArea = VillageSetup.AreaOf(village, route);
            TestRoomSetup.BuildLighting();
            BuildAtmosphere(tuning); // BuildLighting의 앰비언트를 월드용으로 덮어쓴다(시험 방은 그대로)

            var root = new GameObject("World");
            Terrain terrain = BuildTerrain(tuning, route, villageArea, root.transform);
            var sites = new List<Vector3>();

            Transform[] entrances = new Transform[tuning.dungeons.Length];
            Renderer[] doorways = new Renderer[tuning.dungeons.Length];
            TMP_FontAsset font = HudSetup.LoadOrCreateFontAsset();
            for (int i = 0; i < tuning.dungeons.Length; i++)
            {
                entrances[i] = BuildEntrance(tuning, route, terrain, root.transform, i, font, out doorways[i]);
                sites.Add(entrances[i].position);
            }

            Transform start = new GameObject("StartPoint").transform;
            start.SetParent(root.transform, false);
            start.position = OnGround(terrain, route.PointAt(6f));
            start.rotation = Quaternion.LookRotation(Dir(route.DirectionAt(6f)));
            sites.Add(start.position);

            Transform[] encounterSites = BuildEncounterSites(tuning, route, terrain, root.transform);
            foreach (Transform site in encounterSites) sites.Add(site.position);

            BuildTrees(tuning, route, terrain, root.transform, sites, entrances, villageArea);
            VillageSetup.Build(village, villageArea, tuning, route, terrain, root.transform, font); // 마을 집 · 울타리 · 주민도 NavMesh에 구멍을 낸다

            // 관문을 세우기 전에 굽는다 — 관문이 열리면 지나갈 길이 이미 NavMesh에 있게
            BakeNavMesh(root);
            GameObject[] gates = BuildGates(tuning, route, terrain, font);
            BuildBounds(terrain, root.transform);

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
            fields.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, CurrentRun.WorldScene);
            BuildSceneList.Ensure(CurrentRun.WorldScene, first: true);
            BuildSceneList.Ensure(CurrentRun.FloorScene, first: false);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WorldSetup] 월드 씬 생성: {CurrentRun.WorldScene} · 길 {route.Length:0} m · 던전 {tuning.dungeons.Length}개 · 지형 {terrain.terrainData.size.x:0} × {terrain.terrainData.size.z:0} m");
        }

        public static WorldTuning LoadOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<WorldTuning>(TuningPath);
            if (tuning != null)
            {
                EditorUtility.SetDirty(tuning); // 새로 생긴 필드를 파일에 남긴다
                return tuning;
            }
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<WorldTuning>(), TuningPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<WorldTuning>(TuningPath);
        }

        private static void BuildAtmosphere(WorldTuning tuning)
        {
            RenderSettings.skybox = WorldSky(tuning.skyExposure);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = tuning.ambientColor;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = tuning.fogColor;
            RenderSettings.fogStartDistance = tuning.fogStart;
            RenderSettings.fogEndDistance = tuning.fogEnd;
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

        /// <summary>길을 따라 넓은 골짜기, 그 둘레는 못 오르는 절벽. 관문 둘레에서 골짜기가 좁은 고개가 된다.</summary>
        private static Terrain BuildTerrain(WorldTuning tuning, WorldRoute route, VillageSetup.Area village, Transform parent)
        {
            float reach = tuning.valleyHalfWidth + tuning.cliffWidth + tuning.terrainMargin;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < route.PointCount; i++)
            {
                (float x, float z) = route.Point(i);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
            }
            minX -= reach; maxX += reach; minZ -= reach; maxZ += reach;
            float width = maxX - minX, length = maxZ - minZ;
            float height = tuning.cliffHeight + tuning.groundBumps + 1f;

            // 에셋을 먼저 만든 뒤 높이 · 레이어 · 칠하기를 넣는다 — 메모리에서 칠한 뒤 CreateAsset하면 칠한 값(SplatAlpha)이 파일에 남지 않았다
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath) != null) AssetDatabase.DeleteAsset(TerrainDataPath);
            var data = new TerrainData { heightmapResolution = tuning.heightmapResolution };
            data.size = new Vector3(width, height, length);
            AssetDatabase.CreateAsset(data, TerrainDataPath);

            // 레이어 순서 = alphamap 채널: 0 숲 바닥 · 1 흙길 · 2 절벽 바위 · 3 오염 토양(URP Terrain/Lit 한 패스 4층)
            data.terrainLayers = new[]
            {
                Layer("ForestFloor", GroundTexture("Ground_ForestFloor"), tuning.forestFloorTile, tuning.groundSmoothness),
                Layer("DirtTrail", GroundTexture("Ground_DirtTrail"), tuning.dirtTrailTile, tuning.trailSmoothness),
                Layer("Rock", SolidTexture("Rock", RockColor), 4f, tuning.groundSmoothness),
                Layer("PollutedSoil", GroundTexture("Ground_PollutedSoil"), tuning.pollutedSoilTile, tuning.groundSmoothness),
            };
            data.alphamapResolution = 512;
            FillTerrain(tuning, route, village, data, minX, minZ, width, length, height);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);


            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Terrain";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = new Vector3(minX, 0f, minZ);
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.materialTemplate = TerrainMaterial();
            return terrain;
        }

        /// <summary>
        /// 높이 · 바닥 칠하기. Setup World가 새 지형에, Setup Village가 열린 지형에 부른다(같은 값이면 같은 결과).
        /// width · length · height는 지형 크기(m), minX · minZ는 지형 원점.
        /// </summary>
        internal static void FillTerrain(WorldTuning tuning, WorldRoute route, VillageSetup.Area village, TerrainData data, float minX, float minZ, float width, float length, float height)
        {
            var flatSites = new List<Vector2>();
            foreach (WorldTuning.DungeonSite site in tuning.dungeons)
            {
                (float x, float z) = route.BesideAt(site.routeDistance, site.sideOffset);
                flatSites.Add(new Vector2(x, z));
            }

            int res = data.heightmapResolution;
            var heights = new float[res, res];
            for (int zi = 0; zi < res; zi++)
            {
                float z = minZ + length * zi / (res - 1);
                for (int xi = 0; xi < res; xi++)
                {
                    float x = minX + width * xi / (res - 1);
                    heights[zi, xi] = HeightAt(tuning, route, flatSites, village, x, z) / height;
                }
            }
            data.SetHeights(0, 0, heights);

            int alphaRes = data.alphamapResolution;
            var alpha = new float[alphaRes, alphaRes, 4];
            for (int zi = 0; zi < alphaRes; zi++)
            {
                float z = minZ + length * (zi + 0.5f) / alphaRes;
                for (int xi = 0; xi < alphaRes; xi++)
                {
                    float x = minX + width * (xi + 0.5f) / alphaRes;
                    GroundWeights(tuning, route, flatSites, village, x, z, out float forest, out float trail, out float rock, out float polluted);
                    alpha[zi, xi, 0] = forest;
                    alpha[zi, xi, 1] = trail;
                    alpha[zi, xi, 2] = rock;
                    alpha[zi, xi, 3] = polluted;
                }
            }
            data.SetAlphamaps(0, 0, alpha);
        }

        private static float HeightAt(WorldTuning tuning, WorldRoute route, List<Vector2> flatSites, VillageSetup.Area village, float x, float z)
        {
            float d = route.DistanceFrom(x, z, out float along);
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
        private static Transform BuildEntrance(WorldTuning tuning, WorldRoute route, Terrain terrain, Transform parent, int index, TMP_FontAsset font, out Renderer doorway)
        {
            WorldTuning.DungeonSite site = tuning.dungeons[index];
            Vector3 position = OnGround(terrain, route.BesideAt(site.routeDistance, site.sideOffset));
            Vector3 toPath = OnGround(terrain, route.PointAt(site.routeDistance)) - position;
            toPath.y = 0f;

            var dungeon = new GameObject($"Dungeon{index + 1}").transform;
            dungeon.SetParent(parent, false);
            dungeon.SetPositionAndRotation(position, Quaternion.LookRotation(toPath.normalized));

            // 바위 덩어리 + 어두운 입구(입구 면에는 충돌이 없다 — 들어서는 판정은 거리로)
            Block(dungeon, "Rock", new Vector3(0f, 3f, -3.5f), new Vector3(11f, 8f, 6f), Lit("EntranceRock", new Color(0.36f, 0.34f, 0.32f)), collide: true);
            GameObject door = Block(dungeon, "Doorway", new Vector3(0f, 2f, -0.45f), new Vector3(3.4f, 4f, 0.2f), Lit("Doorway", new Color(0.05f, 0.04f, 0.06f)), collide: false);
            doorway = door.GetComponent<Renderer>();
            GameObject glow = Block(dungeon, "EnterMark", new Vector3(0f, 0.03f, 1.2f), new Vector3(tuning.entranceRadius * 1.6f, 0.02f, tuning.entranceRadius * 1.6f), Lit("EnterMark", new Color(0.35f, 0.6f, 0.9f)), collide: false, PrimitiveType.Cylinder);
            glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Label(dungeon, font, site.displayName, new Vector3(0f, 8.2f, 0f), Quaternion.Euler(0f, 180f, 0f));

            var entrance = new GameObject("Entrance").transform;
            entrance.SetParent(dungeon, false);
            entrance.localPosition = new Vector3(0f, 0f, 1.2f);
            return entrance;
        }

        /// <summary>던전마다 그 뒤 좁은 고개를 막는 관문. 마지막 관문 너머는 다음 지역(준비 중).</summary>
        private static GameObject[] BuildGates(WorldTuning tuning, WorldRoute route, Terrain terrain, TMP_FontAsset font)
        {
            var parent = new GameObject("Gates").transform;
            var gates = new GameObject[tuning.dungeons.Length];
            Material material = TestRoomSetup.LoadOrCreateLit("Assets/Game/Art/Floor/LockedDoor.mat", new Color(0.75f, 0.2f, 0.15f));
            for (int i = 0; i < gates.Length; i++)
            {
                float distance = tuning.GateDistance(i);
                Vector3 center = OnGround(terrain, route.PointAt(distance));
                Vector3 forward = Dir(route.DirectionAt(distance));
                var gate = new GameObject($"Gate{i + 1}").transform;
                gate.SetParent(parent, false);
                gate.SetPositionAndRotation(center, Quaternion.LookRotation(forward));
                float span = tuning.passHalfWidth * 2f + 10f; // 양쪽 절벽 속까지
                Block(gate, "Wall", new Vector3(0f, 2.5f, 0f), new Vector3(span, 7f, 1.5f), material, collide: true);
                // 보이지 않는 벽: 절벽을 점프로 타고 올라 관문을 돌아가지 못하게 지형 끝까지 막는다(길은 남→북으로만 나아가서 다른 골짜기를 자르지 않는다)
                var barrier = new GameObject("Barrier").AddComponent<BoxCollider>();
                barrier.transform.SetParent(gate, false);
                barrier.center = new Vector3(0f, 40f, 0f);
                barrier.size = new Vector3(terrain.terrainData.size.x * 2.5f, 100f, 1.5f);
                string text = i == gates.Length - 1 ? tuning.nextRegionLabel : $"관문 — {tuning.dungeons[i].displayName}을 깨면 열린다";
                Label(gate, font, text, new Vector3(0f, 7.5f, -1f), Quaternion.identity);
                gates[i] = gate.gameObject;
            }
            return gates;
        }

        /// <summary>적 무리 자리마다 자식으로 적이 나올 자리(둥글게).</summary>
        private static Transform[] BuildEncounterSites(WorldTuning tuning, WorldRoute route, Terrain terrain, Transform parent)
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

        /// <summary>골짜기 안 흙길 밖에 회색 도형 나무(줄기 충돌 · 수관). 고정 시드라 매번 같은 자리.</summary>
        /// <summary>
        /// 골짜기 안 흙길 밖에 나무(VARCO 모델 프리팹, 줄기 충돌). 종류는 treeKinds 무게로 섞고, 던전 입구 둘레는 weightNearDungeon.
        /// 고정 시드라 매번 같은 자리. 프리팹이 없는 종류는 회색 도형으로 그린다.
        /// </summary>
        private static void BuildTrees(WorldTuning tuning, WorldRoute route, Terrain terrain, Transform parent, List<Vector3> sites, Transform[] entrances, VillageSetup.Area village)
        {
            TreeSetup.Run();
            var forest = new GameObject("Trees").transform;
            forest.SetParent(parent, false);
            var prefabs = new GameObject[tuning.treeKinds.Length];
            for (int i = 0; i < prefabs.Length; i++) prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(TreeSetup.PrefabPath(tuning.treeKinds[i].id));
            var dungeonSpots = new List<Vector3>();
            foreach (Transform entrance in entrances) dungeonSpots.Add(entrance.position);

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            var random = new System.Random(tuning.treeSeed);
            var taken = new HashSet<Vector2Int>();
            var counts = new int[prefabs.Length];
            int placed = 0, cleared = 0;
            for (int attempt = 0; attempt < tuning.treeCount * 8 && placed < tuning.treeCount; attempt++)
            {
                float x = origin.x + (float)random.NextDouble() * size.x;
                float z = origin.z + (float)random.NextDouble() * size.z;
                float d = route.DistanceFrom(x, z, out float along);
                if (d < tuning.treePathClearance || d > tuning.ValleyHalfWidthAt(along) - 2f) continue;
                if (NearAny(sites, x, z, SiteFlatRadius)) continue;
                var cell = new Vector2Int(Mathf.FloorToInt(x / tuning.treeSpacing), Mathf.FloorToInt(z / tuning.treeSpacing));
                if (!taken.Add(cell)) continue;

                // 나무마다 난수 3번(종류 · 크기 · 회전) — 예전 도형 나무와 같은 횟수라 자리가 그대로다
                bool nearDungeon = NearAny(dungeonSpots, x, z, tuning.treeNearDungeonRadius);
                int kind = PickTreeKind(tuning.treeKinds, nearDungeon, random.NextDouble());
                float scale = Mathf.Lerp(tuning.treeScaleRange.x, tuning.treeScaleRange.y, (float)random.NextDouble());
                Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                Vector3 ground = OnGround(terrain, (x, z));
                // 마을 안 나무는 세우지 않는다. 난수 · 칸 · 번호는 그대로 쓰고 지나가 마을 밖 나무 자리는 마을이 없을 때와 같다(Setup Village가 열린 씬에서 지우는 것과 같은 결과)
                if (village != null && village.ClearsTree(x, z))
                {
                    placed++;
                    cleared++;
                    continue;
                }

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
            Debug.Log($"[WorldSetup] 나무 {placed - cleared}그루{summary} · 마을 자리라 안 세운 나무 {cleared}그루");
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

        /// <summary>지형 가장자리 보이지 않는 벽(떨어지지 않게).</summary>
        private static void BuildBounds(Terrain terrain, Transform parent)
        {
            var bounds = new GameObject("Bounds").transform;
            bounds.SetParent(parent, false);
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 center = origin + new Vector3(size.x * 0.5f, 40f, size.z * 0.5f);
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

        // ---------- 도움 ----------

        private static Vector3 OnGround(Terrain terrain, (float x, float z) point)
        {
            var position = new Vector3(point.x, 0f, point.z);
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

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
