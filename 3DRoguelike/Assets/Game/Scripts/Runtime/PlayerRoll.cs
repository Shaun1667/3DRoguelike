using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// 구르기(Shift). 입력 방향(없으면 정면)으로 구르고, 앞쪽 일부 시간만 무적이다.
    /// 시점은 계속 돌릴 수 있어서 몸(Visual)만 구르는 방향을 바라보게 한다.
    /// </summary>
    public sealed class PlayerRoll : MonoBehaviour
    {
        private static readonly int RollId = Animator.StringToHash("Roll");

        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private PlayerMelee melee;
        [SerializeField] private Animator animator;

        private InputAction rollAction;
        private InputAction moveAction;
        private Transform visual;
        private Vector3 direction;
        private float endTime;
        private float invulnerableEndTime;
        private float nextRollTime;
        private bool rolling;

        /// <summary>구르기를 시작한 순간.</summary>
        public event Action Rolled;

        public bool IsRolling => rolling;
        public bool IsInvulnerable => Time.time < invulnerableEndTime;
        public Vector3 Velocity => rolling ? direction * tuning.rollSpeed : Vector3.zero;

        private void Awake()
        {
            rollAction = InputSystem.actions.FindAction("Player/Roll", throwIfNotFound: true);
            moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
            visual = animator.transform;
        }

        private void Update()
        {
            if (rollAction.WasPressedThisFrame()) TryRoll(moveAction.ReadValue<Vector2>());

            if (!rolling) return;
            if (Time.time >= endTime)
            {
                rolling = false;
                visual.localRotation = Quaternion.identity;
            }
        }

        private void LateUpdate()
        {
            if (rolling) visual.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        /// <param name="input">이동 입력 (x 오른쪽, y 앞). 없으면 정면으로 구른다.</param>
        public bool TryRoll(Vector2 input)
        {
            if (rolling || melee.IsAttacking || Time.time < nextRollTime) return false;

            Vector3 wanted = transform.right * input.x + transform.forward * input.y;
            direction = wanted.sqrMagnitude > 0.01f ? wanted.normalized : transform.forward;
            rolling = true;
            endTime = Time.time + tuning.rollDuration;
            invulnerableEndTime = Time.time + tuning.rollDuration * tuning.rollInvulnerableRatio;
            nextRollTime = Time.time + tuning.rollCooldown;
            animator.SetTrigger(RollId);
            Rolled?.Invoke();
            return true;
        }
    }
}