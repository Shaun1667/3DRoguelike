using UnityEngine;

namespace Game
{
    /// <summary>플레이어 조작 수치. 값의 기준은 BALANCE.md 1번.</summary>
    [CreateAssetMenu(menuName = "Game/Player Tuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        [Min(0f)] public float moveSpeed = 4f;
        public float gravity = -20f;
        [Tooltip("마우스 1픽셀당 회전 각도")]
        [Min(0f)] public float lookSensitivity = 0.08f;
        public float minPitch = -30f;
        public float maxPitch = 60f;
        [Tooltip("이동 동작이 입력을 따라가는 속도")]
        [Min(0f)] public float animationBlendSpeed = 8f;
        [Tooltip("걷기 동작 재생 배속. Setup 메뉴가 컨트롤러에 넣는다")]
        [Min(0f)] public float walkPlaybackSpeed = 2.4f;

        [Header("맨몸 근접")]
        [Min(0)] public int meleeDamage = 10;
        [Min(0f)] public float meleeRange = 2.2f;
        [Tooltip("정면 기준 전체 각도")]
        [Range(0f, 360f)] public float meleeAngle = 110f;
        [Tooltip("공격 시작부터 손이 가장 앞으로 뻗는 순간까지. 동작의 그 시점(1.05초) ÷ 재생 배속")]
        [Min(0f)] public float meleeHitDelay = 0.35f;
        [Tooltip("공격 시작부터 다시 움직이거나 공격할 수 있을 때까지")]
        [Min(0f)] public float meleeLockTime = 0.75f;
        [Tooltip("공격 동작 재생 배속. Setup 메뉴가 컨트롤러에 넣는다")]
        [Min(0f)] public float meleePlaybackSpeed = 3f;
    }
}