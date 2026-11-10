using UnityEngine;
using UnityEngine.AI;

namespace Game
{
    /// <summary>
    /// 숲의 Mutant(본능형). 바닥 예고 뒤 나타나 NavMesh로 플레이어를 쫓고, 가까워지면 멈춰 예고한 뒤 앞을 할퀸다.
    /// 예고를 시작할 때 방향이 고정되므로 옆으로 피하거나 구르기 무적으로 넘길 수 있다. 할퀸 뒤에는 빈틈이 있다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Mutant : MonoBehaviour
    {
        private enum State
        {
            Spawning,
            Chasing,
            WindingUp,
            Recovering,
            Dead,
        }

        [SerializeField] private MutantTuning tuning;
        [SerializeField] private Health health;
        [SerializeField] private HitFlash flash;
        [SerializeField] private GameObject body;
        [Tooltip("나타나기 전 바닥 표시")]
        [SerializeField] private GameObject spawnWarning;
        [Tooltip("할퀴기 예고 때 맞는 범위 표시")]
        [SerializeField] private GameObject attackWarning;

        private NavMeshAgent agent;
        private Collider bodyCollider;
        private Health player;
        private State state;
        private float stateEndTime;
        private float nextRepathTime;
        private Vector3 attackForward;
        private Vector3 warningFullScale;
        private Vector3 warningFullPosition;
        private Vector3 bodyRestPosition;

        public bool IsDead => state == State.Dead;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            bodyCollider = GetComponent<Collider>();
            warningFullScale = attackWarning.transform.localScale;
            warningFullPosition = attackWarning.transform.localPosition;
            bodyRestPosition = body.transform.localPosition;
            health.Configure(tuning.maxHealth, 0f);
            flash.Configure(tuning.hitFlashTime);

            agent.speed = tuning.moveSpeed;
            agent.angularSpeed = tuning.turnSpeed;
            agent.acceleration = tuning.moveSpeed * 8f;
            agent.stoppingDistance = tuning.attackStartRange * 0.8f;
            agent.enabled = false;

            body.SetActive(false);
            bodyCollider.enabled = false;
            attackWarning.SetActive(false);
            spawnWarning.SetActive(true);
            state = State.Spawning;
            stateEndTime = Time.time + tuning.spawnTime;
        }

        private void Start()
        {
            player = GameObject.FindWithTag("Player").GetComponent<Health>();
        }

        private void OnEnable()
        {
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Died -= OnDied;
        }

        private void Update()
        {
            switch (state)
            {
                case State.Spawning:
                    if (Time.time >= stateEndTime) Appear();
                    break;
                case State.Chasing:
                    Chase();
                    break;
                case State.WindingUp:
                    if (Time.time >= stateEndTime) Strike();
                    else ShowWindup(Progress(tuning.windupTime));
                    break;
                case State.Recovering:
                    if (Time.time >= stateEndTime) ResumeChase();
                    else Lean(tuning.strikeLeanAngle * (1f - Progress(tuning.recoverTime)));
                    break;
            }
        }

        private void Appear()
        {
            spawnWarning.SetActive(false);
            body.SetActive(true);
            bodyCollider.enabled = true;
            agent.enabled = true;
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            ResumeChase();
        }

        private void Chase()
        {
            if (player.IsDead || !agent.isOnNavMesh)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }

            Vector3 offset = Flat(player.transform.position - transform.position);
            if (offset.magnitude <= tuning.attackStartRange)
            {
                BeginWindup(offset);
                return;
            }

            if (Time.time >= nextRepathTime)
            {
                agent.SetDestination(player.transform.position);
                nextRepathTime = Time.time + tuning.repathInterval;
            }
        }

        private void BeginWindup(Vector3 offset)
        {
            agent.isStopped = true;
            agent.ResetPath();
            attackForward = offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(attackForward);
            attackWarning.SetActive(true);
            state = State.WindingUp;
            stateEndTime = Time.time + tuning.windupTime;
            ShowWindup(0f);
        }

        /// <summary>예고 진행(0~1): 몸을 뒤로 젖히고, 바닥 범위가 발밑에서 앞으로 차오른다. 다 차면 할퀸다.</summary>
        private void ShowWindup(float progress)
        {
            Lean(-tuning.windupLeanAngle * progress);
            float length = Mathf.Max(0.05f, progress);
            attackWarning.transform.localScale = new Vector3(warningFullScale.x, warningFullScale.y, warningFullScale.z * length);
            attackWarning.transform.localPosition = new Vector3(warningFullPosition.x, warningFullPosition.y, warningFullPosition.z * length);
        }

        /// <summary>몸 앞뒤 기울기. 양수 = 앞으로 숙임, 음수 = 뒤로 젖힘. 발밑(루트)을 축으로 돌려 바닥에서 뜨지 않게 한다.</summary>
        private void Lean(float angle)
        {
            Quaternion tilt = Quaternion.Euler(angle, 0f, 0f);
            body.transform.localRotation = tilt;
            body.transform.localPosition = tilt * bodyRestPosition;
        }

        /// <summary>지금 상태가 얼마나 지났나(0~1).</summary>
        private float Progress(float duration)
        {
            return duration <= 0f ? 1f : Mathf.Clamp01(1f - (stateEndTime - Time.time) / duration);
        }

        private void Strike()
        {
            attackWarning.SetActive(false);
            Vector3 offset = Flat(player.transform.position - transform.position);
            bool inRange = offset.magnitude <= tuning.hitRange;
            bool inFront = offset.sqrMagnitude < 0.0001f || Vector3.Angle(attackForward, offset) <= tuning.hitAngle * 0.5f;
            if (inRange && inFront) player.TakeDamage(tuning.damage, $"{tuning.displayName}의 {tuning.attackName}");

            state = State.Recovering;
            stateEndTime = Time.time + tuning.recoverTime;
            Lean(tuning.strikeLeanAngle);
        }

        private void ResumeChase()
        {
            Lean(0f);
            state = State.Chasing;
            nextRepathTime = 0f;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        private void OnDied()
        {
            state = State.Dead;
            body.SetActive(false);
            spawnWarning.SetActive(false);
            attackWarning.SetActive(false);
            bodyCollider.enabled = false;
            agent.enabled = false;
            Destroy(gameObject, tuning.corpseTime);
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
