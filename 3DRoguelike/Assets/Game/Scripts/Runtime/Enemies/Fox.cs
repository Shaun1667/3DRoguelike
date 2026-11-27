using UnityEngine;
using UnityEngine.AI;

namespace Game
{
    /// <summary>
    /// 여우(도시, 히트앤런): 빠르게 다가와 앞이 트인 거리에 들어오면 웅크렸다가(예고, 바닥에 달려들 길) 플레이어가 있던 곳으로 달려들어 문다.
    /// 문 뒤에는 플레이어 반대쪽으로 뛰어 물러나고(빈틈), 잠깐 옆걸음으로 노린 뒤 다시 다가온다.
    /// 달려들 곳은 웅크리기 시작할 때 정해지므로 옆으로 비키거나 구르기로 넘긴다.
    /// E 개체는 두 번에 한 번 연속 달려들기(첫 번째 뒤 짧게 다시 웅크려 지금 자리로 한 번 더).
    /// </summary>
    public sealed class Fox : Enemy
    {
        private enum State
        {
            Chasing,
            WindingUp,
            Lunging,
            Retreating,
            Stalking,
        }

        [SerializeField] private FoxTuning tuning;
        [Tooltip("달려들 길 예고(앞으로 길게, 길이 = 가장 멀리 달려드는 거리)")]
        [SerializeField] private GameObject lungeWarning;

        private State state;
        private bool chain;
        private bool secondLunge;
        private float windupDuration;
        private Vector3 lungeStart;
        private Vector3 lungeTarget;
        private float lungeReach;
        private bool hitThisLunge;
        private Vector3 retreatDirection;
        private float retreated;
        private float stalkSide = 1f;
        private Vector3 laneFullScale;
        private Vector3 laneFullPosition;

        public override EnemyTuning Tuning => tuning;
        protected override float StoppingDistance => 0.8f;

        protected override void Awake()
        {
            laneFullScale = lungeWarning.transform.localScale;
            laneFullPosition = lungeWarning.transform.localPosition;
            base.Awake();
        }

        protected override void HideWarnings() => lungeWarning.SetActive(false);

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
                    if (offset.magnitude <= tuning.lungeStartRange && ClearLunge()) BeginWindup(offset, false);
                    else ChasePlayer();
                    break;
                case State.WindingUp:
                    if (TimerDone) BeginLunge();
                    else ShowLane(TimerProgress(windupDuration));
                    break;
                case State.Lunging:
                    Lunge();
                    break;
                case State.Retreating:
                    Retreat();
                    break;
                case State.Stalking:
                    Stalk();
                    break;
            }
        }

        private void BeginWindup(Vector3 offset, bool again)
        {
            StopMoving();
            if (!again) chain = NextAttackIsExtra();
            secondLunge = again;
            Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
            Face(direction);
            lungeStart = transform.position;
            lungeReach = Mathf.Min(offset.magnitude + tuning.lungeOvershoot, tuning.lungeDistance);
            lungeTarget = lungeStart + direction * lungeReach;
            windupDuration = again ? tuning.chainWindupTime : tuning.windupTime;
            lungeWarning.SetActive(true);
            state = State.WindingUp;
            StartTimer(windupDuration);
            ShowLane(0f);
            PlayAnimation(again ? EnemyAnim.Combo2 : EnemyAnim.Attack); // 웅크림 + 달려들기, 끝이 무는 순간
        }

        /// <summary>예고 진행(0~1): 몸을 낮추고 달려들 길이 앞으로 뻗는다(길이는 이번에 달려들 거리).</summary>
        private void ShowLane(float progress)
        {
            Pose(-tuning.windupLeanAngle * progress, -0.06f * progress);
            float share = tuning.lungeDistance > 0.001f ? lungeReach / tuning.lungeDistance : 1f;
            float length = Mathf.Max(0.05f, progress) * share;
            lungeWarning.transform.localScale = new Vector3(laneFullScale.x, laneFullScale.y, laneFullScale.z * length);
            lungeWarning.transform.localPosition = new Vector3(laneFullPosition.x, laneFullPosition.y, laneFullPosition.z * length);
        }

        private void BeginLunge()
        {
            lungeWarning.SetActive(false);
            lungeStart = transform.position;
            hitThisLunge = false;
            state = State.Lunging;
            StartTimer(tuning.lungeTime);
        }

        private void Lunge()
        {
            float t = TimerProgress(tuning.lungeTime);
            Pose(tuning.strikeLeanAngle);
            MoveStraight(Vector3.Lerp(lungeStart, lungeTarget, t) - transform.position); // 막히면 거기서 멈춘다
            if (!hitThisLunge && PlayerWithin(transform.position, tuning.hitRadius)
                && HitPlayer(secondLunge ? tuning.chainDamage : tuning.damage, secondLunge ? tuning.chainName : tuning.attackName))
            {
                hitThisLunge = true;
            }
            if (!TimerDone) return;

            if (chain && !secondLunge && CanAct && ClearLunge()) BeginWindup(ToPlayer, true);
            else BeginRetreat();
        }

        /// <summary>문 뒤 플레이어 반대쪽으로 뛰어 물러난다(얼굴은 플레이어 쪽). 처음에 빨리, 끝에 느리게.</summary>
        private void BeginRetreat()
        {
            Vector3 away = Player != null ? -ToPlayer : -transform.forward;
            retreatDirection = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
            Face(-retreatDirection);
            retreated = 0f;
            state = State.Retreating;
            StartTimer(tuning.retreatTime);
            PlayAnimation(EnemyAnim.AttackRecover);
        }

        private void Retreat()
        {
            float p = TimerProgress(tuning.retreatTime);
            float wanted = tuning.retreatDistance * (1f - (1f - p) * (1f - p));
            Pose(tuning.strikeLeanAngle * (1f - p));
            MoveStraight(retreatDirection * (wanted - retreated));
            retreated = wanted;
            if (TimerDone) BeginStalk();
        }

        /// <summary>옆걸음으로 노린다(번갈아 왼쪽 · 오른쪽). 이 동안은 달려들지 않는다.</summary>
        private void BeginStalk()
        {
            Pose(0f);
            stalkSide = -stalkSide;
            Agent.speed = tuning.stalkSpeed;
            state = State.Stalking;
            StartTimer(tuning.stalkTime);
            PlayLocomotion();
        }

        private void Stalk()
        {
            if (!CanAct || TimerDone)
            {
                ResumeChase();
                return;
            }
            Vector3 toPlayer = ToPlayer;
            if (toPlayer.sqrMagnitude < 0.0001f) return;
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer.normalized) * stalkSide;
            MoveTo(transform.position + side * 2f);
        }

        /// <summary>
        /// 달려들 길이 트였다. 플레이어가 벽에 붙어 NavMesh 밖(에이전트 반지름만큼 깎인 곳)에 서 있으면 그 가까운 NavMesh 점까지 본다
        /// (시험 방에서 벽에 붙은 플레이어 앞 0.8 m에 멈춰 공격하지 않던 일, 2026-11-27).
        /// </summary>
        private bool ClearLunge()
        {
            if (ClearLineToPlayer()) return true;
            return NavMesh.SamplePosition(Player.transform.position, out NavMeshHit near, 1f, NavMesh.AllAreas)
                && !NavMesh.Raycast(transform.position, near.position, out _, NavMesh.AllAreas);
        }

        private void ResumeChase()
        {
            Pose(0f);
            Agent.speed = tuning.moveSpeed;
            PlayLocomotion();
            state = State.Chasing;
        }
    }
}
