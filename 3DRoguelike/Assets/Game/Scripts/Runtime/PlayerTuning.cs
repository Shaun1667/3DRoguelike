using UnityEngine;

namespace Game
{
    /// <summary>플레이어 조작 수치. 값의 기준은 BALANCE.md 1번.</summary>
    [CreateAssetMenu(menuName = "Game/Player Tuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        [Min(0f)] public float moveSpeed = 4f;
        [Tooltip("Shift 달리기: 이동 속도와 걷기 동작 재생 속도에 곱한다. 조준 · 공격 · 구르기 중에는 달리지 않는다")]
        [Min(1f)] public float sprintMultiplier = 1.6f;
        public float gravity = -20f;
        [Tooltip("스페이스바 점프 높이(m). 뛰어오르는 속도 = √(2 × 높이 × |중력|)")]
        [Min(0f)] public float jumpHeight = 1.2f;
        [Tooltip("마우스 1픽셀당 회전 각도")]
        [Min(0f)] public float lookSensitivity = 0.08f;
        public float minPitch = -30f;
        public float maxPitch = 60f;
        [Tooltip("이동 동작이 입력을 따라가는 속도")]
        [Min(0f)] public float animationBlendSpeed = 8f;
        [Tooltip("걷기 동작 재생 배속. Setup 메뉴가 컨트롤러에 넣는다")]
        [Min(0f)] public float walkPlaybackSpeed = 2.4f;
        [Tooltip("Beast가 이 값 이상이고 Human보다 높으면 걷기 · 달리기 · 점프가 Mutant 동작으로 바뀐다(그 판 동안)")]
        [Min(0)] public int mutantFormBeast = 10;

        [Header("체력")]
        [Min(1)] public int playerMaxHealth = 100;
        [Tooltip("맞은 뒤 무적 시간(초)")]
        [Min(0f)] public float hitInvulnerableTime = 0.5f;
        [Min(0f)] public float hitFlashTime = 0.08f;

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

        [Header("권총")]
        [Min(0)] public int pistolDamage = 6;
        [Tooltip("다음 발까지 최소 간격(초)")]
        [Min(0f)] public float pistolFireInterval = 0.22f;
        [Min(1)] public int pistolMagazine = 12;
        [Min(0f)] public float pistolReloadTime = 1.2f;
        [Min(0f)] public float pistolRange = 40f;
        [Tooltip("탄도 선이 보이는 시간(초)")]
        [Min(0f)] public float tracerTime = 0.05f;

        [Header("구르기")]
        [Min(0f)] public float rollSpeed = 6f;
        [Tooltip("구르는 시간(초). Setup 메뉴가 동작 재생 배속을 이 길이에 맞춘다")]
        [Min(0.01f)] public float rollDuration = 0.8f;
        [Tooltip("구르기 시작부터 다음 구르기까지(초)")]
        [Min(0f)] public float rollCooldown = 1f;
        [Tooltip("구르는 시간 중 앞쪽 몇 %가 무적인가")]
        [Range(0f, 1f)] public float rollInvulnerableRatio = 0.5f;

        [Header("조준")]
        [Range(0f, 1f)] public float aimMoveMultiplier = 0.6f;
        [Min(0f)] public float cameraDistance = 3f;
        [Min(0f)] public float aimCameraDistance = 1.7f;
        [Tooltip("조준 카메라와 오른팔이 따라가는 속도 (전환 1회 = 1 / 이 값 초)")]
        [Min(0f)] public float aimBlendSpeed = 6f;
        [Tooltip("어깨에서 손까지 거리")]
        [Min(0f)] public float aimArmLength = 0.55f;
    }
}