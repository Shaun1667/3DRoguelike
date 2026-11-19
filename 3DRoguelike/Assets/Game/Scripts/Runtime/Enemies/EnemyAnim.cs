using UnityEngine;

namespace Game
{
    /// <summary>
    /// 적 리깅 모델(E 개체 = 두 발 Mixamo, Mutant = 네 발 Generic) 애니메이터의 상태 · 파라미터 이름 한 곳. 컨트롤러는 <c>EnemyAnimationSetup</c>이 이 이름으로 만들고
    /// (Art/Enemies/Animation/Controllers/{종}_E|Mutant.controller — Mutant에는 E 개체 추가 공격 상태(Combo · Slam…)가 없다, 코드도 E 개체일 때만 넘긴다), 적 클래스는 여기 해시로 상태를 넘긴다(<see cref="Enemy.PlayAnimation"/>).
    /// 상태마다 길이는 Setup이 수치 에셋의 시간(예고 · 빈틈 등)에 맞춰 재생 빠르기로 맞춘다 — 코드는 "언제 넘기나"만 안다.
    /// 새 동작 = 여기 이름 한 줄 + EnemyAnimationSetup의 종 표 한 줄 + 적 클래스에서 넘기는 한 줄.
    /// </summary>
    public static class EnemyAnim
    {
        /// <summary>이동 빠르기(m/s). 대기 ↔ 걷기 ↔ 달리기 블렌드.</summary>
        public const string SpeedParameter = "Speed";
        /// <summary>맞은 동작 레이어(더하기 — 하던 동작 위에 움찔만 얹는다).</summary>
        public const int HitLayer = 1;
        public const string HitLayerName = "Hit";

        // 공통
        public const string LocomotionState = "Locomotion";
        public const string HitState = "Hit";
        public const string DeathState = "Death";
        public const string StunState = "Stun";
        // 오소리: 예고(끝이 할퀴는 순간) → 빈틈 / 3연속은 한 번 맞힐 때마다 다음 할퀴기로
        public const string AttackState = "Attack";
        public const string AttackRecoverState = "AttackRecover";
        public const string Combo2State = "Combo2";
        public const string Combo3State = "Combo3";
        public const string ComboRecoverState = "ComboRecover";
        // 멧돼지: 돌진 예고 → 돌진 → (내려찍기 예고(끝이 찍는 순간) → 빈틈)
        public const string ChargeWindupState = "ChargeWindup";
        public const string ChargeState = "Charge";
        public const string SlamState = "Slam";
        public const string SlamRecoverState = "SlamRecover";
        // 멧토끼: 웅크림 → 공중 → 착지 (내려찍기는 Slam…)
        public const string CrouchState = "Crouch";
        public const string LeapState = "Leap";
        public const string LandState = "Land";
        public const string SlamCrouchState = "SlamCrouch";
        public const string SlamLeapState = "SlamLeap";
        public const string SlamLandState = "SlamLand";
        // 두꺼비: 예고(끝이 뱉는 순간) → 뱉은 뒤
        public const string SpitWindupState = "SpitWindup";
        public const string SpitState = "Spit";

        public static readonly int Speed = Animator.StringToHash(SpeedParameter);
        public static readonly int Locomotion = Animator.StringToHash(LocomotionState);
        public static readonly int Hit = Animator.StringToHash(HitState);
        public static readonly int Death = Animator.StringToHash(DeathState);
        public static readonly int Stun = Animator.StringToHash(StunState);
        public static readonly int Attack = Animator.StringToHash(AttackState);
        public static readonly int AttackRecover = Animator.StringToHash(AttackRecoverState);
        public static readonly int Combo2 = Animator.StringToHash(Combo2State);
        public static readonly int Combo3 = Animator.StringToHash(Combo3State);
        public static readonly int ComboRecover = Animator.StringToHash(ComboRecoverState);
        public static readonly int ChargeWindup = Animator.StringToHash(ChargeWindupState);
        public static readonly int Charge = Animator.StringToHash(ChargeState);
        public static readonly int Slam = Animator.StringToHash(SlamState);
        public static readonly int SlamRecover = Animator.StringToHash(SlamRecoverState);
        public static readonly int Crouch = Animator.StringToHash(CrouchState);
        public static readonly int Leap = Animator.StringToHash(LeapState);
        public static readonly int Land = Animator.StringToHash(LandState);
        public static readonly int SlamCrouch = Animator.StringToHash(SlamCrouchState);
        public static readonly int SlamLeap = Animator.StringToHash(SlamLeapState);
        public static readonly int SlamLand = Animator.StringToHash(SlamLandState);
        public static readonly int SpitWindup = Animator.StringToHash(SpitWindupState);
        public static readonly int Spit = Animator.StringToHash(SpitState);
    }
}
