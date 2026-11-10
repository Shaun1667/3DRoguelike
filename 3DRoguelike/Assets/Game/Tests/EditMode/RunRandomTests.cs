using System.Collections.Generic;
using Game.Rules;
using NUnit.Framework;

namespace Game.Tests
{
    public class RunRandomTests
    {
        static readonly RunSeed FixedSeed = new RunSeed(0x12345678u);

        [Test]
        public void Pcg32_MatchesReferenceSequence()
        {
            // pcg32-demo(seed 42, sequence 54)의 첫 출력
            var random = new Pcg32(42, 54);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };

            foreach (uint value in expected) Assert.That(random.NextUInt(), Is.EqualTo(value));
        }

        [Test]
        public void FixedSeed_GivesFixedValues()
        {
            // 이 값이 바뀌면 모든 시드의 맵 · 보상이 바뀐다. 일부러 바꾼 게 아니면 코드를 되돌린다.
            var run = new RunRandom(FixedSeed);

            var map = run.Create(RandomPurpose.Map, 0);
            Assert.That(map.NextUInt(), Is.EqualTo(3987860242u));
            Assert.That(map.NextUInt(), Is.EqualTo(1754944442u));
            Assert.That(map.NextUInt(), Is.EqualTo(2565252973u));
            Assert.That(map.NextUInt(), Is.EqualTo(1547936304u));
            Assert.That(run.Create(RandomPurpose.Map, 1).NextUInt(), Is.EqualTo(1508090910u));
            Assert.That(run.Create(RandomPurpose.Reward, 0).NextUInt(), Is.EqualTo(1968474962u));
        }

        [Test]
        public void SameSeedPurposeAndFloor_GiveSameSequence()
        {
            var a = new RunRandom(FixedSeed).Create(RandomPurpose.Map, 2);
            var b = new RunRandom(new RunSeed(0x12345678u)).Create(RandomPurpose.Map, 2);

            for (int i = 0; i < 100; i++) Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }

        [Test]
        public void DifferentSeedPurposeOrFloor_GiveDifferentSequence()
        {
            var run = new RunRandom(FixedSeed);
            var firsts = new HashSet<uint>
            {
                run.Create(RandomPurpose.Map, 0).NextUInt(),
                run.Create(RandomPurpose.Map, 1).NextUInt(),
                run.Create(RandomPurpose.Decor, 0).NextUInt(),
                run.Create(RandomPurpose.Reward, 0).NextUInt(),
                run.Create(RandomPurpose.Enemy, 0).NextUInt(),
                run.Create(RandomPurpose.Combat, 0).NextUInt(),
                run.Create(RandomPurpose.Effects, 0).NextUInt(),
                new RunRandom(new RunSeed(0x12345679u)).Create(RandomPurpose.Map, 0).NextUInt(),
            };

            Assert.That(firsts.Count, Is.EqualTo(8));
        }

        [Test]
        public void DrawingOtherPurposes_DoesNotChangeMap()
        {
            var run = new RunRandom(FixedSeed);
            var expected = new List<uint>();
            var before = run.Create(RandomPurpose.Map, 0);
            for (int i = 0; i < 20; i++) expected.Add(before.NextUInt());

            var reward = run.Create(RandomPurpose.Reward, 0);
            var combat = run.Create(RandomPurpose.Combat, 0);
            for (int i = 0; i < 1000; i++)
            {
                reward.Next(10);
                combat.NextFloat();
            }

            var after = run.Create(RandomPurpose.Map, 0);
            foreach (uint value in expected) Assert.That(after.NextUInt(), Is.EqualTo(value));
        }

        [Test]
        public void SavedState_ContinuesSameSequence()
        {
            var random = new RunRandom(FixedSeed).Create(RandomPurpose.Reward, 0);
            random.NextUInt();
            Pcg32State saved = random.State;
            var expected = new List<uint>();
            for (int i = 0; i < 20; i++) expected.Add(random.NextUInt());

            var restored = Pcg32.FromState(saved);

            foreach (uint value in expected) Assert.That(restored.NextUInt(), Is.EqualTo(value));
        }

        [Test]
        public void Range_StaysInBoundsAndCoversAllValues()
        {
            var random = new Pcg32(1, 1);
            var seen = new HashSet<int>();

            for (int i = 0; i < 1000; i++)
            {
                int value = random.Range(-3, 4);
                Assert.That(value, Is.InRange(-3, 3));
                seen.Add(value);

                float f = random.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }

            Assert.That(seen.Count, Is.EqualTo(7));
            Assert.That(random.Next(1), Is.EqualTo(0));
            Assert.That(random.Range(int.MinValue, int.MaxValue), Is.LessThan(int.MaxValue));
        }

        [Test]
        public void Range_RejectsEmptyRange()
        {
            var random = new Pcg32(1, 1);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => random.Next(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => random.Range(5, 5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new RunRandom(FixedSeed).Create(RandomPurpose.Map, -1));
        }

        [Test]
        public void Next_IsRoughlyUniform()
        {
            var random = new RunRandom(FixedSeed).Create(RandomPurpose.Reward, 0);
            var counts = new int[6];

            for (int i = 0; i < 60000; i++) counts[random.Next(6)]++;

            foreach (int count in counts) Assert.That(count, Is.InRange(9500, 10500));
        }

        [Test]
        public void SeedCode_HasFixedFormat()
        {
            Assert.That(new RunSeed(0u).ToCode(), Is.EqualTo("2222-226H"));
            Assert.That(new RunSeed(1u).ToCode(), Is.EqualTo("2222-22G3"));
            Assert.That(FixedSeed.ToCode(), Is.EqualTo("4AU7-EY2Q"));
            Assert.That(new RunSeed(uint.MaxValue).ToCode(), Is.EqualTo("ZZZZ-ZZVS"));
        }

        [Test]
        public void SeedCode_RoundTrips()
        {
            foreach (uint value in new[] { 0u, 1u, 0x12345678u, 0x9E3779B9u, uint.MaxValue })
            {
                var seed = new RunSeed(value);

                Assert.That(RunSeed.TryParse(seed.ToCode(), out RunSeed parsed), Is.True);
                Assert.That(parsed, Is.EqualTo(seed));
            }
        }

        [Test]
        public void SeedCode_IgnoresCaseHyphenAndSpaces()
        {
            Assert.That(RunSeed.TryParse("4au7ey2q", out RunSeed a), Is.True);
            Assert.That(RunSeed.TryParse(" 4AU7 - EY2Q ", out RunSeed b), Is.True);
            Assert.That(a, Is.EqualTo(FixedSeed));
            Assert.That(b, Is.EqualTo(FixedSeed));
        }

        [Test]
        public void SeedCode_RejectsTyposAndBadText()
        {
            foreach (string code in new[] { null, "", "4AU7-EY2", "4AU7-EY2QQ", "4AU7-EY2O", "4AU7-EY20", "4AU7-EY2I", "4AU7-EY21", "4AU7-EY2R" })
            {
                Assert.That(RunSeed.TryParse(code, out _), Is.False, code ?? "null");
            }
        }
    }
}
