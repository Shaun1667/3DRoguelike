using Game.Rules;
using NUnit.Framework;

namespace Game.Tests
{
    public class HealthPoolTests
    {
        [Test]
        public void TakeDamage_ReducesCurrent()
        {
            var pool = new HealthPool(100);

            Assert.That(pool.TakeDamage(15), Is.EqualTo(15));
            Assert.That(pool.Current, Is.EqualTo(85));
            Assert.That(pool.IsDead, Is.False);
        }

        [Test]
        public void TakeDamage_StopsAtZero()
        {
            var pool = new HealthPool(10);

            Assert.That(pool.TakeDamage(15), Is.EqualTo(10));
            Assert.That(pool.Current, Is.EqualTo(0));
            Assert.That(pool.IsDead, Is.True);
        }

        [Test]
        public void TakeDamage_IgnoredWhenDeadOrNotPositive()
        {
            var pool = new HealthPool(10);

            Assert.That(pool.TakeDamage(0), Is.EqualTo(0));
            Assert.That(pool.TakeDamage(-5), Is.EqualTo(0));
            Assert.That(pool.Current, Is.EqualTo(10));

            pool.TakeDamage(10);
            Assert.That(pool.TakeDamage(5), Is.EqualTo(0));
        }

        [Test]
        public void Restore_FillsToMax()
        {
            var pool = new HealthPool(10);
            pool.TakeDamage(10);

            pool.Restore();

            Assert.That(pool.Current, Is.EqualTo(10));
            Assert.That(pool.IsDead, Is.False);
        }

        [Test]
        public void Heal_StopsAtMax_AndNotWhenDead()
        {
            var pool = new HealthPool(10);
            pool.TakeDamage(5);

            Assert.That(pool.Heal(3), Is.EqualTo(3));
            Assert.That(pool.Heal(10), Is.EqualTo(2));
            Assert.That(pool.Current, Is.EqualTo(10));
            Assert.That(pool.Heal(0), Is.EqualTo(0));

            pool.TakeDamage(10);
            Assert.That(pool.Heal(5), Is.EqualTo(0));
            Assert.That(pool.IsDead, Is.True);
        }
    }
}