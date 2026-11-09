using System;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>체력을 가진 대상. 계산은 HealthPool, 여기는 무적 시간과 이벤트만 맡는다.</summary>
    public sealed class Health : MonoBehaviour, IDamageable
    {
        private HealthPool pool;
        private float invulnerableTime;
        private float invulnerableEndTime;

        /// <summary>피해가 들어간 순간. 값은 실제로 줄어든 체력.</summary>
        public event Action<int> Damaged;

        public event Action Died;

        /// <summary>true를 돌려주는 동안 피해를 받지 않는다 (구르기 무적 등).</summary>
        public Func<bool> DamageBlocked { get; set; }

        public int Current => pool.Current;
        public int Max => pool.Max;
        public bool IsDead => pool.IsDead;

        public void Configure(int max, float invulnerableAfterHit)
        {
            pool = new HealthPool(max);
            invulnerableTime = invulnerableAfterHit;
        }

        public void TakeDamage(int amount)
        {
            if (Time.time < invulnerableEndTime) return;
            if (DamageBlocked != null && DamageBlocked()) return;

            int dealt = pool.TakeDamage(amount);
            if (dealt == 0) return;

            invulnerableEndTime = Time.time + invulnerableTime;
            Damaged?.Invoke(dealt);
            if (pool.IsDead) Died?.Invoke();
        }

        public void Restore()
        {
            pool.Restore();
            invulnerableEndTime = 0f;
        }
    }
}