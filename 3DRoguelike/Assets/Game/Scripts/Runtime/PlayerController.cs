using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// TPS 이동과 시점. 몸은 항상 카메라 방향을 바라보고(좌우 회전 = 몸), 위아래는 CameraTarget만 돈다.
    /// 카메라는 CameraTarget을 따라가는 Cinemachine이 맡는다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private static readonly int MoveXId = Animator.StringToHash("MoveX");
        private static readonly int MoveYId = Animator.StringToHash("MoveY");
        private static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        private static readonly int JumpId = Animator.StringToHash("Jump");

        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMelee melee;
        [SerializeField] private PlayerGun gun;
        [SerializeField] private PlayerRoll roll;

        private CharacterController controller;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction jumpAction;
        private float pitch;
        private float verticalVelocity;
        private bool jumping;
        private Vector2 blend;
        private PlayerUpgrades upgrades;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
            lookAction = InputSystem.actions.FindAction("Player/Look", throwIfNotFound: true);
            sprintAction = InputSystem.actions.FindAction("Player/Sprint", throwIfNotFound: true);
            jumpAction = InputSystem.actions.FindAction("Player/Jump", throwIfNotFound: true);
            upgrades = GetComponent<PlayerUpgrades>();
        }

        /// <summary>땅에 서 있다(지난 이동 기준).</summary>
        public bool IsGrounded => controller.isGrounded;

        /// <summary>Shift를 누르고 움직이는 중(조준 · 공격 · 구르기 중이 아닐 때).</summary>
        public bool IsSprinting { get; private set; }

        /// <summary>디버그 · 확인용: 입력 없이 달리기를 켠다.</summary>
        public bool DebugForceSprint { get; set; }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            Look(lookAction.ReadValue<Vector2>());
            bool locked = melee.IsAttacking || roll.IsRolling;
            Vector2 input = locked ? Vector2.zero : Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            if (gun.IsAiming) input *= Mathf.Min(1f, tuning.aimMoveMultiplier * upgrades.Scale(Rules.UpgradeStat.AimMoveSpeed));
            IsSprinting = (sprintAction.IsPressed() || DebugForceSprint) && !locked && !gun.IsAiming && input.sqrMagnitude > 0.01f;
            float speed = IsSprinting ? tuning.sprintMultiplier : 1f;
            if (jumpAction.WasPressedThisFrame()) TryJump(locked);
            Move(input * speed);
            // 앞으로 달릴 때만 MoveY가 1을 넘어 달리기 동작으로 넘어간다(옆 · 뒤로 달리면 걷기 동작을 빨리 재생)
            Animate(IsSprinting && input.y > 0f ? new Vector2(input.x, input.y * speed) : input);
            // 이동 속도 강화만큼 동작도 빨리 재생해 발이 미끄러지지 않게 한다
            animator.SetFloat(MoveSpeedId, speed * upgrades.Scale(Rules.UpgradeStat.MoveSpeed));
        }

        private void Look(Vector2 delta)
        {
            transform.Rotate(0f, delta.x * tuning.lookSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - delta.y * tuning.lookSensitivity, tuning.minPitch, tuning.maxPitch);
            cameraTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>스페이스바 점프. 땅에 있고 공격 · 구르기 중이 아닐 때만 뛴다.</summary>
        public bool TryJump(bool locked)
        {
            if (locked || !controller.isGrounded) return false;
            verticalVelocity = Mathf.Sqrt(2f * tuning.jumpHeight * -tuning.gravity);
            jumping = true;
            animator.SetTrigger(JumpId);
            return true;
        }

        private void Move(Vector2 input)
        {
            // 뛰어오른 첫 프레임은 아직 isGrounded라서, 올라가는 동안에는 땅 판정으로 속도를 지우지 않는다
            if (controller.isGrounded && !(jumping && verticalVelocity > 0f))
            {
                verticalVelocity = -1f;
                jumping = false;
            }
            else
            {
                verticalVelocity += tuning.gravity * Time.deltaTime;
            }
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * tuning.moveSpeed * upgrades.Scale(Rules.UpgradeStat.MoveSpeed) + roll.Velocity;
            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void Animate(Vector2 input)
        {
            blend = Vector2.MoveTowards(blend, input, tuning.animationBlendSpeed * Time.deltaTime);
            animator.SetFloat(MoveXId, blend.x);
            animator.SetFloat(MoveYId, blend.y);
        }
    }
}