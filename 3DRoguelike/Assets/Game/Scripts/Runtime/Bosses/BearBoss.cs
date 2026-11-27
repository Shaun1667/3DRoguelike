using System.Collections;
using Game.Rules;
using Unity.Cinemachine;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 반달가슴곰 Mutant 보스(숲 던전 1 — 무너진 사육장). 땅을 울리는 무거운 근접형(<see cref="BossPatternSets.Bear"/>):
    ///  1. 앞발 휘두르기 — 달려 다가와 앞 부채꼴 예고 뒤 휘두름(격노: 두 번).
    ///  2. 일어서서 내려찍기 — 앞에 예고 원, 일어섰다가 앞발로 땅을 침: 원 안이면 피해 + 크게 밀림 · 화면 흔들림(격노: 원이 넓어짐). 점프로도 피함.
    ///  3. 땅 긁고 돌진 — 바닥에 돌진 길 예고 뒤 벽까지 곧장, 벽에 박으면 기절(멧돼지 방식) — 때릴 틈.
    ///  4. 하울링 — 곰 둘레 바닥 원 예고 뒤 포효, 원 안이면 피해 + 바깥으로 밀림(격노: 원이 넓어짐).
    /// 등장: 이름 표시(체력 막대) + 포효(피해 없음) + 화면 살짝 흔들림. 격노(체력 50% 이하): 하울링 한 번(격노 세기) 뒤 예고 · 빈틈이 짧아지고 돌진이 빨라짐.
    /// 예고는 시작할 때 방향 · 자리를 고정한다 — 옆으로 비키거나 맞는 순간 구르기 무적으로 넘긴다. 수치는 <see cref="BearBossTuning"/>.
    /// 동작은 Blender 리그(Generic) 상태에 바로 CrossFade — 상태 이름은 아래 상수, 재생 빠르기는 BossSetup이 수치 시간으로 굽고 격노 배율은 Tempo 파라미터.
    /// </summary>
    public sealed class BearBoss : Boss
    {
        public const string LocomotionState = "Locomotion";
        public const string SwipeState = "Swipe";
        public const string SlamWindupState = "SlamWindup";
        public const string SlamState = "Slam";
        public const string ChargeWindupState = "ChargeWindup";
        public const string ChargeState = "Charge";
        public const string StunState = "Stun";
        public const string RoarState = "Roar";
        /// <summary>하울링: 포효 클립을 예고 시간에 맞춰 느리게 틀어 입을 벌리는 순간이 예고 끝에 오게 한 상태.</summary>
        public const string HowlState = "Howl";
        public const string DeathState = "Death";
        public const string HitState = "Hit";
        public const string SpeedParameter = "Speed";
        public const string TempoParameter = "Tempo";
        public const int HitLayer = 1;

        private static readonly int LocomotionId = Animator.StringToHash(LocomotionState);
        private static readonly int SwipeId = Animator.StringToHash(SwipeState);
        private static readonly int SlamWindupId = Animator.StringToHash(SlamWindupState);
        private static readonly int SlamId = Animator.StringToHash(SlamState);
        private static readonly int ChargeWindupId = Animator.StringToHash(ChargeWindupState);
        private static readonly int ChargeId = Animator.StringToHash(ChargeState);
        private static readonly int StunId = Animator.StringToHash(StunState);
        private static readonly int RoarId = Animator.StringToHash(RoarState);
        private static readonly int HowlId = Animator.StringToHash(HowlState);
        private static readonly int DeathId = Animator.StringToHash(DeathState);
        private static readonly int HitId = Animator.StringToHash(HitState);
        private static readonly int SpeedId = Animator.StringToHash(SpeedParameter);
        private static readonly int TempoId = Animator.StringToHash(TempoParameter);

        /// <summary>부채꼴 예고 메시의 조각 수.</summary>
        private const int FanSegments = 24;

        [SerializeField] private BearBossTuning tuning;
        [Tooltip("돌진 길 예고(1 × 1 상자, 길이 · 폭은 실행 때 맞춤)")]
        [SerializeField] private GameObject laneWarning;
        [Tooltip("내려찍기 범위 예고(원)")]
        [SerializeField] private GameObject slamWarning;
        [Tooltip("휘두르기 범위 예고(부채꼴 — 메시는 실행 때 수치로 만든다)")]
        [SerializeField] private GameObject swipeWarning;
        [Tooltip("하울링 범위 예고(곰 둘레 원)")]
        [SerializeField] private GameObject howlWarning;
        [Tooltip("머리 뼈(카메라 · 확인용). 비면 몸")]
        [SerializeField] private Transform head;

        private Mesh fanMesh;
        private PlayerController playerController;
        private bool playerControllerLooked;

        public override BossTuning Tuning => tuning;
        public BearBossTuning BearTuning => tuning;
        public GameObject LaneWarning => laneWarning;
        public GameObject SlamWarning => slamWarning;
        public GameObject SwipeWarning => swipeWarning;
        public GameObject HowlWarning => howlWarning;
        public override Transform Head => head != null ? head : transform;
        protected override float ContactHeight => tuning.contactHeight;

        /// <summary>벽에 박아 기절한 횟수 · 포효 횟수 · 공격마다 맞힌 횟수(확인용).</summary>
        public int WallHits { get; private set; }
        public int Roars { get; private set; }
        public int SwipeHits { get; private set; }
        public int SlamHits { get; private set; }
        public int ChargeHits { get; private set; }
        public int HowlHits { get; private set; }
        /// <summary>마지막 휘두르기 · 내려찍기 · 돌진이 끝난 순간 플레이어가 예고 안에 있었는지(맞았든 무적이었든).</summary>
        public bool LastAttackCaughtPlayer { get; private set; }

        private float Tempo => IsEnraged ? tuning.enragedTempo : 1f;

        protected override void Awake()
        {
            base.Awake();
            // 예고 표시는 곰을 따라 움직이지 않게 방(부모) 밑으로
            foreach (GameObject warning in new[] { laneWarning, slamWarning, swipeWarning, howlWarning }) warning.transform.SetParent(transform.parent, true);
            fanMesh = BuildFan(tuning.swipeAngle);
            swipeWarning.GetComponent<MeshFilter>().sharedMesh = fanMesh;
            HideWarnings();
            SetFloat(TempoId, 1f);
        }

        private void OnDestroy()
        {
            if (laneWarning != null) Destroy(laneWarning);
            if (slamWarning != null) Destroy(slamWarning);
            if (swipeWarning != null) Destroy(swipeWarning);
            if (howlWarning != null) Destroy(howlWarning);
            if (fanMesh != null) Destroy(fanMesh);
        }

        public override void ResetAnimation()
        {
            SetFloat(SpeedId, 0f);
            SetFloat(TempoId, 1f);
            Play(LocomotionId, 0f);
        }

        // ---------- 대기 · 등장 · 격노 ----------

        protected override void IdleTick()
        {
            FacePlayer(tuning.turnSpeed);
            float speed = 0f;
            if (DistanceToPlayer > tuning.idleWalkDistance)
            {
                speed = tuning.walkSpeed;
                MoveForward(speed);
            }
            SetFloat(SpeedId, speed);
            Bump();
        }

        protected override IEnumerator Entrance() => tuning.roarOnEntrance ? Roar("포효") : null;

        /// <summary>격노 전환 = 하울링 한 번(예고 · 피해 있음, 격노 세기로). 패턴 순서(난수)에는 들어가지 않는다.</summary>
        protected override IEnumerator EnragePause()
        {
            SetFloat(TempoId, Tempo);
            return Howl("격노 하울링");
        }

        /// <summary>제자리에서 포효: 동작 + 포효 구간에 화면 흔들림. 이 동안은 공격하지 않는다(때릴 틈).</summary>
        private IEnumerator Roar(string label)
        {
            Step = label;
            Roars++;
            SetFloat(SpeedId, 0f);
            SetFloat(TempoId, 1f);
            Play(RoarId, 0.15f);
            bool shaken = false;
            for (float t = 0f; t < tuning.roarTime; t += Time.deltaTime)
            {
                if (!shaken && t >= tuning.roarShakeWindow.x)
                {
                    shaken = true;
                    Shake(tuning.roarShake, tuning.roarShakeWindow.y - tuning.roarShakeWindow.x, CinemachineImpulseDefinition.ImpulseShapes.Rumble);
                }
                yield return null;
            }
            Play(LocomotionId, 0.25f);
        }

        protected override IEnumerator RunPattern(BossPattern pattern)
        {
            SetFloat(TempoId, Tempo);
            switch (pattern)
            {
                case BossPattern.Swipe: return Swipe();
                case BossPattern.RearSlam: return RearSlam();
                case BossPattern.Howl: return Howl("하울링 예고");
                default: return GroundCharge();
            }
        }

        // ---------- 패턴 1: 앞발 휘두르기 ----------

        private IEnumerator Swipe()
        {
            // 달려 다가간다(예고 전이라 피해는 몸통만)
            Step = "다가감";
            Play(LocomotionId, 0.2f);
            for (float t = 0f; t < tuning.swipeApproachTime && DistanceToPlayer > tuning.swipeStartRange; t += Time.deltaTime)
            {
                FacePlayer(tuning.runTurnSpeed);
                MoveForward(tuning.runSpeed);
                SetFloat(SpeedId, tuning.runSpeed);
                Bump();
                yield return null;
            }
            SetFloat(SpeedId, 0f);

            int swings = IsEnraged ? tuning.enragedSwipes : 1;
            for (int i = 0; i < swings; i++)
            {
                // 예고: 앞 부채꼴 — 이 뒤로 방향은 바뀌지 않는다
                Step = "휘두르기 예고";
                Vector3 direction = DirectionToPlayer();
                transform.rotation = Quaternion.LookRotation(direction);
                Vector3 origin = Flat(transform.position);
                swipeWarning.transform.SetPositionAndRotation(new Vector3(origin.x, GroundY + 0.02f, origin.z), Quaternion.LookRotation(direction));
                swipeWarning.transform.localScale = new Vector3(tuning.swipeRadius, 1f, tuning.swipeRadius);
                swipeWarning.SetActive(true);
                Play(SwipeId, 0.1f);
                yield return Still(tuning.swipeWindup / Tempo, contact: false);
                swipeWarning.SetActive(false);

                Step = "휘두름";
                bool inside = false;
                if (Player != null && !Player.IsDead)
                {
                    Vector3 offset = Flat(Player.transform.position) - origin;
                    inside = BossShapes.InFan(offset.x, offset.z, direction.x, direction.z, tuning.swipeRadius, tuning.swipeAngle);
                    if (inside && Damage(tuning.swipeDamage, tuning.swipeName))
                    {
                        SwipeHits++;
                        Push(direction * tuning.swipePush);
                    }
                }
                LastAttackCaughtPlayer = inside;
                if (i < swings - 1) yield return Still(0.12f, contact: false);
            }
            Step = "빈틈";
            yield return Still(tuning.swipeRecover / Tempo, contact: true);
            Play(LocomotionId, 0.25f);
        }

        // ---------- 패턴 2: 일어서서 내려찍기 ----------

        private IEnumerator RearSlam()
        {
            Vector3 direction = DirectionToPlayer();
            transform.rotation = Quaternion.LookRotation(direction);
            Vector3 center = Flat(transform.position) + direction * tuning.slamReach;
            float radius = tuning.slamRadius * (IsEnraged ? tuning.enragedSlamRadiusMultiplier : 1f);

            // 예고: 앞에 원 — 일어서는 동안 그대로
            Step = "일어섬";
            slamWarning.transform.position = new Vector3(center.x, GroundY + 0.02f, center.z);
            slamWarning.transform.localScale = new Vector3(radius * 2f, slamWarning.transform.localScale.y, radius * 2f);
            slamWarning.SetActive(true);
            SetFloat(SpeedId, 0f);
            Play(SlamWindupId, 0.12f);
            yield return Still(tuning.slamWindup / Tempo, contact: true);

            Step = "내려찍기";
            Play(SlamId, 0.04f);
            yield return Still(tuning.slamDropTime / Tempo, contact: true);
            slamWarning.SetActive(false);

            // 원 안 + 땅에 붙어 있으면 맞고 바깥으로 크게 밀린다(점프 · 구르기 무적이면 피함)
            bool hit = false, inside = false;
            if (Player != null && !Player.IsDead)
            {
                Vector3 p = Player.transform.position;
                Vector3 away = Flat(p) - center;
                inside = away.magnitude <= radius;
                bool grounded = p.y <= GroundY + tuning.slamJumpClearance;
                if (inside && grounded && Damage(tuning.slamDamage, tuning.slamName))
                {
                    hit = true;
                    SlamHits++;
                    Push((away.sqrMagnitude > 0.01f ? away.normalized : direction) * tuning.slamPush);
                }
            }
            LastAttackCaughtPlayer = inside;
            Shake(tuning.slamShake, 0.35f, CinemachineImpulseDefinition.ImpulseShapes.Bump);
            NotifySlammed(hit);
            Step = "빈틈";
            yield return Still(tuning.slamRecover / Tempo, contact: true);
            Play(LocomotionId, 0.25f);
        }

        // ---------- 패턴 4: 하울링 ----------

        /// <summary>
        /// 제자리에서 고개를 들며(포효 동작을 느리게) 바닥에 곰 둘레 원 예고 → 입을 벌리는 순간 원 안이면 피해 + 바깥으로 밀림 → 포효가 이어지는 동안 빈틈.
        /// 원 밖으로 나가거나 맞는 순간 구르기 무적이면 안 맞는다(소리라 점프로는 못 피한다). 격노 전환(예고 이름 '격노 하울링')도 이것.
        /// </summary>
        private IEnumerator Howl(string label)
        {
            float radius = tuning.howlRadius * (IsEnraged ? tuning.enragedHowlRadiusMultiplier : 1f);
            Vector3 center = Flat(transform.position);
            Step = label;
            Roars++;
            SetFloat(SpeedId, 0f);
            howlWarning.transform.position = new Vector3(center.x, GroundY + 0.02f, center.z);
            howlWarning.transform.localScale = new Vector3(radius * 2f, howlWarning.transform.localScale.y, radius * 2f);
            howlWarning.SetActive(true);
            Play(HowlId, 0.15f);
            for (float t = 0f, windup = tuning.howlWindup / Tempo; t < windup; t += Time.deltaTime)
            {
                FacePlayer(tuning.turnSpeed);
                Bump();
                yield return null;
            }
            howlWarning.SetActive(false);

            Step = "하울링";
            bool inside = false;
            if (Player != null && !Player.IsDead)
            {
                Vector3 away = Flat(Player.transform.position) - center;
                inside = away.magnitude <= radius;
                if (inside && Damage(tuning.howlDamage, tuning.howlName))
                {
                    HowlHits++;
                    Push((away.sqrMagnitude > 0.01f ? away.normalized : transform.forward) * tuning.howlPush);
                }
            }
            LastAttackCaughtPlayer = inside;
            Shake(tuning.howlShake, (tuning.roarShakeWindow.y - tuning.roarShakeWindow.x) / Tempo, CinemachineImpulseDefinition.ImpulseShapes.Rumble);
            NotifySlammed(inside);
            Step = "빈틈";
            yield return Still(tuning.howlRecover / Tempo, contact: true);
            Play(LocomotionId, 0.25f);
        }


        // ---------- 패턴 3: 땅 긁고 돌진 ----------

        private IEnumerator GroundCharge()
        {
            Vector3 start = Flat(transform.position);
            Vector3 direction = DirectionToPlayer();
            float length = DistanceToArenaEdge(start, direction, tuning.chargeDistance);
            if (length < tuning.bodyRadius * 2f)
            {
                // 플레이어가 곰과 벽 사이에 끼어 갈 길이 없으면 방 가운데 쪽으로
                Vector3 toCenter = Flat(ArenaCenter - start);
                if (toCenter.sqrMagnitude > 0.01f)
                {
                    direction = toCenter.normalized;
                    length = DistanceToArenaEdge(start, direction, tuning.chargeDistance);
                }
            }
            bool reachesWall = length < tuning.chargeDistance - 0.01f;
            Vector3 end = start + direction * length;

            // 예고: 땅을 긁으며 돌진 길 — 이 뒤로 방향은 바뀌지 않는다
            Step = "땅 긁기";
            transform.rotation = Quaternion.LookRotation(direction);
            float laneLength = Mathf.Max(0.1f, length + tuning.bodyRadius);
            laneWarning.transform.SetPositionAndRotation(
                new Vector3(start.x + direction.x * laneLength * 0.5f, GroundY + 0.02f, start.z + direction.z * laneLength * 0.5f), Quaternion.LookRotation(direction));
            laneWarning.transform.localScale = new Vector3(tuning.chargeLaneWidth, laneWarning.transform.localScale.y, laneLength);
            laneWarning.SetActive(true);
            SetFloat(SpeedId, 0f);
            Play(ChargeWindupId, 0.12f);
            yield return Still(tuning.chargeWindup / Tempo, contact: true);
            laneWarning.SetActive(false);

            Step = "돌진";
            Play(ChargeId, 0.06f);
            float speed = tuning.chargeSpeed * Tempo;
            bool hit = false, caught = false;
            while (Flat(end - transform.position).magnitude > 0.01f)
            {
                Vector3 next = Vector3.MoveTowards(Flat(transform.position), end, speed * Time.deltaTime);
                transform.position = new Vector3(next.x, GroundY, next.z);
                if (!hit && Player != null && !Player.IsDead && Flat(Player.transform.position - transform.position).magnitude <= tuning.bodyRadius * 1.15f)
                {
                    caught = true;
                    if (Player.transform.position.y <= transform.position.y + ContactHeight && Damage(tuning.chargeDamage, tuning.chargeName))
                    {
                        hit = true;
                        ChargeHits++;
                        // 돌진 방향으로, 플레이어가 서 있는 쪽 옆으로 비껴 민다(멧돼지와 같은 방식)
                        Vector3 side = Vector3.Cross(Vector3.up, direction);
                        if (Vector3.Dot(side, Player.transform.position - transform.position) < 0f) side = -side;
                        Push((direction + side).normalized * tuning.chargePush);
                    }
                }
                yield return null;
            }
            LastAttackCaughtPlayer = caught;

            if (reachesWall)
            {
                // 벽에 박음 → 기절(맞기만 하고 공격 안 함)
                Step = "기절";
                WallHits++;
                Shake(tuning.wallShake, 0.4f, CinemachineImpulseDefinition.ImpulseShapes.Bump);
                Play(StunId, 0.1f);
                yield return Still(tuning.stunTime, contact: false);
            }
            else
            {
                Step = "빈틈";
                Play(LocomotionId, 0.2f);
                yield return Still(tuning.chargeRecover / Tempo, contact: true);
            }
            Play(LocomotionId, 0.3f);
        }

        // ---------- 맞음 · 쓰러짐 · 멈춤 ----------

        protected override void OnHurt()
        {
            Animator animator = Animator;
            if (animator == null || !animator.isActiveAndEnabled || animator.layerCount <= HitLayer) return;
            animator.Play(HitId, HitLayer, 0f);
        }

        protected override void OnStopped()
        {
            HideWarnings();
            SetFloat(SpeedId, 0f);
        }

        protected override IEnumerator FallDown()
        {
            SetFloat(SpeedId, 0f);
            SetFloat(TempoId, 1f);
            Play(DeathId, 0.15f);
            Vector3 position = transform.position;
            transform.position = new Vector3(position.x, GroundY, position.z);
            yield return new WaitForSeconds(tuning.corpseTime);
            Destroy(gameObject);
        }

        // ---------- 도움 ----------

        /// <summary>제자리에 서 있는다(돌지 않음 — 예고 방향 고정). contact면 몸통 피해.</summary>
        private IEnumerator Still(float duration, bool contact)
        {
            SetFloat(SpeedId, 0f);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (contact) Bump();
                yield return null;
            }
        }

        /// <summary>몸통 부딪치기: 맞으면 곰 바깥으로 밀어낸다(몸은 플레이어를 막지 않아 안에 갇히지 않게).</summary>
        private void Bump()
        {
            if (!Contact(tuning.contactDamage, tuning.bodyRadius, tuning.contactName)) return;
            Vector3 away = Flat(Player.transform.position - transform.position);
            Push((away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward) * tuning.contactPush);
        }


        /// <summary>앞으로 걷거나 달린다(경기장 안으로 막힘).</summary>
        private void MoveForward(float speed)
        {
            Vector3 next = ClampToArena(transform.position + Flat(transform.forward).normalized * (speed * Time.deltaTime));
            transform.position = new Vector3(next.x, GroundY, next.z);
        }

        private void Push(Vector3 velocity)
        {
            if (!playerControllerLooked)
            {
                playerControllerLooked = true;
                playerController = Player != null ? Player.GetComponent<PlayerController>() : null;
            }
            if (playerController != null) playerController.Push(velocity);
        }

        private void Play(int state, float fade)
        {
            Animator animator = Animator;
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
        }

        private void SetFloat(int id, float value)
        {
            Animator animator = Animator;
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
            animator.SetFloat(id, value);
        }

        private void HideWarnings()
        {
            if (laneWarning != null) laneWarning.SetActive(false);
            if (slamWarning != null) slamWarning.SetActive(false);
            if (swipeWarning != null) swipeWarning.SetActive(false);
            if (howlWarning != null) howlWarning.SetActive(false);
        }

        /// <summary>반지름 1 부채꼴(꼭짓점 원점, 가운데 +z, 바닥 xz면). 크기는 localScale로 맞춘다.</summary>
        private static Mesh BuildFan(float angle)
        {
            var vertices = new Vector3[FanSegments + 2];
            var triangles = new int[FanSegments * 3];
            vertices[0] = Vector3.zero;
            float half = angle * 0.5f * Mathf.Deg2Rad;
            for (int i = 0; i <= FanSegments; i++)
            {
                float a = -half + 2f * half * i / FanSegments;
                vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            }
            for (int i = 0; i < FanSegments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            var mesh = new Mesh { name = "BearSwipeFan", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
