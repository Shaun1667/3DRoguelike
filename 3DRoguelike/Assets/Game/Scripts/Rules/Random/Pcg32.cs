using System;

namespace Game.Rules
{
    /// <summary>
    /// PCG32 (pcg-random.org의 pcg32 기본형). 어느 런타임에서도 같은 수열을 내고, <see cref="State"/>로 저장 · 복원할 수 있다.
    /// </summary>
    public sealed class Pcg32 : IRandom
    {
        const ulong Multiplier = 6364136223846793005UL;

        ulong state;
        readonly ulong increment;

        /// <summary>참조 구현의 pcg32_srandom(initState, initSequence)와 같다.</summary>
        public Pcg32(ulong initState, ulong initSequence)
        {
            state = 0;
            increment = (initSequence << 1) | 1UL;
            NextUInt();
            state = unchecked(state + initState);
            NextUInt();
        }

        Pcg32(Pcg32State saved)
        {
            state = saved.State;
            increment = saved.Increment;
        }

        /// <summary>지금 상태. 저장했다가 <see cref="FromState"/>로 되살리면 같은 수열이 이어진다.</summary>
        public Pcg32State State => new Pcg32State(state, increment);

        public static Pcg32 FromState(Pcg32State saved)
        {
            if ((saved.Increment & 1UL) == 0) throw new ArgumentException("Increment must be odd.", nameof(saved));
            return new Pcg32(saved);
        }

        public uint NextUInt()
        {
            ulong old = state;
            state = unchecked(old * Multiplier + increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)Bounded((uint)maxExclusive);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            uint span = (uint)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)Bounded(span));
        }

        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>0 이상 bound 미만, 치우침 없이 (참조 구현의 pcg32_boundedrand).</summary>
        uint Bounded(uint bound)
        {
            uint threshold = unchecked(0u - bound) % bound;
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold) return value % bound;
            }
        }
    }

    /// <summary><see cref="Pcg32"/>의 저장용 상태.</summary>
    public readonly struct Pcg32State
    {
        public Pcg32State(ulong state, ulong increment)
        {
            State = state;
            Increment = increment;
        }

        public ulong State { get; }
        public ulong Increment { get; }
    }
}
