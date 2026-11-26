using Game.Rules;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 던전 안 모양 테마(`FloorTheme`)의 재질과 테마 에셋을 만든다. 지금은 도시 던전 "무너진 대피소" 하나 —
    /// 바닥 · 벽은 폐허 도시 바닥 텍스처(`Art/City/Ground`), 장식은 도시 소품 프리팹(`Prefabs/World/City`, `CitySetup`이 만든다).
    /// 재질은 매번 같은 값으로 고쳐 쓰고(여러 번 실행해도 같다), 테마 에셋은 없을 때만 만들어 고친 값을 덮어쓰지 않는다.
    /// 던전에 테마를 붙이는 것은 `WorldTuning.dungeons`의 interiorTheme(데이터).
    /// </summary>
    public static class FloorThemeSetup
    {
        public const string ThemeFolder = "Assets/Game/Data/FloorThemes";
        public const string CityThemePath = ThemeFolder + "/CityShelter.asset";
        private const string MaterialFolder = "Assets/Game/Art/Floor/City";
        private const string GroundFolder = "Assets/Game/Art/City/Ground";
        private const string PropFolder = "Assets/Game/Prefabs/World/City";

        /// <summary>재질 표: 이름 · 바닥 텍스처 · 색(텍스처에 곱함). 시작 방 초록 · 보스 방 빨강 · 보물방 노랑은 회색 층과 같은 뜻으로 남긴다.</summary>
        private static readonly (string name, string texture, Color tint)[] Materials =
        {
            ("CityFloor", "City_Asphalt", new Color(0.95f, 0.95f, 0.95f)), // 지하 주차장 대피소: 갈라진 아스팔트 · 노란 줄
            ("CityStartFloor", "City_WeedyPavement", new Color(0.85f, 1f, 0.85f)),
            ("CityBossFloor", "City_Asphalt", new Color(1f, 0.6f, 0.56f)),
            ("CityTreasureFloor", "City_RubbleConcrete", new Color(1f, 0.86f, 0.5f)),
            ("CityWall", "City_RubbleConcrete", new Color(1f, 1f, 1f)), // 갈라진 콘크리트 벽
        };

        [MenuItem("Tools/Game/Setup Floor Themes")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/City_Barrier.prefab") == null) CitySetup.Run();
            EnsureFolder("Assets/Game/Art/Floor", "City");
            EnsureFolder("Assets/Game/Data", "FloorThemes");

            foreach ((string name, string texture, Color tint) in Materials) WriteMaterial(name, texture, tint);
            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadAssetAtPath<FloorTheme>(CityThemePath) == null)
            {
                var theme = ScriptableObject.CreateInstance<FloorTheme>();
                FillCityShelter(theme);
                AssetDatabase.CreateAsset(theme, CityThemePath);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[FloorThemeSetup] 던전 테마 재질 {Materials.Length}개 · {CityThemePath}");
        }

        public static FloorTheme LoadCityTheme() => AssetDatabase.LoadAssetAtPath<FloorTheme>(CityThemePath);

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

        private static FloorTheme.Prop Prop(string id, float weight, DressingPlace place, float scale, float yawOffset, float yawJitter)
        {
            return new FloorTheme.Prop
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/{id}.prefab"),
                weight = weight,
                place = place,
                scale = scale,
                yawOffset = yawOffset,
                yawJitter = yawJitter,
            };
        }

        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");

        /// <summary>URP Lit, 바닥 텍스처 + 색, 금속 0 · 매끄러움 0.05(숲 · 도시 바닥과 같은 이유: 높으면 하늘이 비쳐 희게 보임).</summary>
        private static void WriteMaterial(string name, string texture, Color tint)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundFolder}/{texture}.png");
            material.SetTexture("_BaseMap", map);
            material.SetTexture("_MainTex", map);
            material.SetColor("_BaseColor", tint);
            material.SetColor("_Color", tint);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.05f);
            material.SetFloat("_Glossiness", 0.05f);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
