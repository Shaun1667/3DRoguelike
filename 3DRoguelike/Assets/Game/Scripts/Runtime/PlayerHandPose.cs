using UnityEngine;

namespace Game
{
    /// <summary>
    /// 손 모양. 애니메이터의 손가락 레이어(Hands)가 모든 동작 위에 평소 손을 덮고,
    /// 여기서는 조준 중이면 권총 쥔 손, 근접 공격 중이면 할퀴는 손으로 섞는 값만 넘긴다.
    /// 모양 값은 <see cref="HandPoseTable"/>(Data/HandPoses.asset), 레이어 · 클립은 HandPoseSetup이 만든다. Animator가 있는 오브젝트에 붙인다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerHandPose : MonoBehaviour
    {
        public const string GripParameter = "HandGrip";
        public const string ClawParameter = "HandClaw";

        private static readonly int GripId = Animator.StringToHash(GripParameter);
        private static readonly int ClawId = Animator.StringToHash(ClawParameter);

        [SerializeField] private HandPoseTable poses;
        [SerializeField] private PlayerGun gun;
        [SerializeField] private PlayerMelee melee;

        private Animator animator;
        private float grip;
        private float claw;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            float step = poses.blendSpeed * Time.deltaTime;
            grip = Mathf.MoveTowards(grip, gun.IsAiming ? 1f : 0f, step);
            claw = Mathf.MoveTowards(claw, melee.IsAttacking ? 1f : 0f, step);
            // 매 프레임 넘긴다: Mutant 모습으로 컨트롤러가 바뀌면 매개변수가 처음 값으로 돌아간다
            animator.SetFloat(GripId, grip);
            animator.SetFloat(ClawId, claw);
        }
    }
}
