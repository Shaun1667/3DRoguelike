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

        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMelee melee;
        [SerializeField] private PlayerGun gun;

        private CharacterController controller;
        private InputAction moveAction;
        private InputAction lookAction;
        private float pitch;
        private float verticalVelocity;
        private Vector2 blend;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
            lookAction = InputSystem.actions.FindAction("Player/Look", throwIfNotFound: true);
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
            Vector2 input = melee.IsAttacking ? Vector2.zero : Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            if (gun.IsAiming) input *= tuning.aimMoveMultiplier;
            Move(input);
            Animate(input);
        }

        private void Look(Vector2 delta)
        {
            transform.Rotate(0f, delta.x * tuning.lookSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - delta.y * tuning.lookSensitivity, tuning.minPitch, tuning.maxPitch);
            cameraTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Move(Vector2 input)
        {
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + tuning.gravity * Time.deltaTime;
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * tuning.moveSpeed;
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