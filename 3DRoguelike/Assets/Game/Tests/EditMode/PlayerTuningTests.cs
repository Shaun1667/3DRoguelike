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
            Assert.That(tuning.sprintMultiplier, Is.EqualTo(1.6f));
            Assert.That(tuning.gravity, Is.EqualTo(-20f));
            Assert.That(tuning.jumpHeight, Is.EqualTo(1.2f));
            Assert.That(tuning.lookSensitivity, Is.EqualTo(0.08f));
            Assert.That(tuning.minPitch, Is.EqualTo(-30f));
            Assert.That(tuning.maxPitch, Is.EqualTo(60f));
            Assert.That(tuning.animationBlendSpeed, Is.EqualTo(8f));
            Assert.That(tuning.walkPlaybackSpeed, Is.EqualTo(2.4f));
            Assert.That(tuning.mutantFormBeast, Is.EqualTo(10));
            Assert.That(tuning.playerMaxHealth, Is.EqualTo(100));
            Assert.That(tuning.hitInvulnerableTime, Is.EqualTo(0.5f));
            Assert.That(tuning.hitFlashTime, Is.EqualTo(0.08f));
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

        /// <summary>BALANCE.md 9번.</summary>
        [Test]
        public void MutantTuning_MatchesBalance()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<MutantTuning>("Assets/Game/Data/MutantTuning.asset");

            Assert.That(tuning, Is.Not.Null);
            Assert.That(tuning.maxHealth, Is.EqualTo(30));
            Assert.That(tuning.moveSpeed, Is.EqualTo(3f));
            Assert.That(tuning.turnSpeed, Is.EqualTo(540f));
            Assert.That(tuning.repathInterval, Is.EqualTo(0.2f));
            Assert.That(tuning.spawnTime, Is.EqualTo(1f));
            Assert.That(tuning.attackStartRange, Is.EqualTo(1.8f));
            Assert.That(tuning.windupTime, Is.EqualTo(1f));
            Assert.That(tuning.hitRange, Is.EqualTo(2.2f));
            Assert.That(tuning.hitAngle, Is.EqualTo(100f));
            Assert.That(tuning.damage, Is.EqualTo(10));
            Assert.That(tuning.recoverTime, Is.EqualTo(1.3f));
            Assert.That(tuning.windupLeanAngle, Is.EqualTo(25f));
            Assert.That(tuning.strikeLeanAngle, Is.EqualTo(20f));
            Assert.That(tuning.hitFlashTime, Is.EqualTo(0.08f));
            Assert.That(tuning.corpseTime, Is.EqualTo(1f));
        }

        /// <summary>BALANCE.md 7 · 8 · 10번. 7번은 FloorGeneratorTests.BalanceSettings, 10번은 EncounterPlannerTests.BalanceSettings와도 같아야 한다.</summary>
        [Test]
        public void FloorTuning_MatchesBalance()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<FloorTuning>("Assets/Game/Data/FloorTuning.asset");

            Assert.That(tuning, Is.Not.Null);
            Assert.That(tuning.gridWidth, Is.EqualTo(9));
            Assert.That(tuning.gridHeight, Is.EqualTo(9));
            Assert.That(tuning.minRooms, Is.EqualTo(8));
            Assert.That(tuning.maxRooms, Is.EqualTo(12));
            Assert.That(tuning.roomShapes.Length, Is.EqualTo(4));
            int[,] shapes = { { 1, 1, 7 }, { 2, 1, 1 }, { 1, 2, 1 }, { 2, 2, 1 } };
            for (int i = 0; i < 4; i++)
            {
                Assert.That(tuning.roomShapes[i].width, Is.EqualTo(shapes[i, 0]));
                Assert.That(tuning.roomShapes[i].height, Is.EqualTo(shapes[i, 1]));
                Assert.That(tuning.roomShapes[i].weight, Is.EqualTo(shapes[i, 2]));
            }
            Assert.That(tuning.cellSize, Is.EqualTo(24f));
            Assert.That(tuning.wallHeight, Is.EqualTo(4f));
            Assert.That(tuning.wallThickness, Is.EqualTo(1f));
            Assert.That(tuning.doorWidth, Is.EqualTo(4f));
            Assert.That(tuning.enterMargin, Is.EqualTo(1.5f));
            Assert.That(tuning.minEnemies, Is.EqualTo(2));
            Assert.That(tuning.maxEnemies, Is.EqualTo(4));
            Assert.That(tuning.extraEnemiesPerCell, Is.EqualTo(1));
            Assert.That(tuning.spawnWallMargin, Is.EqualTo(4f));
            Assert.That(tuning.spawnSpacing, Is.EqualTo(3f));
            Assert.That(tuning.spawnDoorClearance, Is.EqualTo(6f));
            Assert.That(tuning.rewardChance, Is.EqualTo(0.5f));
            Assert.That(tuning.rewardChoices, Is.EqualTo(3));
        }
    }
}