using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 적이 들어오지 못하는 안전 지대(E-07 마을 등, 결정 2026-11-18 사용자). 모양은 이 물체 기준 둥근 범위(radius),
    /// 그 가운데 한쪽(+x, 로컬)을 곧은 선(cutX)으로 자를 수 있다 — 마을 울타리 모양(둥근 울타리 + 길 쪽 곧은 울타리).
    /// 플레이어는 범위 + enterMargin 안이면 보호된다: 적이 쫓지 않고 원래 자리로 돌아가며, 적 공격 · 독액 · 독 웅덩이 피해가 없고,
    /// 길 위 적 무리가 새로 나타나지 않는다(WorldRun). 적은 범위 + enemyMargin 안으로 못 들어온다:
    /// NavMesh에 구멍(자식 NavMeshModifierVolume 띠, 셋업이 만든다)이 나 있고, 그래도 들어오면 밖으로 밀려난다(Enemy).
    /// 값은 셋업이 데이터(마을은 VillageTuning)에서 채운다.
    /// </summary>
    public sealed class SafeZone : MonoBehaviour
    {
        [Tooltip("화면 안내에 보이는 이름")]
        [SerializeField] private string displayName = "";
        [Tooltip("범위 반지름(m, 이 물체 기준 수평)")]
        [Min(0f)] [SerializeField] private float radius = 10f;
        [Tooltip("로컬 +x 쪽을 곧은 선으로 자른다")]
        [SerializeField] private bool useCut;
        [Tooltip("자르는 선의 로컬 x(m). 이보다 +x 쪽은 범위 밖")]
        [SerializeField] private float cutX;
        [Tooltip("플레이어가 범위 밖으로 이만큼(m)까지도 보호된다(정문 문턱 등)")]
        [Min(0f)] [SerializeField] private float enterMargin = 1f;
        [Tooltip("적은 범위 밖으로 이만큼(m) 더 떨어진 곳까지만 온다")]
        [Min(0f)] [SerializeField] private float enemyMargin = 4f;

        private static readonly List<SafeZone> active = new List<SafeZone>();

        public string DisplayName => displayName;
        public float Radius => radius;
        public bool UseCut => useCut;
        public float CutX => cutX;
        public float EnterMargin => enterMargin;
        public float EnemyMargin => enemyMargin;

        public void Configure(string zoneName, float zoneRadius, bool cut, float zoneCutX, float playerMargin, float keepOutMargin)
        {
            displayName = zoneName;
            radius = zoneRadius;
            useCut = cut;
            cutX = zoneCutX;
            enterMargin = playerMargin;
            enemyMargin = keepOutMargin;
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        /// <summary>그 점이 범위 + margin 안(높이 뺌).</summary>
        public bool Contains(Vector3 world, float margin)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            float r = radius + margin;
            if (local.x * local.x + local.z * local.z > r * r) return false;
            return !useCut || local.x <= cutX + margin;
        }

        /// <summary>그 점에서 가장 가까운, 범위 + margin 밖의 점(이미 밖이면 그대로).</summary>
        public Vector3 ClosestOutside(Vector3 world, float margin)
        {
            if (!Contains(world, margin)) return world;
            Vector3 local = transform.InverseTransformPoint(world);
            const float Skin = 0.25f;
            Vector2 flat = new Vector2(local.x, local.z);
            Vector2 dir = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector2.right;
            Vector2 radial = dir * (radius + margin + Skin);
            Vector2 best = radial;
            if (useCut)
            {
                var across = new Vector2(cutX + margin + Skin, flat.y);
                if ((across - flat).sqrMagnitude < (radial - flat).sqrMagnitude) best = across;
            }
            return transform.TransformPoint(new Vector3(best.x, local.y, best.y));
        }

        // ---------- 모든 안전 지대 ----------

        /// <summary>플레이어가 그 점에 서 있으면 보호하는 안전 지대(없으면 null).</summary>
        public static SafeZone Protecting(Vector3 world)
        {
            foreach (SafeZone zone in active)
            {
                if (zone.Contains(world, zone.enterMargin)) return zone;
            }
            return null;
        }

        /// <summary>그 점이 어떤 안전 지대에 보호된다(플레이어 기준).</summary>
        public static bool Protects(Vector3 world) => Protecting(world) != null;

        /// <summary>적이 그 점에 있으면 안 된다면 true와 함께 가장 가까운 밖의 점.</summary>
        public static bool KeepsOut(Vector3 world, out Vector3 outside)
        {
            foreach (SafeZone zone in active)
            {
                if (!zone.Contains(world, zone.enemyMargin)) continue;
                outside = zone.ClosestOutside(world, zone.enemyMargin);
                return true;
            }
            outside = world;
            return false;
        }

        /// <summary>적이 가려는 곳이 안전 지대 안이면 밖의 가장 가까운 곳으로 바꾼다.</summary>
        public static Vector3 OutsideForEnemies(Vector3 world)
        {
            for (int i = 0; i < 3 && KeepsOut(world, out Vector3 outside); i++) world = outside;
            return world;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            DrawOutline(0f, new Color(0.3f, 1f, 0.4f));
            DrawOutline(enemyMargin, new Color(1f, 0.4f, 0.3f));
        }

        private void DrawOutline(float margin, Color color)
        {
            Gizmos.color = color;
            Gizmos.matrix = transform.localToWorldMatrix;
            float r = radius + margin;
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= 72; i++)
            {
                float a = i * Mathf.PI * 2f / 72f;
                var p = new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r);
                if (useCut) p.x = Mathf.Min(p.x, cutX + margin);
                if (i > 0) Gizmos.DrawLine(previous, p);
                previous = p;
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
