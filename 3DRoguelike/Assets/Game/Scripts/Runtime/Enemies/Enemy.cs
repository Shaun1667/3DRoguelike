using System.Collections.Generic;
using Game.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Game
{
    /// <summary>
    /// 숲 적의 공통 부분: 체력 · 맞은 표시 · 나타나기 예고 · NavMesh 쫓기 · 예고/빈틈 시간 · 플레이어 맞히기 · 죽음 · 살아 있는 적 목록.
    /// 종마다 행동은 이것을 이어받은 클래스 한 곳(<see cref="Badger"/> · <see cref="Boar"/> · <see cref="Hare"/> · <see cref="Toad"/>),
    /// 수치는 종마다 <see cref="EnemyTuning"/> 에셋 하나(Mutant · E 개체 값 같이). 모습(Mutant / E 개체)은 프리팹이 정한다.
    /// 모양은 <c>Body</c> 아래 — 모델로 바꿀 때 그 아래만 갈아 끼운다. 피벗은 발밑.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class Enemy : MonoBehaviour
    {
        [Tooltip("Mutant(네 발, 본능) 또는 E 개체(두 발, 체력 ×1.8 + 공격 하나 더)")]
        [SerializeField] private EnemyGrade grade;
        [SerializeField] private Health health;
        [SerializeField] private HitFlash flash;
        [Tooltip("모양(모델 자리). 기울기 · 뛰기는 이것만 움직인다")]
        [SerializeField] private GameObject body;
        [Tooltip("나타나기 전 바닥 표시")]
        [SerializeField] private GameObject spawnWarning;
        [Tooltip("리깅 모델(E 개체 · 네 발 Mutant)의 Animator(상태 이름은 EnemyAnim). 없으면(정적 모델 · 임시 도형) 몸 기울이기 연출만 쓴다")]
        [SerializeField] private Animator animator;

        private static readonly List<Enemy> alive = new List<Enemy>();

        private Collider bodyCollider;
        private Vector3 bodyRestPosition;
        private PlayerController playerController;
        private bool appeared;
        private bool dead;
        private float appearTime;
        private float nextRepathTime;
        private int attacksStarted;
        private Vector3 homePosition;

        /// <summary>살아 있는 적 전부(나타나는 중 포함, 종 무관). 디버그 창의 원펀맨 · 모두 처치가 쓴다.</summary>
        public static IReadOnlyList<Enemy> Alive => alive;

        public bool IsDead => dead;
        public Health Health => health;
        public EnemyGrade Grade => grade;
        public bool IsEvolved => grade == EnemyGrade.Evolved;
        public abstract EnemyTuning Tuning { get; }
        public string DisplayName => Tuning.NameFor(grade);

        protected NavMeshAgent Agent { get; private set; }
        /// <summary>플레이어 체력. 없으면 null(쫓지 않음).</summary>
        protected Health Player { get; private set; }
        /// <summary>상태가 끝나는 시각. <see cref="StartTimer"/>로 맞춘다.</summary>
        protected float StateEnd { get; private set; }

        /// <summary>쫓아갈 때 멈추는 거리(m).</summary>
        protected virtual float StoppingDistance => 0.2f;

        /// <summary>나타난 순간(몸 · 충돌 · NavMesh가 켜진 뒤). 처음 상태를 정한다.</summary>
        protected abstract void OnAppear();

        /// <summary>나타난 뒤 매 프레임.</summary>
        protected abstract void Tick();

        /// <summary>종마다 가진 예고 표시를 모두 끈다(나타날 때 · 죽을 때).</summary>
        protected abstract void HideWarnings();

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            bodyCollider = GetComponent<Collider>();
            bodyRestPosition = body.transform.localPosition;
            EnemyTuning tuning = Tuning;
            health.Configure(tuning.HealthFor(grade), 0f);
            flash.Configure(tuning.hitFlashTime);

            Agent.speed = tuning.moveSpeed;
            Agent.angularSpeed = tuning.turnSpeed;
            Agent.acceleration = tuning.moveSpeed * 8f;
            Agent.stoppingDistance = StoppingDistance;
            Agent.enabled = false;

            body.SetActive(false);
            bodyCollider.enabled = false;
            HideWarnings();
            spawnWarning.SetActive(true);
            appearTime = Time.time + tuning.spawnTime;
        }

        protected virtual void Start()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;
            Player = player.GetComponent<Health>();
            playerController = player.GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            health.Died += OnDied;
            health.Damaged += OnDamaged;
            if (!dead) alive.Add(this);
        }

        private void OnDisable()
        {
            health.Died -= OnDied;
            health.Damaged -= OnDamaged;
            alive.Remove(this);
        }

        private void Update()
        {
            if (dead) return;
            if (!appeared)
            {
                if (Time.time >= appearTime) Appear();
                return;
            }
            Tick();
            KeepOutOfSafeZones();
            if (HasAnimator) animator.SetFloat(EnemyAnim.Speed, Agent.enabled ? Agent.velocity.magnitude : 0f, 0.1f, Time.deltaTime);
        }

        private void Appear()
        {
            appeared = true;
            spawnWarning.SetActive(false);
            body.SetActive(true);
            bodyCollider.enabled = true;
            Agent.enabled = true;
            if (!Agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
            }
            homePosition = SafeZone.OutsideForEnemies(transform.position);
            OnAppear();
        }

        /// <summary>안전 지대(마을 등) 안에 들어와 있으면 밖으로 민다. 보통은 NavMesh 구멍이 먼저 막는다.</summary>
        private void KeepOutOfSafeZones()
        {
            if (dead || !Agent.enabled || !Agent.isOnNavMesh) return;
            if (SafeZone.KeepsOut(transform.position, out Vector3 outside)) Agent.Move(Flat(outside - transform.position));
        }

