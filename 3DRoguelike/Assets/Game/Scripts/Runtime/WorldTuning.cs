using System;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 월드(고정): 진행 길 · 지역(숲 → 폐허 도시 → …) · 골짜기 지형 · 나무 · 던전 입구 · 관문 · 길 위 적 무리. 값의 기준은 BALANCE.md 13번.
    /// 월드는 판마다 같고(결정 2026-11-11, 사용자) `Tools/Game/Setup World`가 이 값으로 씬을 세운다. 던전 안은 판 시드로 생성된다.
    /// 새 던전 = dungeons에 한 줄(길 순서대로), 새 적 무리 = encounters에 한 줄, 새 지역 = regions에 한 줄(+ 길 점).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/World Tuning")]
    public sealed class WorldTuning : ScriptableObject
    {
        [Serializable]
        public struct DungeonSite
        {
            public string displayName;
            [Tooltip("길 시작에서 입구까지 길 거리(m)")]
            [Min(0f)] public float routeDistance;
            [Tooltip("길 가운데에서 옆으로(m). +면 나아가는 방향의 오른쪽")]
            public float sideOffset;
            [Tooltip("입구 건물 프리팹(Assets/...). 비면 바위 입구(숲). 프리팹 정면(-Z)이 길을 보고, 정면이 입구 자리에 온다")]
            public string entrancePrefab;
            [Tooltip("입구 건물 안 어두운 문 면(깬 던전은 회색) 가운데 — 프리팹 좌표(m)")]
            public Vector3 doorwayCenter;
            [Tooltip("어두운 문 면 폭 · 높이(m)")]
            public Vector2 doorwaySize;

            public bool HasEntrancePrefab => !string.IsNullOrEmpty(entrancePrefab);
        }

        [Serializable]
        public struct EncounterSite
        {
            [Min(0f)] public float routeDistance;
            public float sideOffset;
            [Min(1)] public int count;
        }

        /// <summary>
        /// 나무 자리를 고를 때 쓰는 고정 기준(예전 숲 길 · 관문 · 나무를 비운 자리). 월드 길 · 던전 · 무리를 바꿔도 나무 난수 흐름이 그대로여서
        /// 남은 숲 나무 자리가 바뀌지 않는다(지금 길 밖 · 다른 지역 나무는 지역 나무 비율로 빠진다). 비우면 지금 길 · 관문 · 자리를 쓴다.
        /// </summary>
        [Serializable]
        public struct TreeLayoutReference
        {
            [Tooltip("나무 자리 길(x, z). 비면 route")]
            public Vector2[] route;
            [Tooltip("골짜기가 좁아지는 관문 길 거리(m). 비면 지금 관문")]
            public float[] passDistances;
            [Tooltip("둘레 14 m에 나무를 두지 않는 자리(x, z). 지금 입구 · 시작점 · 무리 자리에 더해진다")]
            public Vector2[] keepOut;
        }

        [Serializable]
        public struct TreeKind
        {
            [Tooltip("프리팹 이름 Tree_{id}")]
            public string id;
            [Tooltip("섞는 무게(비율)")]
            [Min(0f)] public float weight;
            [Tooltip("던전 입구 둘레(treeNearDungeonRadius 안)에서의 무게")]
            [Min(0f)] public float weightNearDungeon;
        }
        /// <summary>프리팹 하나와 섞는 무게. 프리팹은 지역의 prefabFolder 안 {id}.prefab.</summary>
        [Serializable]
        public struct Pick
        {
            public string id;
            [Min(0f)] public float weight;
        }

        /// <summary>길가 건물 한 줄(길 양옆). 건물 정면(-Z)이 길을 본다.</summary>
        [Serializable]
        public struct StreetRow
        {
            [Tooltip("길 가운데에서 건물 정면까지(m)")]
            [Min(0f)] public float frontOffset;
            [Tooltip("정면 자리를 앞뒤로 흔드는 폭(±m)")]
            [Min(0f)] public float offsetJitter;
            [Tooltip("건물 사이 틈 최소 · 최대(m)")]
            public Vector2 gap;
            [Tooltip("건물 대신 빈 터로 두는 비율(0~1)")]
            [Range(0f, 1f)] public float emptyChance;
            [Tooltip("길을 바라보는 각도를 흔드는 폭(±도)")]
            [Min(0f)] public float yawJitter;
            public Pick[] kinds;
        }

        /// <summary>길 위 · 옆에 띄엄띄엄 놓는 소품. 길 가운데 clearLaneHalfWidth 안에는 들어가지 않는다.</summary>
        [Serializable]
        public struct PropScatter
        {
            [Tooltip("한쪽 길에서 소품 하나당 평균 길 거리(m)")]
            [Min(1f)] public float every;
            [Tooltip("길 가운데에서 옆으로 최소 · 최대(m)")]
            public Vector2 side;
            [Tooltip("길 방향에 맞춘 뒤 흔드는 각도(±도). 180이면 아무 방향")]
            [Range(0f, 180f)] public float yawJitter;
            [Tooltip("앞뒤를 뒤집어 놓는 비율(차가 반대 차선 방향)")]
            [Range(0f, 1f)] public float flipChance;
            public Pick[] kinds;
        }

        /// <summary>정해 둔 자리의 소품(관문 검문소 · 길 끝 막기). 자리는 기준점에서 x = 오른쪽, y = 앞(m).</summary>
        [Serializable]
        public struct PlacedProp
        {
            public string id;
            public Vector2 position;
            [Tooltip("기준 방향(길 앞)에서 돌린 각도(도). 0이면 정면(-Z)이 뒤(온 쪽)를 본다")]
            public float yaw;
        }

        /// <summary>
        /// 지역 한 줄. 길 거리 startDistance부터 이 지역이고, 거기서 blendDistance 동안 앞 지역과 섞여 바뀐다(분위기 · 지형 높이 · 나무).
        /// groundLayers가 비면 기본 골짜기 지형(숲: 절벽 · 흙길 · 오염 토양, 위 "골짜기 지형 · 바닥" 값). 있으면 평평한 길가 지형 타일을 따로 만든다:
        /// 채널 순서 [길 · 길 옆 · 바깥 바탕 · 바탕에 섞는 것].
        /// </summary>
        [Serializable]
        public struct Region
        {
            public string id;
            public string displayName;
            [Tooltip("길 시작에서 이 지역이 시작하는 길 거리(m). 첫 줄은 0")]
            [Min(0f)] public float startDistance;
            [Tooltip("앞 지역에서 이 지역으로 섞여 바뀌는 길 거리(m)")]
            [Min(1f)] public float blendDistance;

            [Header("분위기 (길 거리로 섞음)")]
            [Tooltip("앰비언트(Flat) 색")]
            public Color ambientColor;
            [Tooltip("안개 색(Linear)")]
            public Color fogColor;
            [Min(0f)] public float fogStart;
            [Min(1f)] public float fogEnd;

            [Header("나무")]
            [Tooltip("숲 나무를 남기는 비율(1 = 그대로, 0 = 없음)")]
            [Range(0f, 1f)] public float treeDensity;

            [Header("길가 지형 (groundLayers가 있을 때)")]
            [Tooltip("지형 레이어 에셋 경로 4개: 길 · 길 옆 · 바깥 바탕 · 바탕에 섞는 것. 비면 기본 골짜기 지형")]
            public string[] groundLayers;
            [Tooltip("바닥 칠하기 격자(2^n). 길 옆 띠가 좁아 숲보다 촘촘하게")]
            public int alphamapResolution;
            [Tooltip("길(첫 레이어) 반폭(m)")]
            [Min(0.5f)] public float roadHalfWidth;
            [Tooltip("길 옆 띠(둘째 레이어) 폭(m)")]
            [Min(0f)] public float sideWidth;
            [Tooltip("길 위를 바탕 · 섞는 것으로 덮는 비율 최대(낡은 길, 길 텍스처 되풀이 무늬를 가림)")]
            [Range(0f, 1f)] public float roadWear;
            [Tooltip("낡은 길 얼룩 노이즈 크기")]
            [Min(0.001f)] public float wearNoiseScale;
            [Tooltip("바깥 바탕에 섞는 것(넷째 레이어) 비율 최대")]
            [Range(0f, 1f)] public float mixAmount;
            [Tooltip("섞는 얼룩 노이즈 크기")]
            [Min(0.001f)] public float mixNoiseScale;
            [Tooltip("길 옆 띠 밖 바닥 굴곡 높이(m)")]
            [Min(0f)] public float groundBumps;
            [Tooltip("다니는 띠 바깥 잔해 둔덕 높이(m) — 건물 뒤가 끝없는 평지로 보이지 않게. 0이면 없음")]
            [Min(0f)] public float outerRise;
            [Tooltip("둔덕이 시작하는 길 가운데에서의 거리 · 다 오르는 폭(m)")]
            public Vector2 outerRiseStartWidth;

            [Header("배치 (고정 시드)")]
            public int layoutSeed;
            [Tooltip("프리팹 폴더(Assets/...)")]
            public string prefabFolder;
            [Tooltip("지역 시작에서 이만큼 지나서부터 · 지역 끝 이만큼 앞까지 배치(m)")]
            public Vector2 layoutMargin;
            [Tooltip("길 가운데 이 반폭 안에는 소품을 두지 않는다(m) — 길을 막지 않게")]
            [Min(0f)] public float clearLaneHalfWidth;
            public StreetRow[] streetRows;
            public PropScatter[] props;
            [Tooltip("지역 경계 보이지 않는 벽: 길 가운데에서(m). 0이면 없음")]
            [Min(0f)] public float boundaryHalfWidth;
            [Tooltip("이 지역 앞 관문(시작 거리 바로 앞 관문) 둘레 소품. 기준은 관문 가운데 · 길 앞")]
            public PlacedProp[] gateProps;
            [Tooltip("지역 끝(마지막 지역이면 길 끝) 소품. 기준은 끝 자리 · 길 앞")]
            public PlacedProp[] endProps;

            public bool HasOwnGround => groundLayers != null && groundLayers.Length > 0;
        }

        /// <summary>길 거리에서 섞인 분위기.</summary>
        public struct Atmosphere
        {
            public Color ambientColor;
            public Color fogColor;
            public float fogStart;
            public float fogEnd;
        }


        [Header("길 (위에서 본 x, z — 시작이 남쪽, 북쪽으로 나아감). 숲 1구간 4점 + 폐허 도시 1점")]
        public Vector2[] route =
        {
            new Vector2(0f, 0f), new Vector2(0f, 120f), new Vector2(60f, 240f), new Vector2(40f, 380f),
            new Vector2(40f, 700f),
        };

        [Header("지역 (길 순서대로, 첫 줄 = 숲)")]
        public Region[] regions =
        {
            new Region
            {
                id = "Forest", displayName = "숲", startDistance = 0f, blendDistance = 1f,
                ambientColor = new Color(0.42f, 0.42f, 0.38f), fogColor = new Color(0.5f, 0.55f, 0.48f), fogStart = 70f, fogEnd = 320f,
                treeDensity = 1f,
            },
            new Region
            {
                id = "City", displayName = "폐허 도시", startDistance = 385f, blendDistance = 40f,
                ambientColor = new Color(0.44f, 0.44f, 0.45f), fogColor = new Color(0.58f, 0.56f, 0.52f), fogStart = 30f, fogEnd = 200f,
                treeDensity = 0f,
                groundLayers = new[]
                {
                    "Assets/Game/Art/City/Ground/City_Asphalt.terrainlayer", "Assets/Game/Art/City/Ground/City_Sidewalk.terrainlayer",
                    "Assets/Game/Art/City/Ground/City_RubbleConcrete.terrainlayer", "Assets/Game/Art/City/Ground/City_WeedyPavement.terrainlayer",
                },
                alphamapResolution = 2048, roadHalfWidth = 7f, sideWidth = 4f, roadWear = 0.45f, wearNoiseScale = 0.09f,
                mixAmount = 0.55f, mixNoiseScale = 0.06f, groundBumps = 0.15f, outerRise = 9f, outerRiseStartWidth = new Vector2(44f, 36f),
                layoutSeed = 11, prefabFolder = "Assets/Game/Prefabs/World/City", layoutMargin = new Vector2(30f, 12f), clearLaneHalfWidth = 2.5f,
                streetRows = new[]
                {
                    new StreetRow
                    {
                        frontOffset = 12.5f, offsetJitter = 0.8f, gap = new Vector2(1.5f, 4.5f), emptyChance = 0.12f, yawJitter = 2f,
                        kinds = new[] { new Pick { id = "City_Shop", weight = 60f }, new Pick { id = "City_Apartment", weight = 40f } },
                    },
                    new StreetRow
                    {
                        frontOffset = 28f, offsetJitter = 3f, gap = new Vector2(4f, 12f), emptyChance = 0.25f, yawJitter = 6f,
                        kinds = new[] { new Pick { id = "City_Apartment", weight = 70f }, new Pick { id = "City_Shop", weight = 30f } },
                    },
                },
                props = new[]
                {
                    // 버려진 차: 차선 위, 길 방향 ± 12°, 셋에 하나는 반대 방향
                    new PropScatter { every = 24f, side = new Vector2(4f, 5.6f), yawJitter = 12f, flipChance = 0.35f,
                        kinds = new[] { new Pick { id = "City_Car", weight = 1f } } },
                    // 길 위 장애물: 방벽 · 잔해(드문드문)
                    new PropScatter { every = 70f, side = new Vector2(4.2f, 6.2f), yawJitter = 40f, flipChance = 0.5f,
                        kinds = new[] { new Pick { id = "City_Barrier", weight = 50f }, new Pick { id = "City_Debris", weight = 50f } } },
                    // 보도 위: 바리케이드 · 잔해 · 방벽
                    new PropScatter { every = 16f, side = new Vector2(8f, 10.4f), yawJitter = 180f, flipChance = 0f,
                        kinds = new[] { new Pick { id = "City_Barricade", weight = 35f }, new Pick { id = "City_Debris", weight = 35f }, new Pick { id = "City_Barrier", weight = 30f } } },
                },
                boundaryHalfWidth = 16f,
                gateProps = new[]
                {
                    // 숲 쪽(관문 앞): 방벽 두 줄로 길을 좁힘
                    new PlacedProp { id = "City_Barrier", position = new Vector2(-4.4f, -4f), yaw = 0f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(4.4f, -4f), yaw = 0f },
                    // 도시 쪽(관문 뒤): 방벽 · 바리케이드를 엇갈려 검문소처럼, 가운데 5 m는 비움
                    new PlacedProp { id = "City_Barrier", position = new Vector2(-4.2f, 4f), yaw = 180f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(4.2f, 4f), yaw = 180f },
                    new PlacedProp { id = "City_Barricade", position = new Vector2(-3.9f, 9f), yaw = 15f },
                    new PlacedProp { id = "City_Barricade", position = new Vector2(3.9f, 9f), yaw = -15f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(-5f, 14f), yaw = 90f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(5f, 14f), yaw = -90f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(-5.4f, 17.5f), yaw = 90f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(5.4f, 17.5f), yaw = -90f },
                    new PlacedProp { id = "City_Barricade", position = new Vector2(-4.6f, 22f), yaw = 70f },
                    new PlacedProp { id = "City_Debris", position = new Vector2(7.5f, 24f), yaw = 30f },
                    new PlacedProp { id = "City_Car", position = new Vector2(-6.5f, 29f), yaw = 160f },
                },
                endProps = new[]
                {
                    // 길 끝: 방벽 · 바리케이드로 막음(뒤에 보이지 않는 벽)
                    new PlacedProp { id = "City_Barrier", position = new Vector2(-9f, 0f), yaw = 180f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(-6f, 0f), yaw = 180f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(-3f, 0f), yaw = 180f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(0f, 0f), yaw = 180f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(3f, 0f), yaw = 180f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(6f, 0f), yaw = 180f },
                    new PlacedProp { id = "City_Barrier", position = new Vector2(9f, 0f), yaw = 180f },
                    new PlacedProp { id = "City_Barricade", position = new Vector2(-4.5f, -3f), yaw = 10f },
                    new PlacedProp { id = "City_Barricade", position = new Vector2(2f, -3.2f), yaw = -8f },
                    new PlacedProp { id = "City_Debris", position = new Vector2(8f, -4f), yaw = 40f },
                },
            },
        };

        [Header("골짜기 지형")]
        [Tooltip("다닐 수 있는 골짜기 반폭(m). 길 가운데에서 이만큼까지 평지")]
        [Min(5f)] public float valleyHalfWidth = 55f;
        [Tooltip("관문 고개의 반폭(m). 관문 앞뒤로 골짜기가 이만큼 좁아진다")]
        [Min(2f)] public float passHalfWidth = 6f;
        [Tooltip("좁은 고개 길이(m)")]
        [Min(1f)] public float passLength = 30f;
        [Tooltip("골짜기 폭에서 고개 폭으로 줄어드는 거리(m)")]
        [Min(1f)] public float passBlend = 45f;
        [Tooltip("골짜기를 둘러싼 절벽 높이(m)")]
        [Min(2f)] public float cliffHeight = 28f;
        [Tooltip("절벽이 솟는 가로 거리(m). 짧을수록 가파르다(못 오르게 45° 넘게)")]
        [Min(1f)] public float cliffWidth = 10f;
        [Tooltip("흙길 반폭(m)")]
        [Min(0.5f)] public float pathHalfWidth = 4f;
        [Tooltip("골짜기 바닥 굴곡 높이(m). 길 · 입구 근처는 평평")]
        [Min(0f)] public float groundBumps = 1.5f;
        [Tooltip("굴곡 크기(작을수록 완만한 언덕)")]
        [Min(0.001f)] public float bumpScale = 0.02f;
        [Tooltip("길 둘레 바깥 여백(m)")]
        [Min(10f)] public float terrainMargin = 40f;
        [Tooltip("지형 높이 격자(2^n + 1)")]
        public int heightmapResolution = 1025;

        [Header("바닥 (지형 레이어: 숲 바닥 · 흙길 · 절벽 바위 · 오염 토양)")]
        [Tooltip("숲 바닥 텍스처 한 장이 덮는 크기(m)")]
        [Min(0.5f)] public float forestFloorTile = 5f;
        [Tooltip("흙길 텍스처 한 장이 덮는 크기(m)")]
        [Min(0.5f)] public float dirtTrailTile = 4f;
        [Tooltip("오염 토양 텍스처 한 장이 덮는 크기(m)")]
        [Min(0.5f)] public float pollutedSoilTile = 5f;
        [Tooltip("바닥 매끄러움(숲 바닥 · 바위 · 오염 토양). 높으면 하늘이 비쳐 바닥이 희게 보인다")]
        [Range(0f, 1f)] public float groundSmoothness = 0.05f;
        [Tooltip("흙길 매끄러움")]
        [Range(0f, 1f)] public float trailSmoothness = 0.1f;
        [Tooltip("던전 입구에서 이 거리 안은 오염 토양(m)")]
        [Min(0f)] public float pollutedRadius = 28f;
        [Tooltip("오염 가장자리가 숲 바닥으로 바뀌는 거리(m)")]
        [Min(0.1f)] public float pollutedFade = 8f;
        [Tooltip("오염 경계를 노이즈로 들쭉날쭉하게 하는 폭(±m)")]
        [Min(0f)] public float pollutedEdgeNoise = 6f;
        [Tooltip("오염 경계 노이즈 크기(작을수록 굵은 굴곡)")]
        [Min(0.001f)] public float pollutedNoiseScale = 0.08f;
        [Tooltip("숲 바닥에 군데군데 섞는 흙 비율 최대(0~1)")]
        [Range(0f, 1f)] public float forestDirtMix = 0.15f;
        [Tooltip("숲 바닥 흙 얼룩 노이즈 크기")]
        [Min(0.001f)] public float forestDirtNoiseScale = 0.05f;

        [Header("하늘 (분위기 색 · 안개는 regions 표)")]
        [Tooltip("하늘(Procedural 스카이박스) 밝기")]
        [Min(0f)] public float skyExposure = 1.1f;

        [Header("나무 (VARCO 모델, 종류는 treeKinds 표)")]
        [Min(0)] public int treeCount = 900;
        [Tooltip("나무 자리 고정 기준 — 숲 9점 길 · 관문 3 · 입구 3 · 무리 13곳이던 때 값(2026-11-25 숲을 1구간으로 줄일 때 고정)")]
        public TreeLayoutReference treeReference = new TreeLayoutReference
        {
            route = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 120f), new Vector2(60f, 240f), new Vector2(40f, 380f),
                new Vector2(-50f, 500f), new Vector2(-40f, 640f), new Vector2(40f, 760f), new Vector2(30f, 880f), new Vector2(0f, 980f),
            },
            passDistances = new[] { 370f, 670f, 970f },
            keepOut = new[]
            {
                new Vector2(70.14897f, 287.751129f), new Vector2(-62.88043f, 555.4732f), new Vector2(50.94242f, 830.989746f), new Vector2(0f, 6f),
                new Vector2(8f, 38f), new Vector2(-8f, 90f), new Vector2(22.36068f, 142.360672f), new Vector2(32.646595f, 198.7096f), new Vector2(67.80138f, 241.9589f),
                new Vector2(25.7512684f, 387.331635f), new Vector2(5.55127f, 440.931641f), new Vector2(-38.64873f, 476.531647f), new Vector2(-40.28087f, 523.782532f),
                new Vector2(-31.6474018f, 663.3455f), new Vector2(11.3418646f, 700.7878f), new Vector2(28.53757f, 755.4258f), new Vector2(42.67109f, 800.1965f),
            },
        };
        [Tooltip("길 가운데에서 이만큼 안에는 나무가 없다(m)")]
        [Min(0f)] public float treePathClearance = 9f;
        [Min(0.5f)] public float treeSpacing = 4f;
        [Tooltip("나무 배치 고정 시드(월드는 판마다 같다)")]
        public int treeSeed = 7;
        [Tooltip("나무 종류 표. 프리팹은 Prefabs/World/Tree_{id}.prefab(TreeSetup이 만든다). 새 나무 = 표 한 줄 + TreeSetup 모델 표 한 줄")]
        public TreeKind[] treeKinds =
        {
            new TreeKind { id = "Oak", weight = 50f, weightNearDungeon = 40f },
            new TreeKind { id = "Pine", weight = 35f, weightNearDungeon = 25f },
            new TreeKind { id = "Mutant", weight = 15f, weightNearDungeon = 35f },
        };
        [Tooltip("던전 입구에서 이 거리 안의 나무는 weightNearDungeon 비율을 쓴다(m) — 오염된 둥지 둘레에 돌연변이 나무가 많게")]
        [Min(0f)] public float treeNearDungeonRadius = 45f;
        [Tooltip("나무 크기 배율 범위(최소 · 최대)")]
        public Vector2 treeScaleRange = new Vector2(0.85f, 1.15f);


        [Header("던전 · 관문 (길 순서대로. 던전을 깨면 그 뒤 관문이 열리고, 다음 지역이 없는 마지막 던전을 깨면 클리어)")]
        public DungeonSite[] dungeons =
        {
            new DungeonSite { displayName = "숲 던전 1 — 무너진 사육장", routeDistance = 300f, sideOffset = 18f },
            // 결정 2026-11-25(사용자): "도시 던전을 깨면 다음이 열리게" — 다음 지역이 아직 없어 이 던전이 마지막(깨면 클리어). 자리는 정한 것(Claude, 확인 필요)
            new DungeonSite
            {
                displayName = "도시 던전 — 무너진 대피소", routeDistance = 630f, sideOffset = 12.5f,
                entrancePrefab = "Assets/Game/Prefabs/World/City/City_Shelter.prefab",
                doorwayCenter = new Vector3(-0.3f, 1.1f, -1.17f), doorwaySize = new Vector2(1.1f, 2.2f),
            },
        };
        [Tooltip("입구에서 길을 따라 이만큼 앞이 관문(m)")]
        [Min(5f)] public float gateAfterEntrance = 70f;
        [Tooltip("입구 앞 이 거리 안에 들어서면 던전에 들어간다(m)")]
        [Min(0.5f)] public float entranceRadius = 2.5f;
        [Tooltip("마지막 관문 너머 안내(다음 지역은 준비 중)")]
        public string nextRegionLabel = "다음 지역 — 준비 중";

        [Header("길 위 적 무리 (지역마다 관문 고개 밖 · 던전 입구 앞뒤 20 m 밖)")]
        public EncounterSite[] encounters =
        {
            // 1지역(던전 1 앞)
            new EncounterSite { routeDistance = 38f, sideOffset = 8f, count = 2 },
            new EncounterSite { routeDistance = 90f, sideOffset = -8f, count = 2 },
            new EncounterSite { routeDistance = 150f, sideOffset = 10f, count = 3 },
            new EncounterSite { routeDistance = 205f, sideOffset = -6f, count = 3 },
            new EncounterSite { routeDistance = 255f, sideOffset = 8f, count = 3 },
            // 2지역 폐허 도시(도시 던전 앞, 관문 1 검문소 · 섞는 거리 뒤 · 입구 앞 20 m 밖) — 정한 것(Claude, 확인 필요)
            new EncounterSite { routeDistance = 455f, sideOffset = -8f, count = 3 },
            new EncounterSite { routeDistance = 510f, sideOffset = 8f, count = 3 },
            new EncounterSite { routeDistance = 560f, sideOffset = -7f, count = 3 },
            new EncounterSite { routeDistance = 600f, sideOffset = 7f, count = 4 },
        };
        [Tooltip("플레이어가 이 거리 안에 오면 무리가 나타난다(m)")]
        [Min(1f)] public float encounterTriggerRange = 26f;
        [Tooltip("무리 안 적끼리 간격(m)")]
        [Min(0.5f)] public float encounterSpread = 3f;

        /// <summary>진행 길. pointCount를 주면 앞 점 그만큼만(숲 나무 자리 고르기).</summary>
        public WorldRoute ToRoute(int pointCount = -1)
        {
            int count = pointCount < 2 ? route.Length : Mathf.Min(pointCount, route.Length);
            var points = new (float x, float z)[count];
            for (int i = 0; i < count; i++) points[i] = (route[i].x, route[i].y);
            return new WorldRoute(points);
        }

        /// <summary>
        /// 길 거리에서 지역마다 섞는 무게(합 1)를 weights에 채운다. 지역 i는 startDistance부터 blendDistance 동안 0 → 1로 올라
        /// 앞 지역들을 그만큼 덮는다(부드러운 곡선). 첫 지역은 처음부터 1.
        /// </summary>
        public void RegionWeights(float distance, float[] weights)
        {
            for (int i = 0; i < regions.Length; i++)
            {
                float w = i == 0 ? 1f : Smooth01((distance - regions[i].startDistance) / Mathf.Max(1f, regions[i].blendDistance));
                for (int j = 0; j < i; j++) weights[j] *= 1f - w;
                weights[i] = w;
            }
        }

        /// <summary>길 거리에서 무게가 가장 큰 지역.</summary>
        public int RegionAt(float distance)
        {
            int found = 0;
            for (int i = 1; i < regions.Length; i++) if (distance >= regions[i].startDistance + regions[i].blendDistance * 0.5f) found = i;
            return found;
        }

        /// <summary>지역 i가 끝나는 길 거리(다음 지역 시작, 마지막이면 길 끝).</summary>
        public float RegionEnd(int region, float routeLength) => region + 1 < regions.Length ? regions[region + 1].startDistance : routeLength;

        public Atmosphere AtmosphereAt(float distance)
        {
            var weights = new float[regions.Length];
            RegionWeights(distance, weights);
            var result = new Atmosphere();
            for (int i = 0; i < regions.Length; i++)
            {
                result.ambientColor += regions[i].ambientColor * weights[i];
                result.fogColor += regions[i].fogColor * weights[i];
                result.fogStart += regions[i].fogStart * weights[i];
                result.fogEnd += regions[i].fogEnd * weights[i];
            }
            return result;
        }

        /// <summary>길 거리에서 숲 나무를 남기는 비율(지역 treeDensity를 섞은 값).</summary>
        public float TreeDensityAt(float distance)
        {
            var weights = new float[regions.Length];
            RegionWeights(distance, weights);
            float density = 0f;
            for (int i = 0; i < regions.Length; i++) density += regions[i].treeDensity * weights[i];
            return density;
        }

        /// <summary>지역 i 바로 앞 관문(시작 거리 앞에서 가장 가까운 관문). 없으면 -1.</summary>
        public int GateBefore(int region)
        {
            int found = -1;
            for (int i = 0; i < dungeons.Length; i++) if (GateDistance(i) <= regions[region].startDistance) found = i;
            return found;
        }

        /// <summary>
        /// 던전 i를 깨면(그 뒤 관문이 열려) 들어갈 수 있게 되는 지역. 관문 너머에서 새 지역이 시작하지 않으면 -1.
        /// 결정 2026-11-25(사용자): 지역 던전을 깨면 다음 지역 관문이 열린다(숲 던전 1 → 관문 1 → 폐허 도시).
        /// </summary>
        public int RegionOpenedBy(int dungeon)
        {
            for (int r = 1; r < regions.Length; r++) if (GateBefore(r) == dungeon) return r;
            return -1;
        }

        /// <summary>
        /// 마지막 던전 뒤 관문 너머에 다음 지역이 있는가. 있으면 마지막 던전을 깨도 판이 끝나지 않고 관문만 열린다.
        /// 없으면(마지막 지역의 던전) 깨면 클리어 결과 화면. 새 지역 던전 = dungeons에 한 줄이면 이 규칙이 저절로 따라간다.
        /// </summary>
        public bool LastDungeonOpensRegion => dungeons.Length > 0 && RegionOpenedBy(dungeons.Length - 1) >= 0;

        /// <summary>던전 i가 있는 지역(입구 길 거리로).</summary>
        public int RegionOfDungeon(int dungeon) => RegionAt(dungeons[dungeon].routeDistance);

        /// <summary>던전 i를 깨서 판이 끝났을 때 결과 화면에 쓸 지역 이름(그 던전이 있는 지역).</summary>
        public string ClearedRegionName(int dungeon) =>
            dungeon >= 0 && dungeon < dungeons.Length ? regions[RegionOfDungeon(dungeon)].displayName : null;

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>던전 i 뒤 관문의 길 거리.</summary>
        public float GateDistance(int dungeon) => dungeons[dungeon].routeDistance + gateAfterEntrance;

        /// <summary>길 위 무리 i 다음에 나오는 던전 순서(적 구성 단계). 마지막 던전 뒤면 마지막 던전.</summary>
        public int DungeonAhead(int encounter)
        {
            float distance = encounters[encounter].routeDistance;
            for (int i = 0; i < dungeons.Length; i++)
            {
                if (dungeons[i].routeDistance >= distance) return i;
            }
            return Mathf.Max(0, dungeons.Length - 1);
        }

        /// <summary>길 거리에서의 골짜기 반폭. 관문 둘레에서 고개 폭으로 좁아진다.</summary>
        public float ValleyHalfWidthAt(float distance)
        {
            float width = valleyHalfWidth;
            for (int i = 0; i < dungeons.Length; i++) width = Mathf.Min(width, PassWidth(distance, GateDistance(i)));
            return width;
        }

        /// <summary>정해 준 관문 길 거리들로 잰 골짜기 반폭(나무 자리 고정 기준 — <see cref="treeReference"/>).</summary>
        public float ValleyHalfWidthAt(float distance, float[] passDistances)
        {
            if (passDistances == null || passDistances.Length == 0) return ValleyHalfWidthAt(distance);
            float width = valleyHalfWidth;
            foreach (float pass in passDistances) width = Mathf.Min(width, PassWidth(distance, pass));
            return width;
        }

        private float PassWidth(float distance, float gate)
        {
            float away = Mathf.Abs(distance - gate) - passLength * 0.5f;
            float t = Mathf.Clamp01(away / passBlend);
            return Mathf.Lerp(passHalfWidth, valleyHalfWidth, t * t * (3f - 2f * t));
        }
    }
}
