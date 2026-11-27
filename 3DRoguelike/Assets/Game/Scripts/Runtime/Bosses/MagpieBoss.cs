using System.Collections;
using System.Collections.Generic;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 까치 보스(도시 던전 — 무너진 대피소). 패턴은 예전 까치 보스전(main)을 따랐다: 돌진 · 깃털 날리기 · 급강하 내려찍기(<see cref="BossPatternSets.Magpie"/>).
    /// 강화 = 대기 짧아짐(규칙) · 돌진 빨라짐 · 깃털 많아짐(두 번 연사) · 착지 빈틈 짧아짐. 날아다녀 NavMesh 없이 방 안에서 움직인다.
    /// 수치는 <see cref="MagpieBossTuning"/>. 애니메이터가 있으면 예전 컨트롤러의 신호(ChargeReady · Charge · Dive · Land · Die)를 보낸다.
    /// (2026-11-27 보스 종마다 나눔 — 동작 · 수치 · 순서는 예전 Boss 그대로.)
    /// </summary>
    public sealed class MagpieBoss : Boss
    {
        private static readonly int ChargeReadyId = Animator.StringToHash("ChargeReady");
        private static readonly int ChargeId = Animator.StringToHash("Charge");
        private static readonly int DiveId = Animator.StringToHash("Dive");
        private static readonly int LandId = Animator.StringToHash("Land");
        private static readonly int DieId = Animator.StringToHash("Die");

        [SerializeField] private MagpieBossTuning tuning;
        [Tooltip("돌진 길 예고(1 × 1 상자, 길이 · 폭은 실행 때 맞춤)")]
        [SerializeField] private GameObject laneWarning;
        [Tooltip("내려찍기 범위 예고(원)")]
        [SerializeField] private GameObject slamWarning;
        [Tooltip("날개 애니메이터(몸통과 같은 신호, 없으면 건너뜀)")]
        [SerializeField] private Animator wingAnimator;

        private readonly List<FeatherProjectile> feathers = new List<FeatherProjectile>();

        public override BossTuning Tuning => tuning;
        public MagpieBossTuning MagpieTuning => tuning;
        public GameObject LaneWarning => laneWarning;
        public GameObject SlamWarning => slamWarning;
        protected override float ContactHeight => tuning.slamHeight;

        protected override void Awake()
        {
            base.Awake();
            // 예고 표시는 보스를 따라 움직이지 않게 방(부모) 밑으로 옮긴다
            laneWarning.transform.SetParent(transform.parent, true);
            slamWarning.transform.SetParent(transform.parent, true);
            HideWarnings();
        }

        private void OnDestroy()
        {
            if (laneWarning != null) Destroy(laneWarning);
            if (slamWarning != null) Destroy(slamWarning);
        }

        public override void ResetAnimation()
        {
            base.ResetAnimation();
            ResetAnimator(wingAnimator);
        }

        protected override IEnumerator RunPattern(BossPattern pattern)
        {
            switch (pattern)
            {
                case BossPattern.Charge: return Charge();
                case BossPattern.FeatherVolley: return FeatherVolley();
                default: return Slam();
            }
        }

        protected override void OnStopped()
        {
            HideWarnings();
            // 날아가는 깃털도 거둔다(방을 깬 뒤 맞지 않게)
            foreach (FeatherProjectile feather in feathers) if (feather != null) Destroy(feather.gameObject);
            feathers.Clear();
        }

        // ---------- 패턴 1: 돌진 ----------

        private IEnumerator Charge()
        {
            Vector3 start = Flat(transform.position);
            Vector3 direction = DirectionToPlayer();
            float length = DistanceToArenaEdge(start, direction, tuning.chargeDistance);
            if (length < tuning.bodyRadius * 2f)
            {
                // 플레이어가 보스와 벽 사이에 끼어 갈 길이 없으면 방 가운데 쪽으로
                Vector3 toCenter = Flat(ArenaCenter - start);
                if (toCenter.sqrMagnitude > 0.01f)
                {
                    direction = toCenter.normalized;
                    length = DistanceToArenaEdge(start, direction, tuning.chargeDistance);
                }
            }
            Vector3 end = start + direction * length;

            // 예고: 돌진 길을 바닥에 길게 표시 — 이 뒤로 방향은 바뀌지 않는다
            Step = "돌진 예고";
            transform.rotation = Quaternion.LookRotation(direction);
            laneWarning.transform.SetPositionAndRotation(
                new Vector3((start.x + end.x) * 0.5f, GroundY + 0.02f, (start.z + end.z) * 0.5f), Quaternion.LookRotation(direction));
            laneWarning.transform.localScale = new Vector3(tuning.chargeLaneWidth, laneWarning.transform.localScale.y, Mathf.Max(0.1f, length + tuning.bodyRadius));
            laneWarning.SetActive(true);
            Signal(ChargeReadyId);
            yield return Shiver(tuning.chargeWindup, 0.06f);
            laneWarning.SetActive(false);

            Step = "돌진";
            Signal(ChargeId);
            float speed = tuning.chargeSpeed * (IsEnraged ? tuning.enragedChargeSpeedMultiplier : 1f);
            bool hit = false;
            while (Flat(end - transform.position).magnitude > 0.01f)
            {
                Vector3 next = Vector3.MoveTowards(Flat(transform.position), end, speed * Time.deltaTime);
                transform.position = new Vector3(next.x, GroundY, next.z);
                if (!hit) hit = Contact(tuning.chargeDamage, tuning.bodyRadius * 1.1f, tuning.chargeName);
                yield return null;
            }
            Signal(LandId);
            Step = "빈틈";
            yield return Idle(tuning.chargeRecover);
        }

        // ---------- 패턴 2: 깃털 날리기 ----------

        private IEnumerator FeatherVolley()
        {
            Step = "깃털 준비";
            for (float t = 0f; t < tuning.featherWindup; t += Time.deltaTime)
            {
                FacePlayer(tuning.turnSpeed * 2f);
                Body.transform.localScale = BodyRestScale * (1f + 0.06f * Mathf.Sin(t / Mathf.Max(0.01f, tuning.featherWindup) * Mathf.PI));
                yield return null;
            }
            Body.transform.localScale = BodyRestScale;

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
            if (tuning.featherPrefab == null || Player == null) return;
            feathers.RemoveAll(feather => feather == null); // 맞거나 수명이 다한 깃털
            Vector3 spawn = transform.position + Vector3.up * tuning.featherSpawnHeight + DirectionToPlayer() * tuning.bodyRadius;
            Vector3 aim = Player.transform.position + Vector3.up * tuning.playerCenterHeight - spawn;
            if (aim.sqrMagnitude < 0.0001f) aim = transform.forward;
            aim.Normalize();
            foreach (float angle in BossShapes.FanAngles(count, tuning.featherSpread, offsetHalf))
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * aim;
                FeatherProjectile feather = Instantiate(tuning.featherPrefab, spawn, Quaternion.LookRotation(direction), transform.parent);
                feather.Launch(Player, tuning);
                feathers.Add(feather);
            }
        }

        // ---------- 패턴 3: 급강하 내려찍기 ----------

        private IEnumerator Slam()
        {
            float airY = GroundY + tuning.slamHeight;
            Vector3 from = transform.position;

            Step = "떠오름";
            for (float t = 0f; t < tuning.slamRiseTime; t += Time.deltaTime)
            {
                float k = t / tuning.slamRiseTime;
                transform.position = new Vector3(from.x, Mathf.Lerp(GroundY, airY, k * (2f - k)), from.z);
                yield return null;
            }

            // 공중에서 플레이어 위를 따라다니고, 바닥 예고 원이 밑을 따라온다
            Step = "추적";
            slamWarning.transform.localScale = new Vector3(tuning.slamRadius * 2f, slamWarning.transform.localScale.y, tuning.slamRadius * 2f);
            MoveSlamWarning();
            slamWarning.SetActive(true);
            for (float t = 0f; t < tuning.slamTrackTime; t += Time.deltaTime)
            {
                Vector3 target = Player != null ? ClampToArena(Flat(Player.transform.position)) : Flat(transform.position);
                Vector3 next = Vector3.MoveTowards(Flat(transform.position), target, tuning.slamTrackSpeed * Time.deltaTime);
                transform.position = new Vector3(next.x, airY, next.z);
                MoveSlamWarning();
                yield return null;
            }

            // 낙하 지점 고정 — 예고 원이 멈춘 이 시간이 피할 시간
            Step = "고정";
            Signal(DiveId);
            yield return Shiver(tuning.slamLockTime, 0.1f);

            Step = "낙하";
            Vector3 top = transform.position;
            for (float t = 0f; t < tuning.slamFallTime; t += Time.deltaTime)
            {
                float k = t / tuning.slamFallTime;
                transform.position = new Vector3(top.x, Mathf.Lerp(airY, GroundY, k * k), top.z);
                yield return null;
            }
            transform.position = new Vector3(top.x, GroundY, top.z);
            slamWarning.SetActive(false);

            // 원 안 + 땅에 붙어 있으면 맞는다(점프로 높이 떠 있거나 구르기 무적이면 피함)
            bool hit = false;
            if (Player != null && !Player.IsDead)
            {
                Vector3 p = Player.transform.position;
                bool inside = Flat(p - transform.position).magnitude <= tuning.slamRadius;
                bool grounded = p.y <= GroundY + tuning.slamJumpClearance;
                if (inside && grounded) hit = Damage(tuning.slamDamage, tuning.slamName);
            }
            NotifySlammed(hit);
            Signal(LandId);
            Step = "착지";
            yield return Idle(IsEnraged ? tuning.enragedSlamRecover : tuning.slamRecover);
        }

        private void MoveSlamWarning()
        {
            slamWarning.transform.position = new Vector3(transform.position.x, GroundY + 0.02f, transform.position.z);
        }

        // ---------- 쓰러짐 ----------

        /// <summary>쓰러지기: 쓰러지는 동작이 있으면 그것, 없으면 옆으로 기운다. 공중에서 죽으면 바닥으로 내려앉는다.</summary>
        protected override IEnumerator FallDown()
        {
            Animator animator = Animator;
            bool hasDeath = HasParameter(animator, DieId);
            if (hasDeath) animator.SetTrigger(DieId);
            else if (animator != null) animator.speed = 0f;
            if (wingAnimator != null) wingAnimator.speed = 0f;

            Quaternion startRotation = transform.rotation;
            Quaternion endRotation = hasDeath ? startRotation : startRotation * Quaternion.Euler(0f, 0f, 80f);
            Vector3 startPosition = transform.position;
            Vector3 endPosition = new Vector3(startPosition.x, GroundY, startPosition.z);
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

        // ---------- 도움 ----------

        private void HideWarnings()
        {
            if (laneWarning != null) laneWarning.SetActive(false);
            if (slamWarning != null) slamWarning.SetActive(false);
        }

        protected override void Signal(int trigger)
        {
            base.Signal(trigger);
            if (HasParameter(wingAnimator, trigger)) wingAnimator.SetTrigger(trigger);
        }
    }
}
