using UnityEngine;

namespace Game
{
    /// <summary>
    /// E-07 마을 주민(사람 몸 + 동물 머리, 우호적 · 전투 없음). 프리팹 하나 = 주민 한 종(Prefabs/Villagers/Villager_{id}).
    /// Mixamo 리깅 모델(Humanoid)에 대기 동작을 튼다. 주민끼리 똑같이 움직여 보이지 않게, 서 있는 자리로 시작 시점 · 빠르기를 조금씩 달리한다
    /// (같은 자리면 늘 같은 값). 꼬리가 있는 종은 TailChain이 꼬리를 흔든다. 대화 · 상점은 이 컴포넌트와 프리팹에 붙인다.
    /// 적은 "Player" 태그만 노리므로(Enemy.Start) 주민은 적의 대상이 아니다. 체력(IDamageable)이 없어 플레이어 공격도 받지 않는다.
    /// </summary>
    public sealed class Villager : MonoBehaviour
    {
        /// <summary>대기 동작 상태 이름 · 빠르기 파라미터(VillagerSetup이 만드는 컨트롤러와 같아야 한다).</summary>
        public const string IdleState = "Idle";
        public const string IdleSpeedParameter = "IdleSpeed";

        private static readonly int IdleStateHash = Animator.StringToHash(IdleState);
        private static readonly int IdleSpeedHash = Animator.StringToHash(IdleSpeedParameter);

        [Tooltip("종 id(VillagerTuning.species의 id)")]
        [SerializeField] private string speciesId;
        [Tooltip("보이는 이름")]
        [SerializeField] private string displayName;
        [Tooltip("몸 애니메이터(Model). 셋업이 채운다")]
        [SerializeField] private Animator animator;
        [Tooltip("대기 동작 빠르기 범위(배). 주민마다 이 안에서 정해진다")]
        [SerializeField] private Vector2 idleSpeedRange = Vector2.one;

        public string SpeciesId => speciesId;
        public string DisplayName => displayName;

        private void Start()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            Vector3 p = transform.position;
            float speed = Mathf.Lerp(idleSpeedRange.x, idleSpeedRange.y, Hash(p.x * 12.9898f + p.z * 78.233f));
            float offset = Hash(p.x * 39.346f + p.z * 11.135f + 3.7f);
            animator.SetFloat(IdleSpeedHash, speed);
            animator.Play(IdleStateHash, 0, offset);
        }

        private static float Hash(float value)
        {
            float x = Mathf.Sin(value) * 43758.5453f;
            return x - Mathf.Floor(x);
        }
    }
}
