using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 꼬리 흔들림(Verlet 체인). 꼬리 뼈 사슬을 점(뼈마다 하나 + 끝점)으로 보고, 매 프레임 애니메이션 뒤(LateUpdate)에
    /// 관성(지난 자리에서 이어 가기) · 감쇠 · 중력 · 바람 → 원래 모양으로 당기기(탄성) · 벗어남 한계(강성) · 구 충돌 · 길이 유지 순서로 풀고,
    /// 뼈를 다음 점 쪽으로 돌린다(자리는 바꾸지 않아 길이가 변하지 않는다). 계산은 1/60초 고정 걸음.
    /// 꼬리 뼈는 Humanoid 아바타 밖이라 Animator가 건드리지 않는다 — 매 프레임 기준 자세(만들 때의 로컬 회전)에서 다시 시작한다.
    /// 뼈 · 충돌 구는 VillagerSetup이 만들고, 조절 값은 VillagerTuning(종마다 tails, 없으면 tailDefault)에서 복사해 온다.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class TailChain : MonoBehaviour
    {
        [Serializable]
        public struct Settings
        {
            [Tooltip("속도 감쇠(걸음마다 속도에서 덜어 내는 비율). 높으면 빨리 멎는다")]
            [Range(0f, 1f)] public float damping;
            [Tooltip("탄성: 걸음마다 원래 모양 쪽으로 당기는 비율. 높으면 빳빳하고 빨리 돌아온다")]
            [Range(0f, 1f)] public float elasticity;
            [Tooltip("강성: 원래 모양에서 벗어날 수 있는 한계(1 = 못 벗어남, 0 = 마디 길이의 2배까지)")]
            [Range(0f, 1f)] public float stiffness;
            [Tooltip("몸이 움직인 만큼 꼬리를 같이 옮기는 비율(1 = 전부 따라가 뒤처지지 않음, 0 = 제자리에 남았다가 끌려감)")]
            [Range(0f, 1f)] public float inertia;
            [Tooltip("끝으로 갈수록 탄성 · 강성을 줄이는 정도(0 = 뿌리와 같게, 1 = 끝은 0)")]
            [Range(0f, 1f)] public float tipFalloff;
            [Tooltip("중력 가속도(m/s², 월드)")]
            public Vector3 gravity;
            [Tooltip("충돌에 쓰는 꼬리 굵기 비율(잰 굵기 × 이 값)")]
            [Range(0f, 1.5f)] public float radiusScale;
            [Tooltip("발밑 높이 아래로 내려가지 않게 한다")]
            public bool groundCollision;

            [Header("바람 · 살랑거림(제자리에 서 있을 때도 움직이게)")]
            [Tooltip("바람 가속도(m/s², 월드 방향 × 세기). 0이면 바람 없음")]
            public Vector3 wind;
            [Tooltip("바람이 들쭉날쭉한 정도(0 = 고르게, 1 = 멎었다 불었다)")]
            [Range(0f, 1f)] public float windGust;
            [Tooltip("바람이 바뀌는 빠르기(Hz)")]
            [Min(0f)] public float windFrequency;
            [Tooltip("꼬리 뿌리를 좌우로 흔드는 각도(도). 0이면 스스로 흔들지 않는다")]
            [Range(0f, 60f)] public float wagAngle;
            [Tooltip("좌우로 흔드는 빠르기(Hz)")]
            [Min(0f)] public float wagFrequency;

            public static Settings Default => new Settings
            {
                damping = 0.12f,
                elasticity = 0.05f,
                stiffness = 0.45f,
                inertia = 0.3f,
                tipFalloff = 0.5f,
                gravity = new Vector3(0f, -2f, 0f),
                radiusScale = 0.6f,
                groundCollision = true,
                wind = new Vector3(1.2f, 0f, 0.3f),
                windGust = 0.7f,
                windFrequency = 0.35f,
                wagAngle = 10f,
                wagFrequency = 0.45f,
            };
        }

        /// <summary>충돌 구. 자리 · 반지름은 bone 로컬 기준(반지름은 bone의 월드 크기를 곱해 쓴다).</summary>
        [Serializable]
        public struct Sphere
        {
            public Transform bone;
            public Vector3 center;
            public float radius;
        }

        private const float Step = 1f / 60f;
        private const int MaxSteps = 4;
        private const float TeleportDistance = 2f;

        [Tooltip("꼬리 뼈(뿌리 → 끝). 마지막은 끝점(스키닝에 안 쓰는 빈 트랜스폼)")]
        [SerializeField] private Transform[] bones = new Transform[0];
        [Tooltip("점마다 꼬리 굵기(반지름, 뼈 로컬 단위)")]
        [SerializeField] private float[] radii = new float[0];
        [Tooltip("꼬리가 뚫지 않을 구(다리)")]
        [SerializeField] private Sphere[] spheres = new Sphere[0];
        [SerializeField] private Settings settings = default;

        private Quaternion[] restRotations;
        private Vector3[] rest;     // 이번 프레임 기준 자세의 점 자리(월드)
        private Vector3[] points;   // 지금 점 자리
        private Vector3[] previous; // 한 걸음 전 점 자리
        private Vector3 lastRoot;
        private float accumulated;
        private float clock;
        private float phase;
        private bool started;

        public Settings Current { get => settings; set => settings = value; }
        public int BoneCount => bones != null ? bones.Length : 0;

        /// <summary>셋업(에디터)이 뼈 · 굵기 · 충돌 구 · 값을 넣는다.</summary>
        public void Configure(Transform[] chain, float[] chainRadii, Sphere[] colliders, Settings values)
        {
            bones = chain;
            radii = chainRadii;
            spheres = colliders;
            settings = values;
        }

        private void OnEnable()
        {
            if (bones == null || bones.Length < 2)
            {
                enabled = false;
                return;
            }
            int n = bones.Length;
            if (restRotations == null || restRotations.Length != n)
            {
                restRotations = new Quaternion[n];
                for (int i = 0; i < n; i++) restRotations[i] = bones[i].localRotation;
                rest = new Vector3[n];
                points = new Vector3[n];
                previous = new Vector3[n];
            }
            // 주민마다 흔들림이 겹치지 않게 자리로 위상을 정한다
            Vector3 p = transform.position;
            phase = Mathf.Repeat(p.x * 12.9898f + p.z * 78.233f + p.y * 37.719f, 100f);
            started = false;
            accumulated = 0f;
        }

        private void OnDisable()
        {
            if (restRotations == null) return;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null) bones[i].localRotation = restRotations[i];
            }
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            Simulate(deltaTime);
        }

        /// <summary>deltaTime만큼 흔들림을 진행하고 뼈에 반영한다. LateUpdate가 부른다(확인용으로 직접 불러도 된다).</summary>
        public void Simulate(float deltaTime)
        {
            int n = bones.Length;
            // 기준 자세: 꼬리 뼈를 만들 때 회전으로 되돌리면, 엉덩이 뼈(애니메이션)를 그대로 따라간 자리가 된다
            for (int i = 0; i < n; i++) bones[i].localRotation = restRotations[i];
            for (int i = 0; i < n; i++) rest[i] = bones[i].position;

            Vector3 move = rest[0] - lastRoot;
            lastRoot = rest[0];
            if (!started || move.sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                for (int i = 0; i < n; i++)
                {
                    points[i] = rest[i];
                    previous[i] = rest[i];
                }
                started = true;
                accumulated = 0f;
            }
            else
            {
                Vector3 carried = move * settings.inertia;
                for (int i = 1; i < n; i++)
                {
                    points[i] += carried;
                    previous[i] += carried;
                }
            }

            accumulated += deltaTime;
            int steps = Mathf.Min(MaxSteps, Mathf.FloorToInt(accumulated / Step));
            accumulated -= steps * Step;
            if (accumulated > Step) accumulated = 0f; // 프레임이 크게 밀리면 남은 시간은 버린다
            Transform anchor = bones[0].parent != null ? bones[0].parent : transform;
            float scale = bones[0].lossyScale.x;
            for (int s = 0; s < steps; s++)
            {
                clock += Step;
                StepOnce(anchor, scale);
            }
            points[0] = rest[0];

            // 뼈를 다음 점 쪽으로 돌린다(부모부터). 길이를 지켰으므로 자식 뼈 자리는 점과 같아진다
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 current = bones[i + 1].position - bones[i].position;
                Vector3 wanted = points[i + 1] - points[i];
                if (current.sqrMagnitude < 1e-12f || wanted.sqrMagnitude < 1e-12f) continue;
                bones[i].rotation = Quaternion.FromToRotation(current, wanted) * bones[i].rotation;
            }
        }

        private void StepOnce(Transform anchor, float scale)
        {
            int n = points.Length;
            Vector3 acceleration = settings.gravity + WindAt(clock);
            float wag = settings.wagAngle * Mathf.Sin((clock * settings.wagFrequency + phase) * Mathf.PI * 2f);
            // 사슬 회전: 기준 자세의 마디 방향을 지금까지 푼 부모 마디가 돈 만큼 돌린다(뿌리는 살랑거림 각도)
            Quaternion chain = Quaternion.AngleAxis(wag, anchor.up);
            float ground = transform.position.y;
            points[0] = rest[0];
            for (int i = 1; i < n; i++)
            {
                float keep = 1f - settings.tipFalloff * (i - 1) / Mathf.Max(1, n - 2);
                Vector3 p = points[i];
                Vector3 velocity = (p - previous[i]) * (1f - settings.damping);
                previous[i] = p;
                p += velocity + acceleration * (Step * Step);

                Vector3 restDelta = rest[i] - rest[i - 1];
                float length = restDelta.magnitude;
                Vector3 target = points[i - 1] + chain * restDelta;
                p = Vector3.Lerp(p, target, settings.elasticity * keep);
                float limit = length * 2f * (1f - settings.stiffness * keep);
                Vector3 away = p - target;
                if (away.sqrMagnitude > limit * limit) p = target + away.normalized * limit;

                float radius = (i < radii.Length ? radii[i] : 0f) * scale * settings.radiusScale;
                p = Collide(p, radius);
                if (settings.groundCollision && p.y < ground + radius) p.y = ground + radius;

                Vector3 direction = p - points[i - 1];
                float distance = direction.magnitude;
                p = distance > 1e-6f ? points[i - 1] + direction * (length / distance) : target;
                points[i] = p;
                chain = Quaternion.FromToRotation(chain * restDelta, p - points[i - 1]) * chain;
            }
        }

        private Vector3 WindAt(float time)
        {
            Vector3 wind = settings.wind;
            if (wind.sqrMagnitude < 1e-8f) return Vector3.zero;
            float t = time * settings.windFrequency;
            float gust = Mathf.PerlinNoise(t, phase) * 2f;           // 0~2, 평균 1
            float sway = Mathf.PerlinNoise(phase + 31.7f, t * 1.7f) * 2f - 1f;
            Vector3 side = Vector3.Cross(Vector3.up, wind);
            return wind * Mathf.Lerp(1f, gust, settings.windGust) + side * (sway * settings.windGust);
        }

        private Vector3 Collide(Vector3 point, float radius)
        {
            for (int i = 0; i < spheres.Length; i++)
            {
                Transform bone = spheres[i].bone;
                if (bone == null) continue;
                Vector3 center = bone.TransformPoint(spheres[i].center);
                float reach = spheres[i].radius * bone.lossyScale.x + radius;
                Vector3 offset = point - center;
                float sqr = offset.sqrMagnitude;
                if (sqr >= reach * reach || sqr < 1e-10f) continue;
                point = center + offset * (reach / Mathf.Sqrt(sqr));
            }
            return point;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (bones == null) return;
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.9f);
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                float radius = (i < radii.Length ? radii[i] : 0f) * bones[i].lossyScale.x * settings.radiusScale;
                Gizmos.DrawWireSphere(bones[i].position, Mathf.Max(0.005f, radius));
                if (i > 0 && bones[i - 1] != null) Gizmos.DrawLine(bones[i - 1].position, bones[i].position);
            }
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.7f);
            if (spheres == null) return;
            foreach (Sphere sphere in spheres)
            {
                if (sphere.bone == null) continue;
                Gizmos.DrawWireSphere(sphere.bone.TransformPoint(sphere.center), sphere.radius * sphere.bone.lossyScale.x);
            }
        }
#endif
    }
}
