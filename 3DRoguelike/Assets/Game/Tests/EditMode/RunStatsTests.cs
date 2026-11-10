using System;
using Game.Rules;
using NUnit.Framework;

namespace Game.Tests
{
    public class RunStatsTests
    {
        static readonly RunSeed Seed = new RunSeed(0x12345678u);

        [Test]
        public void NewRun_IsPlayingWithZeros()
        {
            var stats = new RunStats(Seed, 9);

            Assert.That(stats.Outcome, Is.EqualTo(RunOutcome.Playing));
            Assert.That(stats.Seed, Is.EqualTo(Seed));
            Assert.That(stats.BattleRooms, Is.EqualTo(9));
            Assert.That(stats.RoomsCleared, Is.EqualTo(0));
            Assert.That(stats.Kills, Is.EqualTo(0));
            Assert.That(stats.DeathCause, Is.Null);
        }

        [Test]
        public void Die_RecordsCauseAndTimeOnce()
        {
            var stats = new RunStats(Seed, 9);
            stats.RecordKill();
            stats.RecordRoomCleared();

            Assert.That(stats.Die("Mutant의 할퀴기", 75.5f), Is.True);
            Assert.That(stats.Die("다른 원인", 99f), Is.False);
            Assert.That(stats.ClearFloor(99f), Is.False);

            Assert.That(stats.Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(stats.DeathCause, Is.EqualTo("Mutant의 할퀴기"));
            Assert.That(stats.ElapsedSeconds, Is.EqualTo(75.5f));
            Assert.That(stats.Kills, Is.EqualTo(1));
            Assert.That(stats.RoomsCleared, Is.EqualTo(1));
        }

        [Test]
        public void Die_WithoutCause_SaysUnknown()
        {
            var stats = new RunStats(Seed, 9);

            stats.Die(null, 1f);

            Assert.That(stats.DeathCause, Is.EqualTo("알 수 없음"));
        }

        [Test]
        public void AfterEnd_CountsStop()
        {
            var stats = new RunStats(Seed, 9);
            stats.RecordKill();
            Assert.That(stats.ClearFloor(30f), Is.True);

            stats.RecordKill();
            stats.RecordRoomCleared();

            Assert.That(stats.Outcome, Is.EqualTo(RunOutcome.FloorCleared));
            Assert.That(stats.Kills, Is.EqualTo(1));
            Assert.That(stats.RoomsCleared, Is.EqualTo(0));
            Assert.That(stats.DeathCause, Is.Null);
        }

        [Test]
        public void FormatTime_UsesMinutesAndSeconds()
        {
            Assert.That(RunStats.FormatTime(0f), Is.EqualTo("0:00"));
            Assert.That(RunStats.FormatTime(59.9f), Is.EqualTo("0:59"));
            Assert.That(RunStats.FormatTime(75.5f), Is.EqualTo("1:15"));
            Assert.That(RunStats.FormatTime(3600f + 62f), Is.EqualTo("1:01:02"));
            Assert.That(RunStats.FormatTime(-3f), Is.EqualTo("0:00"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunStats(Seed, -1));
        }
    }
}
