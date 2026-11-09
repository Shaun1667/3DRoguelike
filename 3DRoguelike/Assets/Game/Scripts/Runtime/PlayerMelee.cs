using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// 맨몸 근접. 좌우 번갈아 할퀴고, 손이 가장 앞으로 뻗는 순간 앞쪽 부채꼴 안의 대상을 한 번씩 때린다.
    /// 공격 동작 동안은 움직일 수 없다.
    /// </summary>
    public sealed class PlayerMelee : MonoBehaviour
    {
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int AttackLeftId = Animator.StringToHash("AttackLeft");

        private const float HitOriginHeight = 1f;
        private const int MaxOverlaps = 32;

        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerGun gun;
        [SerializeField] private PlayerRoll roll;

        private readonly Collider[] overlaps = new Collider[MaxOverlaps];
        private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
        private InputAction attackAction;
        private bool nextIsLeft;
        private bool hitPending;
        private float hitTime;
        private float lockEndTime;

        /// <summary>근접이 대상을 때린 순간.</summary>
        public event Action<IDamageable> Hit;

        public bool IsAttacking => Time.time < lockEndTime;

        private void Awake()
        {
            attackAction = InputSystem.actions.FindAction("Player/Attack", throwIfNotFound: true);
        }

        private void Update()
        {
            if (attackAction.WasPressedThisFrame() && !gun.IsAiming) TryAttack();

            if (hitPending && Time.time >= hitTime)
            {
                hitPending = false;
                ApplyHit();
            }
        }

        public bool TryAttack()
        {
            if (IsAttacking || roll.IsRolling) return false;

            lockEndTime = Time.time + tuning.meleeLockTime;
            hitTime = Time.time + tuning.meleeHitDelay;
            hitPending = true;
            animator.SetBool(AttackLeftId, nextIsLeft);
            animator.SetTrigger(AttackId);
            nextIsLeft = !nextIsLeft;
            return true;
        }

        private void ApplyHit()
        {
            hitThisSwing.Clear();
            Vector3 origin = transform.position + Vector3.up * HitOriginHeight;
            int count = Physics.OverlapSphereNonAlloc(origin, tuning.meleeRange, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (other.transform.IsChildOf(transform)) continue;

                Vector3 toOther = other.bounds.center - transform.position;
                toOther.y = 0f;
                if (Vector3.Angle(transform.forward, toOther) > tuning.meleeAngle * 0.5f) continue;

                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || !hitThisSwing.Add(target)) continue;

                target.TakeDamage(tuning.meleeDamage);
                Hit?.Invoke(target);
            }
        }
    }
}