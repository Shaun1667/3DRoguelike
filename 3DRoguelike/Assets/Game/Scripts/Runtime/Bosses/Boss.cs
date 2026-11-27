using System;
using System.Collections;
using System.Collections.Generic;
using Game.Rules;
using Unity.Cinemachine;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 던전 보스 공통(보스 종마다 이것을 이어받은 클래스 하나 — <see cref="MagpieBoss"/> 까치 · <see cref="BearBoss"/> 반달가슴곰).
    /// "등장 → 대기 → 패턴 하나" 되풀이. 패턴 순서는 <see cref="BossPatternPlanner"/>(난수 용도 boss)가 수치 에셋의 패턴 목록(<see cref="BossTuning.Patterns"/>)에서 고르고,
    /// 패턴 하나를 어떻게 하는지는 종 클래스의 <see cref="RunPattern"/>가 정한다. 체력이 비율 이하가 되면 강화(세기는 종 클래스가 <see cref="IsEnraged"/>로).
    /// 모든 공격은 예고(바닥 표시 · 준비 동작)가 먼저 나오고, 예고가 나온 뒤에는 목표가 바뀌지 않는다 — 옆으로 비키거나 구르기 무적으로 넘긴다.
    /// NavMesh를 쓰지 않고 방 안(<see cref="Begin"/>의 사각 경기장)에서 스스로 움직인다. 모양은 <c>Body</c> 아래(모델이 없으면 임시 도형).
    /// </summary>
    public abstract class Boss : MonoBehaviour
    {
        /// <summary>보스가 화면을 흔드는 채널(비트). 게임 카메라(PlayerCamera)만 듣는다 — 등장 연출 카메라(채널 1)와 따로.</summary>
        public const int ShakeChannel = 2;

        private static readonly int IdleStateId = Animator.StringToHash("Idle");

        [SerializeField] private Health health;
        [SerializeField] private HitFlash flash;
        [Tooltip("모양(모델 자리). 준비 동작의 떨림 · 부풀기는 이것만 움직인다")]
        [SerializeField] private GameObject body;
        [Tooltip("몸 애니메이터(없으면 동작을 건너뜀)")]
        [SerializeField] private Animator animator;
        [Tooltip("화면 흔들기(게임 카메라가 듣는 채널). 없으면 흔들지 않는다")]
        [SerializeField] private CinemachineImpulseSource impulse;

        private readonly List<BossPattern> history = new List<BossPattern>();
        private Collider bodyCollider;
        private BossPatternPlanner planner;
        private Coroutine fight;
        private bool enrageShown;
        private bool dead;

        /// <summary>플레이어(싸움을 시작한 뒤).</summary>
        protected Health Player { get; private set; }
        protected Vector3 ArenaCenter { get; private set; }
        protected Vector2 ArenaHalf { get; private set; }
        protected float GroundY { get; set; }
        protected Vector3 BodyRestPosition { get; private set; }
        protected Vector3 BodyRestScale { get; private set; }
        protected GameObject Body => body;
        protected Animator Animator => animator;

        /// <summary>이 보스의 수치(종 클래스가 자기 수치 에셋을 돌려준다).</summary>
        public abstract BossTuning Tuning { get; }
        public Health Health => health;
        public bool IsDead => dead;
        public bool IsEnraged => planner != null && planner.Enraged;
        /// <summary>지금 하는 패턴. 대기 중이면 null.</summary>
        public BossPattern? CurrentPattern { get; private set; }
        /// <summary>지금 하는 일(디버그 · 확인용): 등장 · 대기 · 패턴의 단계 이름 · 빈틈 · 쓰러짐.</summary>
        public string Step { get; protected set; } = "등장";
        /// <summary>지금까지 시작한 패턴(순서대로).</summary>
        public IReadOnlyList<BossPattern> History => history;
        public string DisplayName => Tuning.displayName;
        public HitFlash Flash => flash;
        /// <summary>머리 뼈(등장 연출 카메라가 본다). 못 찾으면 몸.</summary>
        public virtual Transform Head
        {
            get
            {
                Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                return head != null ? head : transform;
            }
        }
        /// <summary>싸움을 시작했는지(<see cref="Begin"/>). 등장 연출 동안은 아직 false.</summary>
        public bool HasBegun => planner != null;
        /// <summary>패턴이 돌고 있는지. 등장 연출 동안 · 멈춰 둔 동안(<see cref="Hold"/>)은 false.</summary>
        public bool IsFighting => fight != null;
        /// <summary>화면을 흔든 횟수(확인용).</summary>
        public int ShakeCount { get; private set; }


        /// <summary>강화된 순간(한 번).</summary>
        public event Action Enraged;
        /// <summary>내려찍기가 땅에 닿은 순간. 값은 플레이어가 맞았는지.</summary>
        public event Action<bool> Slammed;

        protected virtual void Awake()
        {
            bodyCollider = GetComponent<Collider>();
            health.Configure(Tuning.maxHealth, 0f);
            flash.Configure(Tuning.hitFlashTime);
            BodyRestPosition = body.transform.localPosition;
            BodyRestScale = body.transform.localScale;
            GroundY = transform.position.y;
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        /// <summary>
        /// 싸움을 시작한다. half = 보스 몸 가운데가 다닐 수 있는 사각형 반폭(방 가운데 기준, 벽 · 몸통 반지름을 뺀 값).
        /// random은 보스 패턴 난수(`RunRandom.Create(RandomPurpose.Boss, 던전)`).
        /// </summary>
        public void Begin(Health target, Vector3 center, Vector2 half, IRandom random)
        {
            if (planner != null || dead) return;
            Player = target;
            ArenaCenter = new Vector3(center.x, transform.position.y, center.z);
            ArenaHalf = new Vector2(Mathf.Max(0f, half.x), Mathf.Max(0f, half.y));
            GroundY = transform.position.y;
            planner = new BossPatternPlanner(Tuning.ToPatternSettings(), Tuning.Patterns, random);
            // 몸은 플레이어를 밀지 않는다(돌진 · 착지에 끌려가지 않게) — 몸통 피해는 거리로 본다. 근접 · 총 판정은 그대로 맞는다
            var playerBody = target != null ? target.GetComponent<CharacterController>() : null;
            if (playerBody != null && bodyCollider != null) Physics.IgnoreCollision(bodyCollider, playerBody, true);
            fight = StartCoroutine(Fight());
        }

        /// <summary>
        /// 하던 패턴을 그만두고 멈춰 선다(등장 다시 보기 — 디버그). 예고 표시 · 투사체를 거두고 바닥으로 내려온다.
        /// 패턴 순서(난수)는 그대로 이어진다. <see cref="Release"/>로 다시 움직인다(등장 동작부터).
        /// </summary>
        public void Hold()
        {
            if (dead) return;
            if (fight != null) StopCoroutine(fight);
            fight = null;
            CurrentPattern = null;
            Step = "등장";
            OnStopped();
            body.transform.localScale = BodyRestScale;
            body.transform.localPosition = BodyRestPosition;
            transform.position = new Vector3(transform.position.x, GroundY, transform.position.z);
        }

        /// <summary><see cref="Hold"/>로 멈춘 싸움을 잇는다(등장 동작 · 첫 대기부터). 아직 시작하지 않았으면 아무 일도 없다.</summary>
        public void Release()
        {
            if (dead || planner == null || fight != null) return;
            fight = StartCoroutine(Fight());
        }

        /// <summary>동작 신호(트리거)를 보낸다(등장 연출이 쓴다). 그 신호가 없는 애니메이터는 건너뛴다.</summary>
        public void PlaySignal(string trigger)
        {
            Signal(Animator.StringToHash(trigger));
        }

        /// <summary>걸려 있던 신호를 지우고 대기 동작으로 돌린다(등장 연출 앞뒤).</summary>
        public virtual void ResetAnimation()
        {
            ResetAnimator(animator);
        }

        protected static void ResetAnimator(Animator target)
        {
            if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null) return;
            foreach (AnimatorControllerParameter parameter in target.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger) target.ResetTrigger(parameter.nameHash);
            }
            if (target.HasState(0, IdleStateId)) target.Play(IdleStateId, 0, 0f);
        }

        // ---------- 종마다 ----------

        /// <summary>패턴 하나(<see cref="BossTuning.Patterns"/> 중 하나). 끝날 때 보스는 땅 위에 서 있어야 한다.</summary>
        protected abstract IEnumerator RunPattern(BossPattern pattern);

        /// <summary>싸움을 시작할 때의 짧은 등장 동작(예: 곰의 포효). 없으면 null.</summary>
        protected virtual IEnumerator Entrance() => null;

        /// <summary>강화된 뒤 처음 대기 전에 한 번(예: 곰의 포효). 없으면 null.</summary>
        protected virtual IEnumerator EnragePause() => null;

        /// <summary>대기 한 프레임: 기본은 플레이어 쪽으로 돌고 몸통 피해.</summary>
        protected virtual void IdleTick()
        {
            FacePlayer(Tuning.turnSpeed);
            Contact(Tuning.contactDamage, Tuning.bodyRadius, Tuning.contactName);
        }

        /// <summary>패턴을 멈춘 때(<see cref="Hold"/> · 죽음): 예고 표시 · 투사체를 거둔다.</summary>
        protected abstract void OnStopped();

        /// <summary>맞았을 때(살아 있을 때만).</summary>
        protected virtual void OnHurt() { }

        /// <summary>쓰러지기 동작. 끝나면 몸을 치운다(<see cref="BossTuning.corpseTime"/>).</summary>
        protected abstract IEnumerator FallDown();

        /// <summary>몸통 피해를 주는 높이(플레이어 발이 보스 발밑보다 이만큼 넘게 위면 안 맞음).</summary>
        protected abstract float ContactHeight { get; }

        // ---------- 흐름 ----------

        private IEnumerator Fight()
        {
            Step = "등장";
            IEnumerator entrance = Entrance();
            if (entrance != null) yield return entrance;
            yield return Idle(Tuning.fightDelay);
            while (true)
            {
                if (IsEnraged && !enrageShown)
                {
                    enrageShown = true;
                    IEnumerator pause = EnragePause();
                    if (pause != null) yield return pause;
                }
                BossStep next = planner.Next();
                Step = "대기";
                yield return Idle(next.IdleSeconds);
                CurrentPattern = next.Pattern;
                history.Add(next.Pattern);
                yield return RunPattern(next.Pattern);
                CurrentPattern = null;
            }
        }

        protected IEnumerator Idle(float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                IdleTick();
                yield return null;
            }
        }

        // ---------- 죽음 · 피해 ----------

        private void OnDamaged(int amount)
        {
            if (dead) return;
            OnHurt();
            if (planner == null) return;
            if (planner.UpdateHealth(health.Current, health.Max))
            {
                Debug.Log($"[Boss] {Tuning.displayName} 강화({Tuning.enragedLabel}) — 체력 {health.Current}/{health.Max}");
                Enraged?.Invoke();
            }
        }

        private void OnDied()
        {
            dead = true;
            if (fight != null) StopCoroutine(fight);
            fight = null;
            CurrentPattern = null;
            Step = "쓰러짐";
            OnStopped();
            body.transform.localScale = BodyRestScale;
            body.transform.localPosition = BodyRestPosition;
            if (bodyCollider != null) bodyCollider.enabled = false;
            StartCoroutine(FallDown());
        }

        /// <summary>내려찍기가 땅에 닿았다고 알린다.</summary>
        protected void NotifySlammed(bool hit) => Slammed?.Invoke(hit);

        /// <summary>플레이어가 몸통 반지름 안(높이 뺀)이면 피해. 실제로 체력이 줄었으면 true.</summary>
        protected bool Contact(int damage, float radius, string attackName)
        {
            if (Player == null || Player.IsDead || damage <= 0) return false;
            Vector3 p = Player.transform.position;
            if (p.y > transform.position.y + ContactHeight) return false;
            if (Flat(p - transform.position).magnitude > radius) return false;
            return Damage(damage, attackName);
        }

        /// <summary>플레이어에게 피해(사망 원인 '{이름}의 {공격}'). 구르기 무적 · 맞은 뒤 무적이면 false.</summary>
        protected bool Damage(int damage, string attackName)
        {
            int before = Player.Current;
            Player.TakeDamage(damage, $"{Tuning.displayName}의 {attackName}");
            return Player.Current < before;
        }

        /// <summary>화면을 흔든다(게임 카메라의 듣는 채널 — 등장 연출 카메라와 따로).</summary>
        protected void Shake(float force, float duration, CinemachineImpulseDefinition.ImpulseShapes shape)
        {
            if (impulse == null || force <= 0f) return;
            impulse.ImpulseDefinition.ImpulseShape = shape;
            impulse.ImpulseDefinition.ImpulseDuration = Mathf.Max(0.05f, duration);
            impulse.GenerateImpulseWithForce(force);
            ShakeCount++;
        }

        // ---------- 도움 ----------

        /// <summary>준비 동작: 제자리에서 몸을 떤다(난수 없이 — 연출이 결과를 바꾸지 않게).</summary>
        protected IEnumerator Shiver(float duration, float amount)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                body.transform.localPosition = BodyRestPosition + new Vector3(Mathf.Sin(t * 71f), 0f, Mathf.Cos(t * 53f)) * amount;
                yield return null;
            }
            body.transform.localPosition = BodyRestPosition;
        }

        protected void FacePlayer(float degreesPerSecond)
        {
            Vector3 direction = DirectionToPlayer();
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * Time.deltaTime);
        }

        protected Vector3 DirectionToPlayer()
        {
            if (Player == null) return Flat(transform.forward).normalized;
            Vector3 offset = Flat(Player.transform.position - transform.position);
            return offset.sqrMagnitude > 0.0001f ? offset.normalized : Flat(transform.forward).normalized;
        }

        protected float DistanceToPlayer => Player != null ? Flat(Player.transform.position - transform.position).magnitude : float.PositiveInfinity;

        protected Vector3 ClampToArena(Vector3 position)
        {
            return new Vector3(
                Mathf.Clamp(position.x, ArenaCenter.x - ArenaHalf.x, ArenaCenter.x + ArenaHalf.x),
                position.y,
                Mathf.Clamp(position.z, ArenaCenter.z - ArenaHalf.y, ArenaCenter.z + ArenaHalf.y));
        }

        /// <summary>방 안의 점에서 그 방향으로 벽(경기장 끝)까지 거리, 최대 maxDistance.</summary>
        protected float DistanceToArenaEdge(Vector3 start, Vector3 direction, float maxDistance)
        {
            return BossShapes.DistanceToEdge(start.x - ArenaCenter.x, start.z - ArenaCenter.z, direction.x, direction.z,
                ArenaHalf.x, ArenaHalf.y, maxDistance);
        }

        protected virtual void Signal(int trigger)
        {
            if (HasParameter(animator, trigger)) animator.SetTrigger(trigger);
        }

        protected static bool HasParameter(Animator target, int id)
        {
            if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null) return false;
            foreach (AnimatorControllerParameter parameter in target.parameters)
            {
                if (parameter.nameHash == id) return true;
            }
            return false;
        }

        protected static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