private void OnDied()
        {
            dead = true;
            alive.Remove(this);
            spawnWarning.SetActive(false);
            HideWarnings();
            bodyCollider.enabled = false;
            Agent.enabled = false;
            if (appeared && HasAnimator)
            {
                // 동작이 있는 모델(E 개체 · Mutant): 판정 · 목록 · 보상은 지금 바로 끝나고, 몸만 남아 죽는 동작을 보여 준 뒤 사라진다
                Pose(0f);
                animator.SetLayerWeight(EnemyAnim.HitLayer, 0f);
                animator.CrossFadeInFixedTime(EnemyAnim.Death, 0.1f, 0, 0f);
                Destroy(gameObject, Tuning.evolvedCorpseTime);
                return;
            }
            body.SetActive(false);
            Destroy(gameObject, Tuning.corpseTime);
        }

        private void OnDamaged(int amount)
        {
            if (dead || !HasAnimator || !animator.isActiveAndEnabled) return;
            animator.Play(EnemyAnim.Hit, EnemyAnim.HitLayer, 0f); // 더하기 레이어 — 하던 동작(예고 · 공격)은 끊기지 않는다
        }

        // ---------- 종 클래스가 쓰는 도움 ----------

        /// <summary>쫓거나 공격할 수 있다(플레이어가 살아 있고 안전 지대 밖, 적은 NavMesh 위).</summary>
        protected bool CanAct => Player != null && !Player.IsDead && !PlayerIsSafe && Agent.isOnNavMesh;

        /// <summary>플레이어가 안전 지대(마을 등) 안에 있다 — 쫓지도 공격하지도 않는다.</summary>
        protected bool PlayerIsSafe => Player != null && SafeZone.Protects(Player.transform.position);

        /// <summary>
        /// 쫓을 수 없을 때(<see cref="CanAct"/>가 false). 플레이어가 안전 지대 안이면 나타났던 자리로 돌아가 서 있고,
        /// 아니면(죽음 · 없음) 그 자리에 멈춘다. 플레이어가 나오면 종 클래스가 다시 쫓는다.
        /// </summary>
        protected void WaitForPlayer()
        {
            if (!PlayerIsSafe || !Agent.isOnNavMesh)
            {
                StopMoving();
                return;
            }
            Pose(0f);
            if (Flat(homePosition - transform.position).magnitude <= Agent.stoppingDistance + Agent.radius) StopMoving();
            else MoveTo(homePosition);
        }

        /// <summary>플레이어까지(높이 뺌).</summary>
        protected Vector3 ToPlayer => Flat(Player.transform.position - transform.position);

        /// <summary>NavMesh로 플레이어 쪽으로 간다(길은 간격마다 다시 찾는다).</summary>
        protected void ChasePlayer() => MoveTo(Player.transform.position);

        protected void MoveTo(Vector3 destination)
        {
            if (!Agent.isOnNavMesh) return;
            Agent.isStopped = false;
            if (Time.time < nextRepathTime) return;
            Agent.SetDestination(SafeZone.OutsideForEnemies(destination));
            nextRepathTime = Time.time + Tuning.repathInterval;
        }

        /// <summary>멈춘다(공격 · 예고 · 빈틈).</summary>
        protected void StopMoving()
        {
            if (!Agent.isOnNavMesh) return;
            Agent.isStopped = true;
            Agent.ResetPath();
            nextRepathTime = 0f;
        }

        protected void Face(Vector3 direction)
        {
            direction = Flat(direction);
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// NavMesh 위에서 곧장 움직인다(돌진 · 도약 · 내딛기). 벽 · 나무 등 NavMesh 끝에 막혀 가려던 방향으로 절반도 못 갔으면 false.
        /// 막힘은 실제로 간 거리로 본다 — NavMesh.Raycast는 NavMesh 조각 경계에서 막힌 것으로 잘못 나오는 일이 있었다(2026-11-16).
        /// </summary>
        protected bool MoveStraight(Vector3 delta)
        {
            delta = Flat(delta);
            float length = delta.magnitude;
            if (length < 0.001f || !Agent.isOnNavMesh) return true;
            Vector3 from = transform.position;
            Agent.Move(delta);
            float progress = Vector3.Dot(Flat(transform.position - from), delta / length);
            return progress >= length * 0.5f;
        }

        /// <summary>NavMesh 위로 플레이어까지 막힌 데가 없다(돌진 · 독액을 쏠 때).</summary>
        protected bool ClearLineToPlayer()
        {
            return Agent.isOnNavMesh && !NavMesh.Raycast(transform.position, Player.transform.position, out _, NavMesh.AllAreas);
        }

        /// <summary>몸 자세. 앞뒤 기울기(양수 = 앞으로 숙임)는 발밑을 축으로, lift는 위로 뜨는 높이(m).</summary>
protected void Pose(float leanAngle, float lift = 0f)
        {
            if (HasAnimator)
            {
                // 동작이 있는 모델: 젖히기 · 숙이기 · 웅크림은 동작이 맡고, 위로 뜨는 높이만 코드가 준다(수치 에셋의 각도는 그대로 둔다)
                leanAngle = 0f;
                lift = Mathf.Max(0f, lift);
            }
            Quaternion tilt = Quaternion.Euler(leanAngle, 0f, 0f);
            body.transform.localRotation = tilt;
            body.transform.localPosition = tilt * bodyRestPosition + Vector3.up * lift;
        }

        /// <summary>동작이 있는 모델인가(E 개체 · 네 발 Mutant 리깅 모델). 아니면 동작 호출은 모두 아무것도 하지 않는다.</summary>
        protected bool HasAnimator => animator != null;

        /// <summary>동작 상태로 넘어간다(<see cref="EnemyAnim"/>의 해시). 상태 길이는 Setup이 수치 에셋 시간에 맞춰 두었다.</summary>
        protected void PlayAnimation(int state, float fade = 0.08f)
        {
            if (!HasAnimator || !animator.isActiveAndEnabled) return;
            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
        }

        /// <summary>대기/이동 동작으로 돌아간다(이미 그 상태거나 그리로 넘어가는 중이면 그대로).</summary>
        protected void PlayLocomotion()
        {
            if (!HasAnimator || !animator.isActiveAndEnabled) return;
            bool changing = animator.IsInTransition(0);
            int target = changing ? animator.GetNextAnimatorStateInfo(0).shortNameHash : animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (target == EnemyAnim.Locomotion) return;
            animator.CrossFadeInFixedTime(EnemyAnim.Locomotion, 0.2f, 0);
        }

        protected void StartTimer(float duration) => StateEnd = Time.time + duration;
        protected bool TimerDone => Time.time >= StateEnd;

        /// <summary>지금 상태가 얼마나 지났나(0~1).</summary>
        protected float TimerProgress(float duration)
        {
            return duration <= 0f ? 1f : Mathf.Clamp01(1f - (StateEnd - Time.time) / duration);
        }

        /// <summary>공격을 시작할 때 부른다. E 개체면 <see cref="EnemyTuning.extraAttackEvery"/>번마다 true(추가 공격 차례).</summary>
        protected bool NextAttackIsExtra()
        {
            attacksStarted++;
            return IsEvolved && attacksStarted % Mathf.Max(1, Tuning.extraAttackEvery) == 0;
        }

        /// <summary>플레이어에게 피해. 사망 원인은 '{이름}의 {공격}'. 실제로 체력이 줄었으면 true(구르기 무적 · 맞은 뒤 무적이면 false).</summary>
        protected bool HitPlayer(int damage, string attackName)
        {
            if (Player == null || Player.IsDead || PlayerIsSafe) return false;
            int before = Player.Current;
            Player.TakeDamage(damage, $"{DisplayName}의 {attackName}");
            return Player.Current < before;
        }

        /// <summary>플레이어를 민다(멧돼지 돌진 등). 줄어드는 빠르기는 PlayerTuning.</summary>
        protected void PushPlayer(Vector3 velocity)
        {
            if (playerController != null) playerController.Push(velocity);
        }

        /// <summary>플레이어가 그 점에서 반지름 안(높이 뺌)에 있다.</summary>
        protected bool PlayerWithin(Vector3 center, float radius)
        {
            return Player != null && Flat(Player.transform.position - center).sqrMagnitude <= radius * radius;
        }

        protected static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
