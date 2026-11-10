namespace Game.Rules
{
    /// <summary>시드 계산용 해시. 결과가 저장된 시드의 의미를 정하므로 바꾸지 않는다.</summary>
    static class SeedHash
    {
        /// <summary>SplitMix64 마무리 단계. 비슷한 입력도 전혀 다른 값이 된다.</summary>
        public static ulong Mix64(ulong value)
        {
            unchecked
            {
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        /// <summary>FNV-1a 64비트. 문자 코드를 그대로 쓰므로 인코딩 설정과 무관하다.</summary>
        public static ulong Fnv1a64(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in text)
            {
                hash ^= c;
                hash = unchecked(hash * 1099511628211UL);
            }
            return hash;
        }
    }
}
