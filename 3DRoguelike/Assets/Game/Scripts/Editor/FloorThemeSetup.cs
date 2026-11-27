using System.Collections.Generic;
using Game.Rules;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 던전 안 모양 테마(`FloorTheme`)의 재질 · 장식 프리팹 · 테마 에셋을 만든다.
    /// - 도시 던전 "무너진 대피소": 바닥 · 벽은 폐허 도시 바닥 텍스처(`Art/City/Ground`), 장식은 도시 소품 프리팹(`Prefabs/World/City`, `CitySetup`이 만든다).
    /// - 숲 던전 1 "무너진 사육장": 바닥은 숲 지형 텍스처(`Art/World/Ground`), 벽은 이끼 낀 잔해 콘크리트, 장식은 숲 나무 3종(`Prefabs/World/Tree_*`, 작게)
    ///   · 단순 도형을 합친 쓰러진 통나무 · 그루터기 · 바위 · 사육장 우리 · 나무 울타리(`Prefabs/World/Forest`) · 도시 바리케이드 · 잔해.
    /// 재질 · 도형 메시 · 장식 프리팹은 매번 같은 값으로 고쳐 쓰고(여러 번 실행해도 같다), 테마 에셋은 없을 때만 만들어 고친 값을 덮어쓰지 않는다.
    /// 던전에 테마를 붙이는 것은 `WorldTuning.dungeons`의 interiorTheme(데이터).
    /// </summary>
    public static class FloorThemeSetup
    {
        public const string ThemeFolder = "Assets/Game/Data/FloorThemes";
        public const string CityThemePath = ThemeFolder + "/CityShelter.asset";
        public const string ForestThemePath = ThemeFolder + "/ForestKennel.asset";
        private const string MaterialFolder = "Assets/Game/Art/Floor/City";
        private const string GroundFolder = "Assets/Game/Art/City/Ground";
        private const string PropFolder = "Assets/Game/Prefabs/World/City";
        private const string ForestMaterialFolder = "Assets/Game/Art/Floor/Forest";
        private const string ForestMeshFolder = "Assets/Game/Art/Floor/Forest/Meshes";
        private const string ForestGroundFolder = "Assets/Game/Art/World/Ground";
        public const string ForestPropFolder = "Assets/Game/Prefabs/World/Forest";
        private const string TreePrefabFolder = "Assets/Game/Prefabs/World";

        /// <summary>재질 표: 이름 · 바닥 텍스처 · 색(텍스처에 곱함). 시작 방 초록 · 보스 방 빨강 · 보물방 노랑은 회색 층과 같은 뜻으로 남긴다.</summary>
        private static readonly (string name, string texture, Color tint)[] Materials =
        {
            ("CityFloor", "City_Asphalt", new Color(0.95f, 0.95f, 0.95f)), // 지하 주차장 대피소: 갈라진 아스팔트 · 노란 줄
            ("CityStartFloor", "City_WeedyPavement", new Color(0.85f, 1f, 0.85f)),
            ("CityBossFloor", "City_Asphalt", new Color(1f, 0.6f, 0.56f)),
            ("CityTreasureFloor", "City_RubbleConcrete", new Color(1f, 0.86f, 0.5f)),
            ("CityWall", "City_RubbleConcrete", new Color(1f, 1f, 1f)), // 갈라진 콘크리트 벽
        };

        /// <summary>
        /// 숲 던전 1 "무너진 사육장" 재질 표: 이름 · 텍스처 경로 · 색 · 금속 · 매끄러움. 정한 것 (Claude, 확인 필요, BALANCE 8-1).
        /// 바닥은 숲 지형 텍스처, 시작 방 = 흙길 초록 · 보스 방 = 흙길 빨강(검은 곰이 밝은 바닥에서 잘 보이게) · 보물방 = 숲 바닥 노랑.
        /// </summary>
        private static readonly (string name, string texture, Color tint, float metallic, float smoothness)[] ForestMaterials =
        {
            ("ForestFloor", ForestGroundFolder + "/Ground_ForestFloor.png", new Color(0.8f, 0.8f, 0.76f), 0f, 0.05f), // 조금 어둡게: 갈색 · 초록 적(멧토끼 · 두꺼비)이 바닥에서 더 도드라지게
            ("ForestStartFloor", ForestGroundFolder + "/Ground_DirtTrail.png", new Color(0.82f, 1f, 0.8f), 0f, 0.05f),
            ("ForestBossFloor", ForestGroundFolder + "/Ground_DirtTrail.png", new Color(1f, 0.7f, 0.64f), 0f, 0.05f),
            ("ForestTreasureFloor", ForestGroundFolder + "/Ground_ForestFloor.png", new Color(1f, 0.88f, 0.52f), 0f, 0.05f),
            ("ForestWall", GroundFolder + "/City_RubbleConcrete.png", new Color(0.76f, 0.82f, 0.7f), 0f, 0.05f), // 이끼 낀 사육장 콘크리트 담
            ("ForestBark", ForestGroundFolder + "/Ground_DirtTrail.png", new Color(0.45f, 0.34f, 0.25f), 0f, 0.05f), // 통나무 · 그루터기 껍질
            ("ForestCutWood", ForestGroundFolder + "/Ground_DirtTrail.png", new Color(0.95f, 0.78f, 0.58f), 0f, 0.05f), // 잘린 면
            ("ForestRock", ForestGroundFolder + "/Ground_PollutedSoil.png", new Color(0.8f, 0.8f, 0.78f), 0f, 0.1f), // 이끼 낀 바위
            ("KennelWood", ForestGroundFolder + "/Ground_DirtTrail.png", new Color(0.62f, 0.5f, 0.38f), 0f, 0.05f), // 낡은 울타리 판자
            ("KennelMetal", null, new Color(0.38f, 0.3f, 0.25f), 0.55f, 0.35f), // 녹슨 우리 쇠창살
            ("KennelTray", null, new Color(0.2f, 0.19f, 0.17f), 0.3f, 0.2f), // 우리 바닥 판
        };

        [MenuItem("Tools/Game/Setup Floor Themes")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/City_Barrier.prefab") == null) CitySetup.Run();
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{TreePrefabFolder}/Tree_Oak.prefab") == null) TreeSetup.Run();
            EnsureFolder("Assets/Game/Art/Floor", "City");
            EnsureFolder("Assets/Game/Art/Floor", "Forest");
            EnsureFolder("Assets/Game/Art/Floor/Forest", "Meshes");
            EnsureFolder("Assets/Game/Prefabs/World", "Forest");
            EnsureFolder("Assets/Game/Data", "FloorThemes");

            foreach ((string name, string texture, Color tint) in Materials) WriteMaterial($"{MaterialFolder}/{name}.mat", $"{GroundFolder}/{texture}.png", tint, 0f, 0.05f);
            foreach ((string name, string texture, Color tint, float metallic, float smoothness) in ForestMaterials)
                WriteMaterial($"{ForestMaterialFolder}/{name}.mat", texture, tint, metallic, smoothness);
            AssetDatabase.SaveAssets();
            BuildForestProps();
            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadAssetAtPath<FloorTheme>(CityThemePath) == null)
            {
                var theme = ScriptableObject.CreateInstance<FloorTheme>();
                FillCityShelter(theme);
                AssetDatabase.CreateAsset(theme, CityThemePath);
                AssetDatabase.SaveAssets();
            }
            if (AssetDatabase.LoadAssetAtPath<FloorTheme>(ForestThemePath) == null)
            {
                var theme = ScriptableObject.CreateInstance<FloorTheme>();
                FillForestKennel(theme);
                AssetDatabase.CreateAsset(theme, ForestThemePath);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[FloorThemeSetup] 던전 테마 재질 {Materials.Length + ForestMaterials.Length}개 · 숲 장식 프리팹 {ForestProps.Length + TreeProps.Length}개 · {CityThemePath} · {ForestThemePath}");
        }

        public static FloorTheme LoadCityTheme() => AssetDatabase.LoadAssetAtPath<FloorTheme>(CityThemePath);
        public static FloorTheme LoadForestTheme() => AssetDatabase.LoadAssetAtPath<FloorTheme>(ForestThemePath);

        /// <summary>도시 던전 — 무너진 대피소 첫 값(BALANCE 8-1). 정한 것 (Claude, 확인 필요).</summary>
        private static void FillCityShelter(FloorTheme theme)
        {
            theme.displayName = "무너진 대피소";
            theme.floor = Mat("CityFloor");
            theme.startFloor = Mat("CityStartFloor");
            theme.bossFloor = Mat("CityBossFloor");
            theme.treasureFloor = Mat("CityTreasureFloor");
            theme.wall = Mat("CityWall");
            theme.floorTile = 8f;
            theme.wallTile = 6f;

            theme.ambientColor = new Color(0.4f, 0.4f, 0.42f);
            theme.fog = true;
            theme.fogColor = new Color(0.27f, 0.26f, 0.25f);
            theme.fogStart = 14f;
            theme.fogEnd = 70f;
            theme.sunColor = new Color(1f, 0.92f, 0.82f);
            theme.sunIntensity = 0.8f;

            theme.props = new[]
            {
                // 벽 앞: 정면이 방 안쪽, 조금만 흔듦(띠 깊이 3.2 m 안에 들어가게)
                Prop("City_Barrier", 30f, DressingPlace.Wall, 1f, 0f, 6f),
                Prop("City_Barricade", 25f, DressingPlace.Wall, 0.9f, 0f, 8f),
                Prop("City_Debris", 25f, DressingPlace.Wall, 0.6f, 0f, 8f),
                Prop("City_Car", 12f, DressingPlace.Wall, 1f, 90f, 4f), // 긴 변이 벽을 따라
                // 방 안 엄폐물: 아무 방향
                Prop("City_Barrier", 40f, DressingPlace.Open, 1f, 0f, 180f),
                Prop("City_Debris", 30f, DressingPlace.Open, 0.75f, 0f, 180f),
                Prop("City_Car", 15f, DressingPlace.Open, 1f, 0f, 180f),
                Prop("City_Barricade", 15f, DressingPlace.Open, 0.9f, 0f, 180f),
            };
            theme.wallPropsPerCell = new Vector2Int(4, 6);
            theme.openPropsPerCell = new Vector2Int(1, 3);
            theme.wallBandDepth = 3.2f;
            theme.doorClearance = 4f;
            theme.spawnClearance = 2f;
            theme.centerClearance = 4f;
            theme.laneHalfWidth = 2.5f;
            theme.propGap = 0.4f;
            theme.openWallMargin = 5f;
        }

        /// <summary>
        /// 숲 던전 1 — 무너진 사육장 첫 값(BALANCE 8-1). 정한 것 (Claude, 확인 필요).
        /// 숲이 삼킨 사육장 마당: 벽 앞에 작은 나무 · 우리 · 울타리 · 통나무, 방 안에는 낮은 엄폐물(바위 · 통나무 · 그루터기 · 우리)만 — 곰 돌진 · 시야를 덜 가리게.
        /// </summary>
        private static void FillForestKennel(FloorTheme theme)
        {
            theme.displayName = "무너진 사육장";
            theme.floor = ForestMat("ForestFloor");
            theme.startFloor = ForestMat("ForestStartFloor");
            theme.bossFloor = ForestMat("ForestBossFloor");
            theme.treasureFloor = ForestMat("ForestTreasureFloor");
            theme.wall = ForestMat("ForestWall");
            theme.floorTile = 6f;
            theme.wallTile = 6f;

            // 숲 속 반실외: 나뭇잎 사이로 드는 따뜻한 해, 초록빛 앰비언트 · 옅은 안개
            theme.ambientColor = new Color(0.5f, 0.54f, 0.48f);
            theme.fog = true;
            theme.fogColor = new Color(0.42f, 0.48f, 0.42f);
            theme.fogStart = 20f;
            theme.fogEnd = 90f;
            theme.sunColor = new Color(1f, 0.95f, 0.84f);
            theme.sunIntensity = 1.05f;

            theme.props = new[]
            {
                // 벽 앞: 정면이 방 안쪽, 긴 변이 벽을 따라
                Prop(ForestPropFolder, "Forest_TreeOak", 14f, DressingPlace.Wall, 1f, 0f, 180f),
                Prop(ForestPropFolder, "Forest_TreePine", 10f, DressingPlace.Wall, 1f, 0f, 180f),
                Prop(ForestPropFolder, "Forest_TreeMutant", 8f, DressingPlace.Wall, 1f, 0f, 180f),
                Prop(ForestPropFolder, "Kennel_Cage", 14f, DressingPlace.Wall, 1f, 0f, 6f),
                Prop(ForestPropFolder, "Kennel_Fence", 16f, DressingPlace.Wall, 1f, 0f, 4f),
                Prop(ForestPropFolder, "Forest_Log", 10f, DressingPlace.Wall, 1f, 0f, 8f),
                Prop(ForestPropFolder, "Forest_Rock", 10f, DressingPlace.Wall, 1f, 0f, 20f),
                Prop(PropFolder, "City_Barricade", 8f, DressingPlace.Wall, 0.9f, 0f, 8f), // 판자 · 녹슨 철판 바리케이드
                Prop(PropFolder, "City_Debris", 6f, DressingPlace.Wall, 0.6f, 0f, 8f), // 무너진 담 잔해
                // 방 안 엄폐물: 낮은 것만, 아무 방향
                Prop(ForestPropFolder, "Forest_Rock", 30f, DressingPlace.Open, 1f, 0f, 180f),
                Prop(ForestPropFolder, "Forest_Log", 25f, DressingPlace.Open, 1f, 0f, 180f),
                Prop(ForestPropFolder, "Forest_Stump", 15f, DressingPlace.Open, 1f, 0f, 180f),
                Prop(ForestPropFolder, "Kennel_Cage", 15f, DressingPlace.Open, 1f, 0f, 180f),
            };
            theme.wallPropsPerCell = new Vector2Int(4, 6);
            theme.openPropsPerCell = new Vector2Int(1, 3);
            theme.wallBandDepth = 3.2f;
            theme.doorClearance = 4f;
            theme.spawnClearance = 2f;
            theme.centerClearance = 4f;
            theme.laneHalfWidth = 2.5f;
            theme.propGap = 0.4f;
            theme.openWallMargin = 5f;
        }

        private static FloorTheme.Prop Prop(string id, float weight, DressingPlace place, float scale, float yawOffset, float yawJitter)
        {
            return Prop(PropFolder, id, weight, place, scale, yawOffset, yawJitter);
        }

        private static FloorTheme.Prop Prop(string folder, string id, float weight, DressingPlace place, float scale, float yawOffset, float yawJitter)
        {
            return new FloorTheme.Prop
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{id}.prefab"),
                weight = weight,
                place = place,
                scale = scale,
                yawOffset = yawOffset,
                yawJitter = yawJitter,
            };
        }

        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
        private static Material ForestMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{ForestMaterialFolder}/{name}.mat");

        /// <summary>URP Lit, 바닥 텍스처(없으면 색만) + 색. 바닥 · 벽 매끄러움 0.05(숲 · 도시 바닥과 같은 이유: 높으면 하늘이 비쳐 희게 보임).</summary>
        private static void WriteMaterial(string path, string texturePath, Color tint, float metallic, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            var map = texturePath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) : null;
            material.SetTexture("_BaseMap", map);
            material.SetTexture("_MainTex", map);
            material.SetColor("_BaseColor", tint);
            material.SetColor("_Color", tint);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Glossiness", smoothness);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
        }

        // --- 숲 던전 장식 프리팹 ---

        /// <summary>단순 도형 하나: 모양 · 자리 · 회전(도) · 크기 · 재질 번호(그 소품의 재질 목록 안).</summary>
        private struct Part
        {
            public PrimitiveType type;
            public Vector3 position;
            public Vector3 euler;
            public Vector3 scale;
            public int material;

            public Part(PrimitiveType type, Vector3 position, Vector3 euler, Vector3 scale, int material = 0)
            {
                this.type = type;
                this.position = position;
                this.euler = euler;
                this.scale = scale;
                this.material = material;
            }
        }

        /// <summary>숲 나무 3종을 방 크기로 줄여 쓴다: 이름 · 원래 나무 프리팹 · 배율. 충돌 · 바닥 자리는 줄기 둘레 상자(가지 · 잎은 위로 벽을 넘어 퍼짐).</summary>
        private static readonly (string name, string tree, float scale)[] TreeProps =
        {
            ("Forest_TreeOak", "Tree_Oak", 0.7f),
            ("Forest_TreePine", "Tree_Pine", 0.6f),
            ("Forest_TreeMutant", "Tree_Mutant", 0.75f),
        };
        private static readonly Vector3 TreeTrunkBox = new Vector3(1.2f, 3f, 1.2f);

        /// <summary>도형을 합친 소품: 이름 · 재질 목록 · 충돌 상자(= 바닥 자리, 바닥 가운데 피벗, 정면 -Z) · 도형.</summary>
        private static readonly (string name, string[] materials, Vector3 box, Part[] parts)[] ForestProps =
        {
            ("Forest_Log", new[] { "ForestBark", "ForestCutWood" }, new Vector3(3.6f, 0.75f, 0.8f), LogParts()),
            ("Forest_Stump", new[] { "ForestBark", "ForestCutWood" }, new Vector3(1.2f, 0.62f, 1.2f), StumpParts()),
            ("Forest_Rock", new[] { "ForestRock" }, new Vector3(2.2f, 1.1f, 1.7f), RockParts()),
            ("Kennel_Fence", new[] { "KennelWood" }, new Vector3(3f, 1.5f, 0.3f), FenceParts()),
            ("Kennel_Cage", new[] { "KennelMetal", "KennelTray" }, new Vector3(2f, 1.5f, 1.6f), CageParts()),
        };

        private static Part[] LogParts()
        {
            var cylinder = PrimitiveType.Cylinder;
            return new[]
            {
                new Part(cylinder, new Vector3(0f, 0.34f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.68f, 1.78f, 0.68f)), // 줄기 3.56 m
                new Part(cylinder, new Vector3(-1.2f, 0.3f, 0.02f), new Vector3(0f, 0f, 90f), new Vector3(0.74f, 0.35f, 0.74f)), // 뿌리 쪽 굵게
                new Part(cylinder, new Vector3(0.55f, 0.62f, 0.12f), new Vector3(25f, 0f, -35f), new Vector3(0.14f, 0.28f, 0.14f)), // 부러진 가지
                new Part(cylinder, new Vector3(-0.35f, 0.5f, -0.24f), new Vector3(-50f, 0f, 15f), new Vector3(0.12f, 0.2f, 0.12f)),
                new Part(cylinder, new Vector3(1.785f, 0.34f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.6f, 0.01f, 0.6f), 1), // 잘린 면
                new Part(cylinder, new Vector3(-1.565f, 0.3f, 0.02f), new Vector3(0f, 0f, 90f), new Vector3(0.66f, 0.01f, 0.66f), 1),
            };
        }

        private static Part[] StumpParts()
        {
            var parts = new List<Part>
            {
                new Part(PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(0.82f, 0.3f, 0.82f)),
                new Part(PrimitiveType.Cylinder, new Vector3(0f, 0.602f, 0f), Vector3.zero, new Vector3(0.74f, 0.01f, 0.74f), 1), // 나이테 면
            };
            for (int i = 0; i < 4; i++)
            {
                float yaw = 20f + i * 90f;
                Vector3 outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                parts.Add(new Part(PrimitiveType.Cube, outward * 0.42f + Vector3.up * 0.1f, new Vector3(-25f, yaw, 0f), new Vector3(0.22f, 0.2f, 0.5f))); // 뿌리
            }
            return parts.ToArray();
        }

        private static Part[] RockParts()
        {
            var sphere = PrimitiveType.Sphere;
            return new[]
            {
                new Part(sphere, new Vector3(0f, 0.48f, 0f), new Vector3(8f, 20f, 6f), new Vector3(1.6f, 1.08f, 1.3f)),
                new Part(sphere, new Vector3(0.62f, 0.28f, 0.28f), new Vector3(10f, 60f, 0f), new Vector3(0.9f, 0.62f, 0.78f)),
                new Part(sphere, new Vector3(-0.68f, 0.2f, -0.3f), new Vector3(-6f, 35f, 12f), new Vector3(0.72f, 0.48f, 0.6f)),
            };
        }

        private static Part[] FenceParts()
        {
            var cube = PrimitiveType.Cube;
            var parts = new List<Part>();
            foreach (float x in new[] { -1.42f, 0f, 1.42f }) parts.Add(new Part(cube, new Vector3(x, 0.72f, 0.06f), Vector3.zero, new Vector3(0.14f, 1.44f, 0.14f))); // 기둥
            parts.Add(new Part(cube, new Vector3(0f, 0.38f, 0.1f), Vector3.zero, new Vector3(2.96f, 0.09f, 0.06f))); // 가로대
            parts.Add(new Part(cube, new Vector3(0f, 1.1f, 0.1f), new Vector3(0f, 0f, -1.5f), new Vector3(2.96f, 0.09f, 0.06f)));
            for (int i = 0; i < 10; i++)
            {
                if (i == 3 || i == 4) continue; // 빠진 판자(무너진 울타리)
                float x = -1.3f + i * 0.29f;
                float tilt = i == 7 ? 11f : 0f;
                float height = i == 6 ? 0.9f : 1.24f; // 부러진 판자
                parts.Add(new Part(cube, new Vector3(x, height * 0.5f + 0.04f, 0.0f), new Vector3(0f, 0f, tilt), new Vector3(0.22f, height, 0.04f)));
            }
            parts.Add(new Part(cube, new Vector3(-0.35f, 0.06f, -0.08f), new Vector3(0f, 18f, 84f), new Vector3(0.22f, 1.2f, 0.04f))); // 떨어진 판자
            return parts.ToArray();
        }

        private static Part[] CageParts()
        {
            const float w = 2f, h = 1.5f, d = 1.6f, frame = 0.06f, bar = 0.028f;
            var cube = PrimitiveType.Cube;
            var parts = new List<Part>
            {
                new Part(cube, new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(w - 0.04f, 0.05f, d - 0.04f), 1), // 바닥 판
            };
            float hx = w * 0.5f - frame * 0.5f, hz = d * 0.5f - frame * 0.5f;
            foreach (float x in new[] { -hx, hx })
                foreach (float z in new[] { -hz, hz })
                    parts.Add(new Part(cube, new Vector3(x, h * 0.5f, z), Vector3.zero, new Vector3(frame, h, frame))); // 모서리 기둥
            foreach (float y in new[] { frame * 0.5f, h - frame * 0.5f })
            {
                foreach (float z in new[] { -hz, hz }) parts.Add(new Part(cube, new Vector3(0f, y, z), Vector3.zero, new Vector3(w, frame, frame)));
                foreach (float x in new[] { -hx, hx }) parts.Add(new Part(cube, new Vector3(x, y, 0f), Vector3.zero, new Vector3(frame, frame, d)));
            }
            for (int i = 0; i < 9; i++) parts.Add(new Part(cube, new Vector3(-0.8f + i * 0.2f, h * 0.5f, hz), Vector3.zero, new Vector3(bar, h, bar))); // 뒤 창살
            for (int i = 0; i < 7; i++)
                foreach (float x in new[] { -hx, hx }) parts.Add(new Part(cube, new Vector3(x, h * 0.5f, -0.6f + i * 0.2f), Vector3.zero, new Vector3(bar, h, bar))); // 옆 창살
            for (int i = 0; i < 5; i++) parts.Add(new Part(cube, new Vector3(0f, h - frame * 0.5f, -0.55f + i * 0.275f), Vector3.zero, new Vector3(w, bar, bar))); // 지붕 창살
            // 앞: 오른쪽 반만 창살, 하나는 휘어짐 — 문이 뜯겨 나간 우리
            for (int i = 0; i < 4; i++)
            {
                float x = 0.2f + i * 0.2f;
                parts.Add(new Part(cube, new Vector3(x, h * 0.5f, -hz), new Vector3(0f, 0f, i == 0 ? 14f : 0f), new Vector3(bar, h, bar)));
            }
            parts.Add(new Part(cube, new Vector3(-0.5f, h * 0.5f, -hz), Vector3.zero, new Vector3(1f - frame, 0.04f, 0.04f))); // 문 가로대만 남음
            return parts.ToArray();
        }

        private static void BuildForestProps()
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
                | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;

            foreach ((string name, string tree, float scale) in TreeProps)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{TreePrefabFolder}/{tree}.prefab");
                var root = new GameObject(name);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                instance.name = "Tree";
                instance.transform.localScale = Vector3.one * scale;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, TreeTrunkBox.y * 0.5f, 0f);
                collider.size = TreeTrunkBox;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                PrefabUtility.SaveAsPrefabAsset(root, $"{ForestPropFolder}/{name}.prefab");
                Object.DestroyImmediate(root);
            }

            foreach ((string name, string[] materialNames, Vector3 box, Part[] parts) in ForestProps)
            {
                Mesh mesh = WriteMesh($"{ForestMeshFolder}/{name}.asset", name, parts, materialNames.Length);
                var root = new GameObject(name);
                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = model.AddComponent<MeshRenderer>();
                var materials = new Material[materialNames.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = ForestMat(materialNames[i]);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, box.y * 0.5f, 0f);
                collider.size = box;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                PrefabUtility.SaveAsPrefabAsset(root, $"{ForestPropFolder}/{name}.prefab");
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>도형을 재질마다 하위 메시로 합쳐 메시 에셋에 쓴다(있으면 내용만 바꿔 GUID 그대로).</summary>
        private static Mesh WriteMesh(string path, string name, Part[] parts, int materialCount)
        {
            var builtin = new Dictionary<PrimitiveType, Mesh>();
            Mesh Builtin(PrimitiveType type)
            {
                if (builtin.TryGetValue(type, out Mesh found)) return found;
                var temp = GameObject.CreatePrimitive(type);
                Mesh shared = temp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(temp);
                builtin[type] = shared;
                return shared;
            }

            var perMaterial = new CombineInstance[materialCount];
            var temporary = new List<Mesh>();
            for (int m = 0; m < materialCount; m++)
            {
                var instances = new List<CombineInstance>();
                foreach (Part part in parts)
                {
                    if (part.material != m) continue;
                    instances.Add(new CombineInstance { mesh = Builtin(part.type), transform = Matrix4x4.TRS(part.position, Quaternion.Euler(part.euler), part.scale) });
                }
                var merged = new Mesh();
                merged.CombineMeshes(instances.ToArray(), true, true);
                temporary.Add(merged);
                perMaterial[m] = new CombineInstance { mesh = merged, transform = Matrix4x4.identity };
            }
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(perMaterial, false, false);
            mesh.RecalculateBounds();
            foreach (Mesh t in temporary) Object.DestroyImmediate(t);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
