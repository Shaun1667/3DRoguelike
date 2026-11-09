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

        public void Restore()
        {
            Current = Max;
        }
    }
}