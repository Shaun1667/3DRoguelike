using UnityEngine;

namespace Game
{
    /// <summary>
    /// 오소리(기본 근접, 2-4 Mutant 동작 그대로): 쫓아와 가까워지면 멈춰 예고한 뒤 앞을 할퀸다. 예고를 시작할 때 방향이 고정되므로
    /// 옆으로 피하거나 구르기 무적으로 넘길 수 있다. 할퀸 뒤에는 빈틈이 있다.
    /// E 개체는 두 번에 한 번 3연속 할퀴기(같은 예고 뒤 앞으로 내디디며 세 번).
    /// </summary>
    public sealed class Badger : Enemy
    {
        private enum State
        {
            Chasing,
            WindingUp,
            Combo,
            Recovering,
        }

        [SerializeField] private BadgerTuning tuning;
        [Tooltip("할퀴기 예고 때 맞는 범위 표시")]
        [SerializeField] private GameObject attackWarning;

        private State state;
        private Vector3 attackForward;
        private Vector3 warningFullScale;
        private Vector3 warningFullPosition;
        private bool combo;
        private int comboLeft;
        private float recoverDuration;

        public override EnemyTuning Tuning => tuning;
        protected override float StoppingDistance => tuning.attackStartRange * 0.8f;

        protected override void Awake()
        {
            warningFullScale = attackWarning.transform.localScale;
            warningFullPosition = attackWarning.transform.localPosition;
            base.Awake();
        }

        protected override void HideWarnings() => attackWarning.SetActive(false);

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
                    if (offset.magnitude <= tuning.attackStartRange) BeginWindup(offset);
                    else ChasePlayer();
                    break;
                case State.WindingUp:
                    if (!TimerDone)
                    {
                        ShowWindup(TimerProgress(tuning.windupTime));
                        break;
                    }
                    if (combo)
                    {
                        comboLeft = tuning.comboHits;
                        ComboHit();
                    }
                    else
                    {
                        attackWarning.SetActive(false);
                        Strike(tuning.damage, tuning.attackName);
                        Recover(tuning.recoverTime);
                        PlayAnimation(EnemyAnim.AttackRecover);
                    }
                    break;
                case State.Combo:
                    if (TimerDone) ComboHit();
                    else Pose(tuning.strikeLeanAngle * (1f - TimerProgress(tuning.comboInterval)));
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
            combo = NextAttackIsExtra();
            attackForward = offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
            Face(attackForward);
            attackWarning.SetActive(true);
            state = State.WindingUp;
            StartTimer(tuning.windupTime);
            ShowWindup(0f);
            PlayAnimation(EnemyAnim.Attack); // 예고 동작의 끝이 할퀴는 순간(한 번 · 3연속 첫 번째 공통)
        }

        /// <summary>예고 진행(0~1): 몸을 뒤로 젖히고, 바닥 범위가 발밑에서 앞으로 차오른다. 다 차면 할퀸다.</summary>
        private void ShowWindup(float progress)
        {
            Pose(-tuning.windupLeanAngle * progress);
            float length = Mathf.Max(0.05f, progress);
            attackWarning.transform.localScale = new Vector3(warningFullScale.x, warningFullScale.y, warningFullScale.z * length);
            attackWarning.transform.localPosition = new Vector3(warningFullPosition.x, warningFullPosition.y, warningFullPosition.z * length);
        }

        /// <summary>3연속 할퀴기의 한 번: 앞으로 내디디며 할퀸다. 범위 표시는 세 번 내내 켜 둔다.</summary>
        private void ComboHit()
        {
            MoveStraight(attackForward * tuning.comboStep);
            Strike(tuning.comboDamage, tuning.comboName);
            comboLeft--;
            if (comboLeft > 0)
            {
                state = State.Combo;
                StartTimer(tuning.comboInterval);
                // 다음 할퀴기 동작: 끝이 다음에 맞히는 순간(간격 뒤)
                PlayAnimation(comboLeft == tuning.comboHits - 1 ? EnemyAnim.Combo2 : EnemyAnim.Combo3);
                return;
            }
            attackWarning.SetActive(false);
            Recover(tuning.comboRecoverTime);
            PlayAnimation(EnemyAnim.ComboRecover);
        }

        private void Strike(int damage, string attackName)
        {
            Pose(tuning.strikeLeanAngle);
            if (Player == null) return;
            Vector3 offset = ToPlayer;
            bool inRange = offset.magnitude <= tuning.hitRange;
            bool inFront = offset.sqrMagnitude < 0.0001f || Vector3.Angle(attackForward, offset) <= tuning.hitAngle * 0.5f;
            if (inRange && inFront) HitPlayer(damage, attackName);
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
