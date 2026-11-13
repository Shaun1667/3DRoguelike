using Game.Rules;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Beast가 높으면 Mutant 동작(결정 2026-11-11: Beast 10 이상이고 Human보다 높을 때).</summary>
    public class TendencyFormTests
    {
        [TestCase(9, 0, false)]
        [TestCase(10, 0, true)]
        [TestCase(10, 9, true)]
        [TestCase(10, 10, false)]
        [TestCase(12, 14, false)]
        [TestCase(15, 3, true)]
        public void ReachesMutant(int beast, int human, bool expected)
        {
            Assert.That(TendencyForm.ReachesMutant(beast, human, 10), Is.EqualTo(expected));
        }
    }
}
