using System;

namespace Game.Rules
{
    /// <summary>
    /// 판 시드(32비트). 화면에는 "K7Q2-9XMP" 같은 8글자 코드로 보인다.
    /// 코드 = 시드 32비트 + 검사 8비트를 5비트씩 8글자로. 0 · O · 1 · I는 쓰지 않고, 오타는 대부분(255/256) 검사에서 걸린다.
    /// </summary>
    public readonly struct RunSeed : IEquatable<RunSeed>
    {
        const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        const int CodeLength = 8;

        public RunSeed(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        /// <summary>"XXXX-XXXX" 형식.</summary>
        public string ToCode()
        {
            ulong packed = ((ulong)Value << 8) | Check(Value);
            var chars = new char[CodeLength + 1];
            int write = chars.Length - 1;
            for (int i = 0; i < CodeLength; i++)
            {
                if (i == 4) chars[write--] = '-';
                chars[write--] = Alphabet[(int)(packed & 31UL)];
                packed >>= 5;
            }
            return new string(chars);
        }

        /// <summary>대소문자 · 하이픈 · 공백은 상관없다. 글자가 틀리거나 검사에 걸리면 false.</summary>
        public static bool TryParse(string code, out RunSeed seed)
        {
            seed = default;
            if (code == null) return false;

            ulong packed = 0;
            int count = 0;
            foreach (char raw in code)
            {
                if (raw == '-' || char.IsWhiteSpace(raw)) continue;
                int index = Alphabet.IndexOf(char.ToUpperInvariant(raw));
                if (index < 0 || ++count > CodeLength) return false;
                packed = (packed << 5) | (uint)index;
            }
            if (count != CodeLength) return false;

            uint value = (uint)(packed >> 8);
            if ((byte)packed != Check(value)) return false;
            seed = new RunSeed(value);
            return true;
        }

        static byte Check(uint value)
        {
            return (byte)(SeedHash.Mix64(value ^ 0x5EED5EED5EED5EEDUL) >> 56);
        }

        public bool Equals(RunSeed other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RunSeed other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => ToCode();
    }
}
