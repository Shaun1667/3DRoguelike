using System;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 숲 월드(고정): 진행 길 · 골짜기 지형 · 나무 · 던전 입구 · 관문 · 길 위 적 무리. 값의 기준은 BALANCE.md 13번.
    /// 월드는 판마다 같고(결정 2026-11-11, 사용자) `Tools/Game/Setup World`가 이 값으로 씬을 세운다. 던전 안은 판 시드로 생성된다.
    /// 새 던전 = dungeons에 한 줄(길 순서대로), 새 적 무리 = encounters에 한 줄.
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
        }

        [Serializable]
        public struct EncounterSite
        {
            [Min(0f)] public float routeDistance;
            public float sideOffset;
            [Min(1)] public int count;
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

        [Header("길 (위에서 본 x, z — 시작이 남쪽, 북쪽으로 나아감)")]
        public Vector2[] route =
        {
            new Vector2(0f, 0f), new Vector2(0f, 120f), new Vector2(60f, 240f), new Vector2(40f, 380f),
            new Vector2(-50f, 500f), new Vector2(-40f, 640f), new Vector2(40f, 760f), new Vector2(30f, 880f),
            new Vector2(0f, 980f),
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

        [Header("분위기 (월드 씬만)")]
        [Tooltip("앰비언트(Flat) 색 — 푸른 기 없이 따뜻한 중립")]
        public Color ambientColor = new Color(0.42f, 0.42f, 0.38f);
        [Tooltip("안개 색(Linear)")]
        public Color fogColor = new Color(0.5f, 0.55f, 0.48f);
        [Min(0f)] public float fogStart = 70f;
        [Min(1f)] public float fogEnd = 320f;
        [Tooltip("하늘(Procedural 스카이박스) 밝기")]
        [Min(0f)] public float skyExposure = 1.1f;

        [Header("나무 (VARCO 모델, 종류는 treeKinds 표)")]
        [Min(0)] public int treeCount = 900;
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


        [Header("던전 · 관문 (길 순서대로, 마지막 던전을 깨면 지역 클리어)")]
        public DungeonSite[] dungeons =
        {
            new DungeonSite { displayName = "숲 던전 1 — 무너진 사육장", routeDistance = 300f, sideOffset = 18f },
            new DungeonSite { displayName = "숲 던전 2 — 버려진 관측소", routeDistance = 600f, sideOffset = -18f },
            new DungeonSite { displayName = "숲 던전 3 — 깊은 숲 둥지", routeDistance = 900f, sideOffset = 18f },
        };
        [Tooltip("입구에서 길을 따라 이만큼 앞이 관문(m)")]
        [Min(5f)] public float gateAfterEntrance = 70f;
        [Tooltip("입구 앞 이 거리 안에 들어서면 던전에 들어간다(m)")]
        [Min(0.5f)] public float entranceRadius = 2.5f;
        [Tooltip("마지막 관문 너머 안내(다음 지역은 준비 중)")]
        public string nextRegionLabel = "폐허 도시 — 준비 중";

        [Header("길 위 적 무리")]
        public EncounterSite[] encounters =
        {
            new EncounterSite { routeDistance = 110f, sideOffset = 8f, count = 2 },
            new EncounterSite { routeDistance = 210f, sideOffset = -10f, count = 3 },
            new EncounterSite { routeDistance = 420f, sideOffset = 6f, count = 2 },
            new EncounterSite { routeDistance = 510f, sideOffset = -8f, count = 3 },
            new EncounterSite { routeDistance = 700f, sideOffset = 10f, count = 3 },
            new EncounterSite { routeDistance = 800f, sideOffset = -6f, count = 4 },
        };
        [Tooltip("플레이어가 이 거리 안에 오면 무리가 나타난다(m)")]
        [Min(1f)] public float encounterTriggerRange = 22f;
        [Tooltip("무리 안 적끼리 간격(m)")]
        [Min(0.5f)] public float encounterSpread = 3f;

        public WorldRoute ToRoute()
        {
            var points = new (float x, float z)[route.Length];
            for (int i = 0; i < route.Length; i++) points[i] = (route[i].x, route[i].y);
            return new WorldRoute(points);
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
            for (int i = 0; i < dungeons.Length; i++)
            {
                float away = Mathf.Abs(distance - GateDistance(i)) - passLength * 0.5f;
                float t = Mathf.Clamp01(away / passBlend);
                width = Mathf.Min(width, Mathf.Lerp(passHalfWidth, valleyHalfWidth, t * t * (3f - 2f * t)));
            }
            return width;
        }
    }
}
