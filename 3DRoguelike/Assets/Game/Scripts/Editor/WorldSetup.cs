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
        private const string NavMeshPath = ArtFolder + "/WorldNavMesh.asset";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Player.prefab";
        private const string EnemyPrefabPath = TestRoomSetup.EnemyPrefabPath;
        private const float SiteFlatRadius = 14f;

        [MenuItem("Tools/Game/Setup World")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) == null)
            {
                TestRoomSetup.Run(); // 플레이어 · Mutant 프리팹을 만든다
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CurrentRun.FloorScene) == null) FloorSetup.Run(); // 던전(층) 씬
            if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Game/Art", "World");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 새 씬을 연 뒤에 불러온다(FloorSetup과 같은 이유: 앞에서 만든 에셋 참조가 끊기지 않게)
            WorldTuning tuning = LoadOrCreateTuning();
            WorldRoute route = tuning.ToRoute();
            TestRoomSetup.BuildLighting();
            BuildAtmosphere();

            var root = new GameObject("World");
            Terrain terrain = BuildTerrain(tuning, route, root.transform);
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

            BuildTrees(tuning, route, terrain, root.transform, sites);

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
            fields.FindProperty("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
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

        private static void BuildAtmosphere()
        {
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.6f, 0.66f, 0.62f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 320f;
        }

        // ---------- 지형 ----------

        /// <summary>길을 따라 넓은 골짜기, 그 둘레는 못 오르는 절벽. 관문 둘레에서 골짜기가 좁은 고개가 된다.</summary>
        private static Terrain BuildTerrain(WorldTuning tuning, WorldRoute route, Transform parent)
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

            var flatSites = new List<Vector2>();
            foreach (WorldTuning.DungeonSite site in tuning.dungeons)
            {
                (float x, float z) = route.BesideAt(site.routeDistance, site.sideOffset);
                flatSites.Add(new Vector2(x, z));
            }

            var data = new TerrainData { heightmapResolution = tuning.heightmapResolution };
            data.size = new Vector3(width, height, length);
            int res = data.heightmapResolution;
            var heights = new float[res, res];
            for (int zi = 0; zi < res; zi++)
            {
                float z = minZ + length * zi / (res - 1);
                for (int xi = 0; xi < res; xi++)
                {
                    float x = minX + width * xi / (res - 1);
                    heights[zi, xi] = HeightAt(tuning, route, flatSites, x, z) / height;
                }
            }
            data.SetHeights(0, 0, heights);

            data.terrainLayers = new[]
            {
                Layer("Grass", new Color(0.33f, 0.42f, 0.24f)),
                Layer("Dirt", new Color(0.45f, 0.36f, 0.25f)),
                Layer("Rock", new Color(0.42f, 0.41f, 0.39f)),
            };
            data.alphamapResolution = 512;
            int alphaRes = data.alphamapResolution;
            var alpha = new float[alphaRes, alphaRes, 3];
            for (int zi = 0; zi < alphaRes; zi++)
            {
                float z = minZ + length * (zi + 0.5f) / alphaRes;
                for (int xi = 0; xi < alphaRes; xi++)
                {
                    float x = minX + width * (xi + 0.5f) / alphaRes;
                    float d = route.DistanceFrom(x, z, out float along);
                    float half = tuning.ValleyHalfWidthAt(along);
                    float dirt = 1f - Smooth01((d - tuning.pathHalfWidth) / 1.5f);
                    float rock = Smooth01((d - half - 1f) / 3f);
                    float grass = Mathf.Max(0f, 1f - dirt - rock);
                    float sum = dirt + rock + grass;
                    alpha[zi, xi, 0] = grass / sum;
                    alpha[zi, xi, 1] = dirt / sum;
                    alpha[zi, xi, 2] = rock / sum;
                }
            }
            data.SetAlphamaps(0, 0, alpha);

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath) != null) AssetDatabase.DeleteAsset(TerrainDataPath);
            AssetDatabase.CreateAsset(data, TerrainDataPath);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Terrain";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = new Vector3(minX, 0f, minZ);
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.materialTemplate = TerrainMaterial();
            return terrain;
        }

        private static float HeightAt(WorldTuning tuning, WorldRoute route, List<Vector2> flatSites, float x, float z)
        {
            float d = route.DistanceFrom(x, z, out float along);
            float half = tuning.ValleyHalfWidthAt(along);
            float cliff = Smooth01((d - half) / tuning.cliffWidth) * tuning.cliffHeight;

            float nearSite = float.MaxValue;
            foreach (Vector2 site in flatSites) nearSite = Mathf.Min(nearSite, Vector2.Distance(site, new Vector2(x, z)));
            // 길 · 입구 근처는 평평하게, 골짜기 안쪽만 완만한 굴곡
            float weight = Smooth01((d - tuning.pathHalfWidth - 2f) / 12f) * Smooth01((nearSite - SiteFlatRadius) / 10f);
            float bumps = Mathf.PerlinNoise(x * tuning.bumpScale + 100f, z * tuning.bumpScale + 100f) * tuning.groundBumps * weight;
            return cliff + bumps;
        }

        private static TerrainLayer Layer(string name, Color color)
        {
            string texturePath = $"{ArtFolder}/{name}.asset";
            string layerPath = $"{ArtFolder}/{name}.terrainlayer";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = name };
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
                texture.SetPixels(pixels);
                texture.Apply();
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(4f, 4f);
            EditorUtility.SetDirty(layer);
            return layer;
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
        private static void BuildTrees(WorldTuning tuning, WorldRoute route, Terrain terrain, Transform parent, List<Vector3> sites)
        {
            var forest = new GameObject("Trees").transform;
            forest.SetParent(parent, false);
            Material trunk = Lit("TreeTrunk", new Color(0.32f, 0.24f, 0.17f));
            Material[] leaves = { Lit("TreeLeaves", new Color(0.18f, 0.32f, 0.16f)), Lit("TreeLeavesDark", new Color(0.13f, 0.25f, 0.14f)) };

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            var random = new System.Random(tuning.treeSeed);
            var taken = new HashSet<Vector2Int>();
            int placed = 0;
            for (int attempt = 0; attempt < tuning.treeCount * 8 && placed < tuning.treeCount; attempt++)
            {
                float x = origin.x + (float)random.NextDouble() * size.x;
                float z = origin.z + (float)random.NextDouble() * size.z;
                float d = route.DistanceFrom(x, z, out float along);
                if (d < tuning.treePathClearance || d > tuning.ValleyHalfWidthAt(along) - 2f) continue;
                if (NearAny(sites, x, z, SiteFlatRadius)) continue;
                var cell = new Vector2Int(Mathf.FloorToInt(x / tuning.treeSpacing), Mathf.FloorToInt(z / tuning.treeSpacing));
                if (!taken.Add(cell)) continue;

                float scale = 0.8f + (float)random.NextDouble() * 0.5f;
                Vector3 ground = OnGround(terrain, (x, z));
                var tree = new GameObject($"Tree{placed}").transform;
                tree.SetParent(forest, false);
                tree.position = ground;
                tree.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                Block(tree, "Trunk", new Vector3(0f, 3f * scale, 0f), new Vector3(0.6f * scale, 3f * scale, 0.6f * scale), trunk, collide: true, PrimitiveType.Cylinder);
                float crown = (3.5f + (float)random.NextDouble() * 1.5f) * scale;
                Block(tree, "Crown", new Vector3(0f, 6f * scale + crown * 0.3f, 0f), new Vector3(crown, crown * 1.2f, crown), leaves[placed % 2], collide: false, PrimitiveType.Sphere);
                placed++;
            }
            Debug.Log($"[WorldSetup] 나무 {placed}그루");
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
        private static void Label(Transform parent, TMP_FontAsset font, string text, Vector3 localPosition, Quaternion localRotation)
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
