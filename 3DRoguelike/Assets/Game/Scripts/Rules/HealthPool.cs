using System;

namespace Game.Rules
{
    /// <summary>체력 계산. 0 아래로 내려가지 않고, 죽은 뒤에는 피해를 받지 않는다.</summary>
    public sealed class HealthPool
    {
        public HealthPool(int max)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            Current = max;
        }

        public int Max { get; }
        public int Current { get; private set; }
        public bool IsDead => Current == 0;

        /// <returns>실제로 줄어든 체력</returns>
        public int TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return 0;
            int dealt = Math.Min(amount, Current);
            Current -= dealt;
            return dealt;
        }

        /// <returns>실제로 오른 체력. 죽은 뒤에는 회복하지 않는다(되살리기는 <see cref="Restore"/>).</returns>
        public int Heal(int amount)
        {
            if (amount <= 0 || IsDead) return 0;
            int healed = Math.Min(amount, Max - Current);
            Current += healed;
            return healed;
        }

        public void Restore()
        {
            Current = Max;
        }

        /// <summary>씬을 옮길 때 넘겨받은 체력으로 맞춘다. 0 ~ 최대로 자른다.</summary>
        public void SetCurrent(int value)
        {
            Current = Math.Max(0, Math.Min(Max, value));
        }
    }
}