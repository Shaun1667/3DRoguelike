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

        /// <summary>회복한 순간. 값은 실제로 오른 체력.</summary>
        public event Action<int> Healed;

        /// <summary>true를 돌려주는 동안 피해를 받지 않는다 (구르기 무적 등).</summary>
        public Func<bool> DamageBlocked { get; set; }

        public int Current => pool.Current;
        public int Max => pool.Max;
        public bool IsDead => pool.IsDead;
        /// <summary>마지막으로 피해를 준 공격 이름(결과 화면의 사망 원인). 이름 없이 맞았으면 null.</summary>
        public string LastDamageSource { get; private set; }

        public void Configure(int max, float invulnerableAfterHit)
        {
            pool = new HealthPool(max);
            invulnerableTime = invulnerableAfterHit;
        }

        public void TakeDamage(int amount)
        {
            TakeDamage(amount, null);
        }

        /// <param name="source">공격 이름. 예: "Mutant의 할퀴기"</param>
        public void TakeDamage(int amount, string source)
        {
            if (Time.time < invulnerableEndTime) return;
            if (DamageBlocked != null && DamageBlocked()) return;

            int dealt = pool.TakeDamage(amount);
            if (dealt == 0) return;

            LastDamageSource = source;
            invulnerableEndTime = Time.time + invulnerableTime;
            Damaged?.Invoke(dealt);
            if (pool.IsDead) Died?.Invoke();
        }

        /// <summary>회복. 실제로 오른 만큼 <see cref="Healed"/>를 알린다.</summary>
        public int Heal(int amount)
        {
            int healed = pool.Heal(amount);
            if (healed > 0) Healed?.Invoke(healed);
            return healed;
        }

        public void Restore()
        {
            pool.Restore();
            invulnerableEndTime = 0f;
        }

        /// <summary>씬을 옮길 때 넘겨받은 체력으로 맞춘다(월드 ↔ 던전). 이벤트 없음 — HUD의 Start보다 먼저 부른다.</summary>
        public void SetCurrent(int value)
        {
            pool.SetCurrent(value);
        }
    }
}