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
            Assert.That(tuning.meleeDamage, Is.EqualTo(10));
            Assert.That(tuning.meleeRange, Is.EqualTo(2.2f));
            Assert.That(tuning.meleeAngle, Is.EqualTo(110f));
            Assert.That(tuning.meleeHitDelay, Is.EqualTo(0.35f));
            Assert.That(tuning.meleeLockTime, Is.EqualTo(0.75f));
            Assert.That(tuning.meleePlaybackSpeed, Is.EqualTo(3f));
            Assert.That(tuning.pistolDamage, Is.EqualTo(6));
            Assert.That(tuning.pistolFireInterval, Is.EqualTo(0.22f));
            Assert.That(tuning.pistolMagazine, Is.EqualTo(12));
            Assert.That(tuning.pistolReloadTime, Is.EqualTo(1.2f));
            Assert.That(tuning.pistolRange, Is.EqualTo(40f));
            Assert.That(tuning.tracerTime, Is.EqualTo(0.05f));
            Assert.That(tuning.rollSpeed, Is.EqualTo(6f));
            Assert.That(tuning.rollDuration, Is.EqualTo(0.8f));
            Assert.That(tuning.rollCooldown, Is.EqualTo(1f));
            Assert.That(tuning.rollInvulnerableRatio, Is.EqualTo(0.5f));
            Assert.That(tuning.aimMoveMultiplier, Is.EqualTo(0.6f));
            Assert.That(tuning.cameraDistance, Is.EqualTo(3f));
            Assert.That(tuning.aimCameraDistance, Is.EqualTo(1.7f));
            Assert.That(tuning.aimBlendSpeed, Is.EqualTo(6f));
            Assert.That(tuning.aimArmLength, Is.EqualTo(0.55f));
        }
    }
}