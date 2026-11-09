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
    }
}