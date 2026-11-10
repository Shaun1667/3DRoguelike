using System;

namespace Game.Rules
{
    /// <summary>
    /// 판 시드 하나에서 용도 · 층별 난수를 만든다. 같은 (시드, 용도, 층) = 같은 수열.
    /// 각 난수는 서로 독립이라, 한 용도에서 몇 번 뽑든 다른 용도의 결과는 바뀌지 않는다.
    /// </summary>
    public sealed class RunRandom
    {
        public RunRandom(RunSeed seed)
        {
            Seed = seed;
        }

        public RunSeed Seed { get; }

        /// <param name="floorIndex">판 안의 층 순서, 0부터. 층과 무관한 용도는 0.</param>
        public Pcg32 Create(RandomPurpose purpose, int floorIndex)
        {
            if (purpose.Key == null) throw new ArgumentException("Use one of the RandomPurpose values.", nameof(purpose));
            if (floorIndex < 0) throw new ArgumentOutOfRangeException(nameof(floorIndex));

            ulong purposeAndFloor = SeedHash.Fnv1a64(purpose.Key) ^ unchecked((ulong)floorIndex * 0x9E3779B97F4A7C15UL);
            ulong initState = SeedHash.Mix64(Seed.Value ^ SeedHash.Mix64(purposeAndFloor));
            ulong initSequence = SeedHash.Mix64(initState ^ 0xD1B54A32D192ED03UL);
            return new Pcg32(initState, initSequence);
        }
    }
}
