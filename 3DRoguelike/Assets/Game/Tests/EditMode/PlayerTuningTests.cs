using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    /// <summary>BALANCE.md 1번과 데이터 에셋 값이 같은지 검사한다.</summary>
    public class PlayerTuningTests
    {
        [Test]
        public void PlayerTuning_MatchesBalance()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuning>("Assets/Game/Data/PlayerTuning.asset");

            Assert.That(tuning, Is.Not.Null);
            Assert.That(tuning.moveSpeed, Is.EqualTo(4f));
            Assert.That(tuning.gravity, Is.EqualTo(-20f));
            Assert.That(tuning.lookSensitivity, Is.EqualTo(0.08f));
            Assert.That(tuning.minPitch, Is.EqualTo(-30f));
            Assert.That(tuning.maxPitch, Is.EqualTo(60f));
            Assert.That(tuning.animationBlendSpeed, Is.EqualTo(8f));
            Assert.That(tuning.walkPlaybackSpeed, Is.EqualTo(2.4f));
        }
    }
}