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
        private static readonly int MoveAmountId = Animator.StringToHash("MoveAmount");
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
        private float moveAmount;
        private PlayerUpgrades upgrades;
        private Vector3 pushVelocity;

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

        /// <summary>밀림(멧돼지 돌진 등). 수평 속도를 더하고 <see cref="PlayerTuning.pushDecay"/>로 줄어든다. 벽에는 막힌다.</summary>
        public void Push(Vector3 velocity)
        {
            velocity.y = 0f;
            pushVelocity += velocity;
        }

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
            Animate(input, speed);
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
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * tuning.moveSpeed * upgrades.Scale(Rules.UpgradeStat.MoveSpeed) + roll.Velocity + pushVelocity;
            pushVelocity *= Mathf.Exp(-tuning.pushDecay * Time.deltaTime);
            if (pushVelocity.sqrMagnitude < 0.01f) pushVelocity = Vector3.zero;
            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

/// <summary>
        /// 이동 동작. 방향 트리에는 방향만 넣고(MoveX · MoveY, 걷기는 길이 1, 달리기는 길이 sprintMultiplier로 어느 방향이든 달리기 동작으로 넘어간다),
        /// 움직이는지는 MoveAmount(0 대기 ~ 1 이동)로, 빠르기는 상태 배속 MoveSpeed로 넣는다.
        /// 조준 중이거나 스틱을 덜 밀어 느리게 갈 때 대기 동작이 섞이면 발이 미끄러지므로, 걷기 동작을 그만큼 천천히 재생한다.
        /// </summary>
        private void Animate(Vector2 input, float speed)
        {
            float amount = input.magnitude;
            bool moving = amount > 0.01f;
            float step = tuning.animationBlendSpeed * Time.deltaTime;
            if (moving)
            {
                Vector2 direction = input / amount;
                if (IsSprinting) direction *= speed; // 달리기 링(반지름 sprintMultiplier): 어느 방향이든 달리기 동작
                // 서 있다가 출발할 때는 방향을 바로 잡는다(가운데에서부터 돌려 오지 않는다)
                blend = moveAmount > 0f ? Vector2.MoveTowards(blend, direction, step) : direction;
            }
            moveAmount = Mathf.MoveTowards(moveAmount, moving ? 1f : 0f, step);
            animator.SetFloat(MoveXId, blend.x);
            animator.SetFloat(MoveYId, blend.y);
            animator.SetFloat(MoveAmountId, moveAmount);
            // 이동 속도 강화만큼 동작도 빨리 재생해 발이 미끄러지지 않게 한다
            animator.SetFloat(MoveSpeedId, speed * upgrades.Scale(Rules.UpgradeStat.MoveSpeed) * (moving ? amount : 1f));
        }
    }
}