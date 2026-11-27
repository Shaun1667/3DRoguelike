using System;
using System.Collections.Generic;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 던전 안 모양 테마(바닥 · 벽 재질, 방 꾸미기 장식, 조명 · 안개). 던전마다 `WorldTuning.dungeons`의 interiorTheme으로 고른다.
    /// 비어 있으면 회색 층(숲 던전 1 = 무너진 사육장, 도시 던전 = 무너진 대피소). 새 테마 = 이 에셋 하나(`FloorThemeSetup`이 재질 · 에셋을 만든다), 새 장식 = props에 한 줄.
    /// 장식 자리는 규칙 <see cref="RoomDressingPlanner"/>가 판 시드로 정한다(BALANCE 8-1).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Floor Theme")]
    public sealed class FloorTheme : ScriptableObject
    {
        [Serializable]
        public struct Prop
        {
            public GameObject prefab;
            [Min(0f)] public float weight;
            public DressingPlace place;
            [Tooltip("크기 배율(바닥 자리는 프리팹 BoxCollider × 배율)")]
            [Min(0.05f)] public float scale;
            [Tooltip("기본 방향에 더 돌리는 각도(도). 벽 장식은 기본이 정면(-Z)이 방 안쪽, 90이면 긴 변이 벽을 따라")]
            public float yawOffset;
            [Tooltip("흔드는 각도(±도). 180이면 아무 방향")]
            [Range(0f, 180f)] public float yawJitter;
        }

        public string displayName;

        [Header("재질 (비면 회색 기본 재질)")]
        public Material floor;
        public Material startFloor;
        public Material bossFloor;
        public Material treasureFloor;
        public Material wall;
        [Tooltip("바닥 텍스처 한 장이 덮는 크기(m). 0이면 도형 UV 그대로")]
        [Min(0f)] public float floorTile = 6f;
        [Tooltip("벽 텍스처 한 장이 덮는 크기(m)")]
        [Min(0f)] public float wallTile = 4f;

        [Header("분위기")]
        public Color ambientColor = new Color(0.45f, 0.45f, 0.5f);
        public bool fog;
        public Color fogColor = Color.gray;
        [Min(0f)] public float fogStart = 20f;
        [Min(1f)] public float fogEnd = 90f;
        public Color sunColor = Color.white;
        [Min(0f)] public float sunIntensity = 1f;

        [Header("방 꾸미기")]
        public Prop[] props = Array.Empty<Prop>();
        [Tooltip("칸 하나에 벽 장식 수 최소 · 최대")]
        public Vector2Int wallPropsPerCell = new Vector2Int(3, 5);
        [Tooltip("칸 하나에 방 안 엄폐물 수 최소 · 최대(전투 · 보스 방만)")]
        public Vector2Int openPropsPerCell = new Vector2Int(0, 2);
        [Tooltip("벽 장식이 방 경계에서 들어올 수 있는 깊이(m). 적 자리 벽 여백(FloorTuning.spawnWallMargin)보다 얕게")]
        [Min(0.6f)] public float wallBandDepth = 3.2f;
        [Tooltip("문 가운데에서 비우는 거리(m)")]
        [Min(0f)] public float doorClearance = 4f;
        [Tooltip("적 자리에서 비우는 거리(m)")]
        [Min(0f)] public float spawnClearance = 2f;
        [Tooltip("방 가운데(보상물)에서 비우는 거리(m)")]
        [Min(0f)] public float centerClearance = 4f;
        [Tooltip("문 → 방 가운데 길에서 비우는 반폭(m)")]
        [Min(0f)] public float laneHalfWidth = 2.5f;
        [Tooltip("장식끼리 · 벽과 띄우는 틈(m)")]
        [Min(0f)] public float propGap = 0.4f;
        [Tooltip("방 안 엄폐물이 방 경계에서 떨어질 거리(m)")]
        [Min(0f)] public float openWallMargin = 5f;

        /// <summary>장식 종류의 바닥 자리 = 프리팹 BoxCollider(없으면 렌더러 경계) × 배율.</summary>
        public static Vector2 Footprint(Prop prop)
        {
            if (prop.prefab == null) return Vector2.zero;
            float scale = prop.scale > 0f ? prop.scale : 1f;
            var box = prop.prefab.GetComponent<BoxCollider>();
            if (box != null) return new Vector2(box.size.x, box.size.z) * scale;
            var bounds = new Bounds();
            bool any = false;
            foreach (Renderer renderer in prop.prefab.GetComponentsInChildren<Renderer>())
            {
                if (!any) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                any = true;
            }
            return any ? new Vector2(bounds.size.x, bounds.size.z) * scale : Vector2.zero;
        }

        public DressingSettings ToDressingSettings(FloorTuning tuning)
        {
            var kinds = new List<DressingKind>(props.Length);
            foreach (Prop prop in props)
            {
                Vector2 size = Footprint(prop);
                bool usable = prop.prefab != null && size.x > 0f && size.y > 0f;
                // 쓸 수 없는 줄은 무게 0(순서 = 종류 번호는 그대로)
                kinds.Add(new DressingKind(usable ? size.x : 1f, usable ? size.y : 1f, usable ? prop.weight : 0f, prop.place, prop.yawOffset, prop.yawJitter));
            }
            return new DressingSettings(tuning.cellSize, tuning.wallThickness, kinds,
                wallPropsPerCell.x, Mathf.Max(wallPropsPerCell.x, wallPropsPerCell.y), openPropsPerCell.x, Mathf.Max(openPropsPerCell.x, openPropsPerCell.y),
                wallBandDepth, doorClearance, spawnClearance, centerClearance, laneHalfWidth, propGap, openWallMargin);
        }

        /// <summary>앰비언트 · 안개 · 해를 테마 값으로.</summary>
        public void ApplyAtmosphere(Light sun)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.fog = fog;
            if (fog)
            {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd;
            }
            if (sun != null)
            {
                sun.color = sunColor;
                sun.intensity = sunIntensity;
            }
        }
    }
}
