namespace Game.Rules
{
    /// <summary>규칙 코드가 쓰는 난수. 전역 난수(UnityEngine.Random, System.Random) 대신 이것을 주입받는다.</summary>
    public interface IRandom
    {
        uint NextUInt();

        /// <summary>0 이상 maxExclusive 미만. maxExclusive는 1 이상.</summary>
        int Next(int maxExclusive);

        /// <summary>minInclusive 이상 maxExclusive 미만. minInclusive &lt; maxExclusive.</summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>0 이상 1 미만.</summary>
        float NextFloat();
    }
}
