using UnityEngine;

namespace Game
{
    /// <summary>
    /// 멧돼지(돌진 탱커): 앞이 트인 거리에 들어오면 멈춰 예고하고(바닥에 돌진 길), 그 방향으로 곧장 돌진한다.
    /// 돌진에 부딪히면 피해 + 밀림, 벽 · 나무에 박으면 기절(머리 위 노란 별). 박지 않으면 짧은 빈틈.
    /// E 개체는 두 번에 한 번 돌진 끝에서 내려찍기(둥근 예고 뒤 둘레 피해).
    /// </summary>
    public sealed class Boar : Enemy
    {
        private enum State
        {
            Chasing,
            WindingUp,
            Charging,
            SlamWindup,
            Stunned,
            Recovering,
        }

        [SerializeField] private BoarTuning tuning;
        [Tooltip("돌진 길 예고(앞으로 길게)")]
        [SerializeField] private GameObject laneWarning;
        [Tooltip("내려찍기 범위 예고(둥글게)")]
        [SerializeField] private GameObject slamWarning;
        [Tooltip("기절 표시(머리 위에서 돈다)")]
        [SerializeField] private GameObject stunMark;

        private State state;
        private Vector3 chargeDirection;
        private float travelled;
        private bool hitThisCharge;
        private bool slamAfterCharge;
        private float recoverDuration;
        private Vector3 laneFullScale;
        private Vector3 laneFullPosition;
        private Vector3 slamFullScale;

        public override EnemyTuning Tuning => tuning;
        protected override float StoppingDistance => 1.5f;

        protected override void Awake()
        {
            laneFullScale = laneWarning.transform.localScale;
            laneFullPosition = laneWarning.transform.localPosition;
            slamFullScale = slamWarning.transform.localScale;
            base.Awake();
        }

        protected override void HideWarnings()
        {
            laneWarning.SetActive(false);
            slamWarning.SetActive(false);
            stunMark.SetActive(false);
        }

        protected override void OnAppear() => ResumeChase();

        protected override void Tick()
        {
            switch (state)
            {
                case State.Chasing:
                    if (!CanAct)
                    {
                        WaitForPlayer();
                        return;
                    }
                    Vector3 offset = ToPlayer;
                    if (offset.magnitude <= tuning.chargeStartRange && ClearLineToPlayer()) BeginWindup(offset);
                    else ChasePlayer();
                    break;
                case State.WindingUp:
                    if (TimerDone) BeginCharge();
                    else ShowLane(TimerProgress(tuning.windupTime));
                    break;
                case State.Charging:
                    Charge();
                    break;
                case State.SlamWindup:
                    if (TimerDone) Slam();
                    else ShowSlam(TimerProgress(tuning.slamWindupTime));
                    break;
                case State.Stunned:
                    stunMark.transform.Rotate(0f, 360f * Time.deltaTime, 0f, Space.Self);
                    if (TimerDone)
                    {
                        stunMark.SetActive(false);
                        ResumeChase();
                    }
                    break;
                case State.Recovering:
                    if (TimerDone) ResumeChase();
                    else Pose(tuning.strikeLeanAngle * (1f - TimerProgress(recoverDuration)));
                    break;
            }
        }

        private void BeginWindup(Vector3 offset)
        {
            StopMoving();
            slamAfterCharge = NextAttackIsExtra();
            chargeDirection = offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
            Face(chargeDirection);
            laneWarning.SetActive(true);
            state = State.WindingUp;
            StartTimer(tuning.windupTime);
            ShowLane(0f);
            PlayAnimation(EnemyAnim.ChargeWindup);
        }

        /// <summary>예고 진행(0~1): 몸을 뒤로 젖히고 돌진 길이 앞으로 뻗는다.</summary>
        private void ShowLane(float progress)
        {
            Pose(-tuning.windupLeanAngle * progress);
            float length = Mathf.Max(0.05f, progress);
            laneWarning.transform.localScale = new Vector3(laneFullScale.x, laneFullScale.y, laneFullScale.z * length);
            laneWarning.transform.localPosition = new Vector3(laneFullPosition.x, laneFullPosition.y, laneFullPosition.z * length);
        }

        private void BeginCharge()
        {
            laneWarning.SetActive(false);
            Pose(tuning.strikeLeanAngle);
            travelled = 0f;
            hitThisCharge = false;
            state = State.Charging;
            PlayAnimation(EnemyAnim.Charge);
        }

        private void Charge()
        {
            float step = Mathf.Min(tuning.chargeSpeed * Time.deltaTime, tuning.chargeDistance - travelled);
            bool blocked = !MoveStraight(chargeDirection * step);
            travelled += step;

            if (!hitThisCharge && PlayerWithin(transform.position, tuning.hitRadius) && HitPlayer(tuning.damage, tuning.attackName))
            {
                hitThisCharge = true;
                // 돌진 방향으로, 플레이어가 서 있는 쪽 옆으로 조금 비껴 민다
                Vector3 side = Vector3.Cross(Vector3.up, chargeDirection);
                if (Vector3.Dot(side, ToPlayer) < 0f) side = -side;
                PushPlayer((chargeDirection + side * 0.5f).normalized * tuning.knockbackSpeed);
            }

            if (blocked)
            {
                BeginStun();
                return;
            }
            if (travelled < tuning.chargeDistance) return;

            if (slamAfterCharge)
            {
                slamWarning.SetActive(true);
                state = State.SlamWindup;
                StartTimer(tuning.slamWindupTime);
                ShowSlam(0f);
                PlayAnimation(EnemyAnim.Slam); // 끝이 내려찍는 순간
            }
            else
            {
                Recover(tuning.recoverTime);
                PlayLocomotion();
            }
        }

        /// <summary>내려찍기 예고(0~1): 몸을 젖히고 둥근 범위가 넓어진다.</summary>
        private void ShowSlam(float progress)
        {
            Pose(-tuning.windupLeanAngle * progress);
            float size = Mathf.Max(0.05f, progress);
            slamWarning.transform.localScale = new Vector3(slamFullScale.x * size, slamFullScale.y, slamFullScale.z * size);
        }

        private void Slam()
        {
            slamWarning.SetActive(false);
            Pose(tuning.strikeLeanAngle);
            if (PlayerWithin(transform.position, tuning.slamRadius)) HitPlayer(tuning.slamDamage, tuning.slamName);
            Recover(tuning.slamRecoverTime);
            PlayAnimation(EnemyAnim.SlamRecover);
        }

        private void BeginStun()
        {
            Pose(0f);
            stunMark.SetActive(true);
            state = State.Stunned;
            StartTimer(tuning.stunTime);
            PlayAnimation(EnemyAnim.Stun, 0.15f);
        }

        private void Recover(float duration)
        {
            state = State.Recovering;
            recoverDuration = duration;
            StartTimer(duration);
        }

        private void ResumeChase()
        {
            Pose(0f);
            PlayLocomotion();
            state = State.Chasing;
        }
    }
}
