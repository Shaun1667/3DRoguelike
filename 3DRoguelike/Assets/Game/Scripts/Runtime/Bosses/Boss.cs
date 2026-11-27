using System;
using System.Collections;
using System.Collections.Generic;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 던전 보스(지금은 숲 던전 1의 까치). 패턴은 예전 까치 보스전(main)을 따랐다:
    /// "대기 → 패턴 하나" 되풀이, 패턴은 돌진 · 깃털 날리기 · 급강하 내려찍기, 같은 패턴은 연달아 안 나옴(<see cref="BossPatternPlanner"/>, 난수 용도 boss).
    /// 체력이 비율 이하가 되면 강화 — 대기 짧아짐 · 돌진 빨라짐 · 깃털 많아짐(두 번 연사) · 착지 빈틈 짧아짐.
    /// 모든 공격은 예고(바닥 표시 · 준비 동작)가 먼저 나오고, 예고가 나온 뒤에는 목표가 바뀌지 않는다 — 옆으로 비키거나 구르기 무적으로 넘긴다.
    /// 날아다니는 보스라 NavMesh를 쓰지 않고 방 안(<see cref="Begin"/>의 사각 경기장)에서 스스로 움직인다. 수치는 <see cref="BossTuning"/>.
    /// 모양은 <c>Body</c> 아래(모델이 없으면 임시 도형). 애니메이터가 있으면 예전 컨트롤러의 신호(ChargeReady · Charge · Dive · Land · Die)를 보낸다.
    /// </summary>
    public sealed class Boss : MonoBehaviour
    {
        private static readonly int ChargeReadyId = Animator.StringToHash("ChargeReady");
        private static readonly int ChargeId = Animator.StringToHash("Charge");
        private static readonly int DiveId = Animator.StringToHash("Dive");
        private static readonly int LandId = Animator.StringToHash("Land");
        private static readonly int DieId = Animator.StringToHash("Die");
        private static readonly int IdleStateId = Animator.StringToHash("Idle");


        [SerializeField] private BossTuning tuning;
        [SerializeField] private Health health;
        [SerializeField] private HitFlash flash;
        [Tooltip("모양(모델 자리). 준비 동작의 떨림 · 부풀기는 이것만 움직인다")]
        [SerializeField] private GameObject body;
        [Tooltip("돌진 길 예고(1 × 1 상자, 길이 · 폭은 실행 때 맞춤)")]
        [SerializeField] private GameObject laneWarning;
        [Tooltip("내려찍기 범위 예고(원)")]
        [SerializeField] private GameObject slamWarning;
        [Tooltip("몸통 애니메이터(없으면 동작 신호를 건너뜀)")]
        [SerializeField] private Animator animator;
        [Tooltip("날개 애니메이터(몸통과 같은 신호, 없으면 건너뜀)")]
        [SerializeField] private Animator wingAnimator;

        private readonly List<BossPattern> history = new List<BossPattern>();
        private readonly List<FeatherProjectile> feathers = new List<FeatherProjectile>();
        private Collider bodyCollider;
        private Health player;
        private Vector3 arenaCenter;
        private Vector2 arenaHalf;
        private BossPatternPlanner planner;
        private Coroutine fight;
        private float groundY;
        private Vector3 bodyRestPosition;
        private Vector3 bodyRestScale;
        private bool dead;

        public BossTuning Tuning => tuning;
        public Health Health => health;
        public bool IsDead => dead;
        public bool IsEnraged => planner != null && planner.Enraged;
        /// <summary>지금 하는 패턴. 대기 중이면 null.</summary>
        public BossPattern? CurrentPattern { get; private set; }
        /// <summary>지금 하는 일(디버그 · 확인용): 대기 · 돌진 예고 · 돌진 · 깃털 준비 · 깃털 · 떠오름 · 추적 · 고정 · 낙하 · 착지.</summary>
        public string Step { get; private set; } = "등장";
        /// <summary>지금까지 시작한 패턴(순서대로).</summary>
        public IReadOnlyList<BossPattern> History => history;
        public GameObject LaneWarning => laneWarning;
        public GameObject SlamWarning => slamWarning;
        public string DisplayName => tuning.displayName;
        public HitFlash Flash => flash;
        /// <summary>머리 뼈(등장 연출 카메라가 본다). 사람 뼈대가 아니면 몸.</summary>
        public Transform Head
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


        /// <summary>강화된 순간(한 번).</summary>
        public event Action Enraged;
        /// <summary>내려찍기가 땅에 닿은 순간. 값은 플레이어가 맞았는지.</summary>
        public event Action<bool> Slammed;

        private void Awake()
        {
            bodyCollider = GetComponent<Collider>();
            health.Configure(tuning.maxHealth, 0f);
            flash.Configure(tuning.hitFlashTime);
            bodyRestPosition = body.transform.localPosition;
            bodyRestScale = body.transform.localScale;
            groundY = transform.position.y;
            // 예고 표시는 보스를 따라 움직이지 않게 방(부모) 밑으로 옮긴다
            laneWarning.transform.SetParent(transform.parent, true);
            slamWarning.transform.SetParent(transform.parent, true);
            HideWarnings();
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

        private void OnDestroy()
        {
            if (laneWarning != null) Destroy(laneWarning);
            if (slamWarning != null) Destroy(slamWarning);
        }

        /// <summary>
        /// 싸움을 시작한다. arenaHalf = 보스 몸 가운데가 다닐 수 있는 사각형 반폭(방 가운데 기준, 벽 · 몸통 반지름을 뺀 값).
        /// random은 보스 패턴 난수(`RunRandom.Create(RandomPurpose.Boss, 던전)`).
        /// </summary>
public void Begin(Health target, Vector3 center, Vector2 half, IRandom random)
        {
            if (planner != null || dead) return;
            player = target;
            arenaCenter = new Vector3(center.x, transform.position.y, center.z);
            arenaHalf = new Vector2(Mathf.Max(0f, half.x), Mathf.Max(0f, half.y));
            groundY = transform.position.y;
            planner = new BossPatternPlanner(tuning.ToPatternSettings(), random);
            // 몸은 플레이어를 밀지 않는다(돌진 · 착지에 끌려가지 않게) — 몸통 피해는 거리로 본다. 근접 · 총 판정은 그대로 맞는다
            var playerBody = target != null ? target.GetComponent<CharacterController>() : null;
            if (playerBody != null && bodyCollider != null) Physics.IgnoreCollision(bodyCollider, playerBody, true);
            fight = StartCoroutine(Fight());
        }

        /// <summary>
        /// 하던 패턴을 그만두고 멈춰 선다(등장 연출 다시 보기 — 디버그). 예고 표시 · 날아가던 깃털을 거두고 바닥으로 내려온다.
        /// 패턴 순서(난수)는 그대로 이어진다. <see cref="Release"/>로 다시 움직인다.
        /// </summary>
        public void Hold()
        {
            if (dead) return;
            if (fight != null) StopCoroutine(fight);
            fight = null;
            CurrentPattern = null;
            Step = "등장";
            HideWarnings();
            foreach (FeatherProjectile feather in feathers) if (feather != null) Destroy(feather.gameObject);
            feathers.Clear();
            body.transform.localScale = bodyRestScale;
            body.transform.localPosition = bodyRestPosition;
            transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
        }

        /// <summary><see cref="Hold"/>로 멈춘 싸움을 잇는다(첫 대기부터). 아직 시작하지 않았으면 아무 일도 없다.</summary>
        public void Release()
        {
            if (dead || planner == null || fight != null) return;
            fight = StartCoroutine(Fight());
        }

        /// <summary>동작 신호를 보낸다(등장 연출이 쓴다: ChargeReady · Charge · Stomp · Land). 그 신호가 없는 애니메이터는 건너뛴다.</summary>
        public void PlaySignal(string trigger)
        {
            Signal(Animator.StringToHash(trigger));
        }

        /// <summary>걸려 있던 신호를 지우고 대기 동작으로 돌린다(등장 연출 앞뒤).</summary>
        public void ResetAnimation()
        {
            ResetAnimator(animator);
            ResetAnimator(wingAnimator);
        }

        private static void ResetAnimator(Animator target)
        {
            if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null) return;
            foreach (AnimatorControllerParameter parameter in target.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger) target.ResetTrigger(parameter.nameHash);
            }
            if (target.HasState(0, IdleStateId)) target.Play(IdleStateId, 0, 0f);
        }

        // ---------- 흐름 ----------

        private IEnumerator Fight()
        {
            Step = "등장";
            yield return Idle(tuning.fightDelay);
            while (true)
            {
                BossStep next = planner.Next();
                Step = "대기";
                yield return Idle(next.IdleSeconds);
                CurrentPattern = next.Pattern;
                history.Add(next.Pattern);
                switch (next.Pattern)
                {
                    case BossPattern.Charge: yield return Charge(); break;
                    case BossPattern.FeatherVolley: yield return FeatherVolley(); break;
                    default: yield return Slam(); break;
                }
                CurrentPattern = null;
            }
        }

        private IEnumerator Idle(float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                FacePlayer(tuning.turnSpeed);
                Contact(tuning.contactDamage, tuning.bodyRadius, tuning.contactName);
                yield return null;
            }
        }

        // ---------- 패턴 1: 돌진 ----------

        private IEnumerator Charge()
        {
            Vector3 start = Flat(transform.position);
            Vector3 direction = DirectionToPlayer();
            float length = ChargeLength(start, direction);
            if (length < tuning.bodyRadius * 2f)
            {
                // 플레이어가 보스와 벽 사이에 끼어 갈 길이 없으면 방 가운데 쪽으로
                Vector3 toCenter = Flat(arenaCenter - start);
                if (toCenter.sqrMagnitude > 0.01f)
                {
                    direction = toCenter.normalized;
                    length = ChargeLength(start, direction);
                }
            }
            Vector3 end = start + direction * length;

            // 예고: 돌진 길을 바닥에 길게 표시 — 이 뒤로 방향은 바뀌지 않는다
            Step = "돌진 예고";
            transform.rotation = Quaternion.LookRotation(direction);
            laneWarning.transform.SetPositionAndRotation(
                new Vector3((start.x + end.x) * 0.5f, groundY + 0.02f, (start.z + end.z) * 0.5f), Quaternion.LookRotation(direction));
            laneWarning.transform.localScale = new Vector3(tuning.chargeLaneWidth, laneWarning.transform.localScale.y, Mathf.Max(0.1f, length + tuning.bodyRadius));
            laneWarning.SetActive(true);
            Signal(ChargeReadyId);
            yield return Hold(tuning.chargeWindup, 0.06f);
            laneWarning.SetActive(false);

            Step = "돌진";
            Signal(ChargeId);
            float speed = tuning.chargeSpeed * (IsEnraged ? tuning.enragedChargeSpeedMultiplier : 1f);
            bool hit = false;
            while (Flat(end - transform.position).magnitude > 0.01f)
            {
                Vector3 next = Vector3.MoveTowards(Flat(transform.position), end, speed * Time.deltaTime);
                transform.position = new Vector3(next.x, groundY, next.z);
                if (!hit) hit = Contact(tuning.chargeDamage, tuning.bodyRadius * 1.1f, tuning.chargeName);
                yield return null;
            }
            Signal(LandId);
            Step = "빈틈";
            yield return Idle(tuning.chargeRecover);
        }

        private float ChargeLength(Vector3 start, Vector3 direction)
        {
            return BossShapes.DistanceToEdge(start.x - arenaCenter.x, start.z - arenaCenter.z, direction.x, direction.z,
                arenaHalf.x, arenaHalf.y, tuning.chargeDistance);
        }

        // ---------- 패턴 2: 깃털 날리기 ----------

        private IEnumerator FeatherVolley()
        {
            Step = "깃털 준비";
            for (float t = 0f; t < tuning.featherWindup; t += Time.deltaTime)
            {
                FacePlayer(tuning.turnSpeed * 2f);
                body.transform.localScale = bodyRestScale * (1f + 0.06f * Mathf.Sin(t / Mathf.Max(0.01f, tuning.featherWindup) * Mathf.PI));
                yield return null;
            }
            body.transform.localScale = bodyRestScale;

            Step = "깃털";
            int volleys = IsEnraged ? tuning.enragedVolleys : tuning.volleys;
            int count = IsEnraged ? tuning.enragedFeatherCount : tuning.featherCount;
            for (int v = 0; v < volleys; v++)
            {
                FireFan(count, v % 2 == 1);
                yield return Idle(tuning.volleyInterval);
            }
            Step = "빈틈";
            yield return Idle(tuning.featherRecover);
        }

        private void FireFan(int count, bool offsetHalf)
        {
            if (tuning.featherPrefab == null || player == null) return;
            feathers.RemoveAll(feather => feather == null); // 맞거나 수명이 다한 깃털
            Vector3 spawn = transform.position + Vector3.up * tuning.featherSpawnHeight + DirectionToPlayer() * tuning.bodyRadius;
            Vector3 aim = player.transform.position + Vector3.up * tuning.playerCenterHeight - spawn;
            if (aim.sqrMagnitude < 0.0001f) aim = transform.forward;
            aim.Normalize();
            foreach (float angle in BossShapes.FanAngles(count, tuning.featherSpread, offsetHalf))
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * aim;
                FeatherProjectile feather = Instantiate(tuning.featherPrefab, spawn, Quaternion.LookRotation(direction), transform.parent);
                feather.Launch(player, tuning);
                feathers.Add(feather);
            }
        }

        // ---------- 패턴 3: 급강하 내려찍기 ----------

        private IEnumerator Slam()
        {
            float airY = groundY + tuning.slamHeight;
            Vector3 from = transform.position;

            Step = "떠오름";
            for (float t = 0f; t < tuning.slamRiseTime; t += Time.deltaTime)
            {
                float k = t / tuning.slamRiseTime;
                transform.position = new Vector3(from.x, Mathf.Lerp(groundY, airY, k * (2f - k)), from.z);
                yield return null;
            }

            // 공중에서 플레이어 위를 따라다니고, 바닥 예고 원이 밑을 따라온다
            Step = "추적";
            slamWarning.transform.localScale = new Vector3(tuning.slamRadius * 2f, slamWarning.transform.localScale.y, tuning.slamRadius * 2f);
            MoveSlamWarning();
            slamWarning.SetActive(true);
            for (float t = 0f; t < tuning.slamTrackTime; t += Time.deltaTime)
            {
                Vector3 target = player != null ? ClampToArena(Flat(player.transform.position)) : Flat(transform.position);
                Vector3 next = Vector3.MoveTowards(Flat(transform.position), target, tuning.slamTrackSpeed * Time.deltaTime);
                transform.position = new Vector3(next.x, airY, next.z);
                MoveSlamWarning();
                yield return null;
            }

            // 낙하 지점 고정 — 예고 원이 멈춘 이 시간이 피할 시간
            Step = "고정";
            Signal(DiveId);
            yield return Hold(tuning.slamLockTime, 0.1f);

            Step = "낙하";
            Vector3 top = transform.position;
            for (float t = 0f; t < tuning.slamFallTime; t += Time.deltaTime)
            {
                float k = t / tuning.slamFallTime;
                transform.position = new Vector3(top.x, Mathf.Lerp(airY, groundY, k * k), top.z);
                yield return null;
            }
            transform.position = new Vector3(top.x, groundY, top.z);
            slamWarning.SetActive(false);

            // 원 안 + 땅에 붙어 있으면 맞는다(점프로 높이 떠 있거나 구르기 무적이면 피함)
            bool hit = false;
            if (player != null && !player.IsDead)
            {
                Vector3 p = player.transform.position;
                bool inside = Flat(p - transform.position).magnitude <= tuning.slamRadius;
                bool grounded = p.y <= groundY + tuning.slamJumpClearance;
                if (inside && grounded) hit = Damage(tuning.slamDamage, tuning.slamName);
            }
            Slammed?.Invoke(hit);
            Signal(LandId);
            Step = "착지";
            yield return Idle(IsEnraged ? tuning.enragedSlamRecover : tuning.slamRecover);
        }

        private void MoveSlamWarning()
        {
            slamWarning.transform.position = new Vector3(transform.position.x, groundY + 0.02f, transform.position.z);
        }

        // ---------- 죽음 · 피해 ----------

        private void OnDamaged(int amount)
        {
            if (dead || planner == null) return;
            if (planner.UpdateHealth(health.Current, health.Max))
            {
                Debug.Log($"[Boss] {tuning.displayName} 강화({tuning.enragedLabel}) — 체력 {health.Current}/{health.Max}");
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
            HideWarnings();
            // 날아가는 깃털도 거둔다(방을 깬 뒤 맞지 않게)
            foreach (FeatherProjectile feather in feathers) if (feather != null) Destroy(feather.gameObject);
            feathers.Clear();
            body.transform.localScale = bodyRestScale;
            body.transform.localPosition = bodyRestPosition;
            if (bodyCollider != null) bodyCollider.enabled = false;
            StartCoroutine(FallDown());
        }

        /// <summary>쓰러지기: 쓰러지는 동작이 있으면 그것, 없으면 옆으로 기운다. 공중에서 죽으면 바닥으로 내려앉는다.</summary>
        private IEnumerator FallDown()
        {
            bool hasDeath = HasParameter(animator, DieId);
            if (hasDeath) animator.SetTrigger(DieId);
            else if (animator != null) animator.speed = 0f;
            if (wingAnimator != null) wingAnimator.speed = 0f;

            Quaternion startRotation = transform.rotation;
            Quaternion endRotation = hasDeath ? startRotation : startRotation * Quaternion.Euler(0f, 0f, 80f);
            Vector3 startPosition = transform.position;
            Vector3 endPosition = new Vector3(startPosition.x, groundY, startPosition.z);
            float duration = hasDeath ? 0.5f : 1.2f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                transform.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, k), Quaternion.Slerp(startRotation, endRotation, k * k));
                yield return null;
            }
            transform.SetPositionAndRotation(endPosition, endRotation);
            Destroy(gameObject, Mathf.Max(0f, tuning.corpseTime - duration));
        }

        /// <summary>플레이어가 몸통 반지름 안(높이 뺌)이면 피해. 실제로 체력이 줄었으면 true.</summary>
        private bool Contact(int damage, float radius, string attackName)
        {
            if (player == null || player.IsDead || damage <= 0) return false;
            Vector3 p = player.transform.position;
            if (p.y > transform.position.y + tuning.slamHeight) return false;
            if (Flat(p - transform.position).magnitude > radius) return false;
            return Damage(damage, attackName);
        }

        private bool Damage(int damage, string attackName)
        {
            int before = player.Current;
            player.TakeDamage(damage, $"{tuning.displayName}의 {attackName}");
            return player.Current < before;
        }

        // ---------- 도움 ----------

        /// <summary>준비 동작: 제자리에서 몸을 떤다(난수 없이 — 연출이 결과를 바꾸지 않게).</summary>
        private IEnumerator Hold(float duration, float amount)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                body.transform.localPosition = bodyRestPosition + new Vector3(Mathf.Sin(t * 71f), 0f, Mathf.Cos(t * 53f)) * amount;
                yield return null;
            }
            body.transform.localPosition = bodyRestPosition;
        }

        private void FacePlayer(float degreesPerSecond)
        {
            Vector3 direction = DirectionToPlayer();
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * Time.deltaTime);
        }

        private Vector3 DirectionToPlayer()
        {
            if (player == null) return Flat(transform.forward).normalized;
            Vector3 offset = Flat(player.transform.position - transform.position);
            return offset.sqrMagnitude > 0.0001f ? offset.normalized : Flat(transform.forward).normalized;
        }

        private Vector3 ClampToArena(Vector3 position)
        {
            return new Vector3(
                Mathf.Clamp(position.x, arenaCenter.x - arenaHalf.x, arenaCenter.x + arenaHalf.x),
                position.y,
                Mathf.Clamp(position.z, arenaCenter.z - arenaHalf.y, arenaCenter.z + arenaHalf.y));
        }

        private void HideWarnings()
        {
            if (laneWarning != null) laneWarning.SetActive(false);
            if (slamWarning != null) slamWarning.SetActive(false);
        }

        private void Signal(int trigger)
        {
            if (HasParameter(animator, trigger)) animator.SetTrigger(trigger);
            if (HasParameter(wingAnimator, trigger)) wingAnimator.SetTrigger(trigger);
        }

        private static bool HasParameter(Animator target, int id)
        {
            if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null) return false;
            foreach (AnimatorControllerParameter parameter in target.parameters)
            {
                if (parameter.nameHash == id) return true;
            }
            return false;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
