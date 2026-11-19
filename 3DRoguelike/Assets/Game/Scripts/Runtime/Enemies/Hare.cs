using UnityEngine;

namespace Game
{
    /// <summary>
    /// 멧토끼(빠른 떼): 빠르게 쫓아와 가까워지면 웅크렸다가(예고) 플레이어가 있던 곳으로 뛰어 들이받는다. 착지 뒤 빈틈.
    /// 뛸 곳은 웅크리기 시작할 때 정해져 바닥에 보이므로, 웅크리는 걸 보고 옆으로 비키면 피한다.
    /// E 개체는 두 번에 한 번 높이 뛰어 내려찍기(착지 둘레 피해).
    /// </summary>
    public sealed class Hare : Enemy
    {
        private enum State
        {
            Chasing,
            Crouching,
            Leaping,
            Recovering,
        }

        [SerializeField] private HareTuning tuning;
        [Tooltip("착지할 곳 예고(둥글게, 월드 자리에 놓는다)")]
        [SerializeField] private GameObject landingWarning;

        private State state;
        private bool slam;
        private Vector3 leapStart;
        private Vector3 leapTarget;
        private float leapTime;
        private float leapHeight;
        private bool hitThisLeap;
        private float recoverDuration;
        private float crouchDuration;

        public override EnemyTuning Tuning => tuning;
        protected override float StoppingDistance => 0.5f;

        protected override void HideWarnings() => landingWarning.SetActive(false);

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
                    if (offset.magnitude <= tuning.leapStartRange && ClearLineToPlayer()) BeginCrouch(offset);
                    else ChasePlayer();
                    break;
                case State.Crouching:
                    // 웅크림: 앞으로 숙이며 낮아진다
                    float progress = TimerProgress(crouchDuration);
                    Pose(tuning.strikeLeanAngle * progress, -0.08f * progress);
                    if (TimerDone) BeginLeap();
                    break;
                case State.Leaping:
                    Leap();
                    break;
                case State.Recovering:
                    if (TimerDone) ResumeChase();
                    else Pose(tuning.strikeLeanAngle * 0.5f * (1f - TimerProgress(recoverDuration)));
                    break;
            }
        }

        private void BeginCrouch(Vector3 offset)
        {
            StopMoving();
            slam = NextAttackIsExtra();
            Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
            Face(direction);
            float reach = slam ? tuning.slamLeapDistance : tuning.leapDistance;
            leapStart = transform.position;
            leapTarget = leapStart + direction * Mathf.Min(offset.magnitude, reach);
            crouchDuration = slam ? tuning.slamCrouchTime : tuning.crouchTime;

            float radius = slam ? tuning.slamRadius : tuning.hitRadius;
            landingWarning.transform.localScale = new Vector3(radius * 2f, landingWarning.transform.localScale.y, radius * 2f);
            landingWarning.transform.position = leapTarget + Vector3.up * 0.02f;
            landingWarning.SetActive(true);
            state = State.Crouching;
            StartTimer(crouchDuration);
            PlayAnimation(slam ? EnemyAnim.SlamCrouch : EnemyAnim.Crouch);
        }

        private void BeginLeap()
        {
            leapStart = transform.position;
            leapTime = slam ? tuning.slamLeapTime : tuning.leapTime;
            leapHeight = slam ? tuning.slamLeapHeight : tuning.leapHeight;
            hitThisLeap = false;
            if (!slam) landingWarning.SetActive(false);
            state = State.Leaping;
            StartTimer(leapTime);
            PlayAnimation(slam ? EnemyAnim.SlamLeap : EnemyAnim.Leap);
        }

        private void Leap()
        {
            float t = TimerProgress(leapTime);
            Vector3 wanted = Vector3.Lerp(leapStart, leapTarget, t);
            MoveStraight(wanted - transform.position); // 막히면 거기서 제자리 뛰기
            Pose(-10f, 4f * leapHeight * t * (1f - t));
            if (slam) landingWarning.transform.position = leapTarget + Vector3.up * 0.02f; // 몸을 따라 움직이지 않게

            if (!slam && !hitThisLeap && PlayerWithin(transform.position, tuning.hitRadius) && HitPlayer(tuning.damage, tuning.attackName))
            {
                hitThisLeap = true;
            }
            if (!TimerDone) return;

            Pose(0f);
            if (slam)
            {
                landingWarning.SetActive(false);
                if (PlayerWithin(transform.position, tuning.slamRadius)) HitPlayer(tuning.slamDamage, tuning.slamName);
                Recover(tuning.slamRecoverTime);
                PlayAnimation(EnemyAnim.SlamLand);
            }
            else
            {
                Recover(tuning.recoverTime);
                PlayAnimation(EnemyAnim.Land);
            }
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
