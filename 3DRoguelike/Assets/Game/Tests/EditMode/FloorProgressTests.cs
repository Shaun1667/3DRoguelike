using System;
using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using NUnit.Framework;

namespace Game.Tests
{
    public class FloorProgressTests
    {
        // FloorGeneratorTests.FixedSeed_GivesFixedLayout의 배치. 문: 0-1 1-2 2-3 3-4 3-5 4-6 6-7 1-8 1-9
        static FloorProgress NewProgress()
        {
            return new FloorProgress(FloorGeneratorTests.Generate(0x12345678u, 0));
        }

        static List<string> LockedDoors(FloorProgress progress)
        {
            return progress.Layout.Doors.Where(progress.IsDoorLocked).Select(door => $"{door.RoomA}-{door.RoomB}").ToList();
        }

        [Test]
        public void Start_IsClearedCurrentAndUnlocked()
        {
            var progress = NewProgress();

            Assert.That(progress.CurrentRoom, Is.EqualTo(0));
            Assert.That(progress.IsCleared(0), Is.True);
            Assert.That(progress.IsCleared(1), Is.False);
            Assert.That(progress.LockedRoom, Is.EqualTo(-1));
            Assert.That(LockedDoors(progress), Is.Empty);
        }

        [Test]
        public void EnteringUnclearedRoom_LocksOnlyItsDoors()
        {
            var progress = NewProgress();
            int lockedEvent = -1;
            progress.RoomLocked += id => lockedEvent = id;

            Assert.That(progress.Enter(1), Is.True);

            Assert.That(lockedEvent, Is.EqualTo(1));
            Assert.That(progress.CurrentRoom, Is.EqualTo(1));
            Assert.That(progress.LockedRoom, Is.EqualTo(1));
            Assert.That(LockedDoors(progress), Is.EquivalentTo(new[] { "0-1", "1-2", "1-8", "1-9" }));
        }

        [Test]
        public void WhileLocked_OtherRoomsAreIgnored()
        {
            var progress = NewProgress();
            progress.Enter(1);

            Assert.That(progress.Enter(2), Is.False);
            Assert.That(progress.Enter(0), Is.False);
            Assert.That(progress.CurrentRoom, Is.EqualTo(1));
            Assert.That(progress.LockedRoom, Is.EqualTo(1));
        }

        [Test]
        public void Clearing_UnlocksAndIsRemembered()
        {
            var progress = NewProgress();
            int clearedEvent = -1;
            progress.RoomCleared += id => clearedEvent = id;
            progress.Enter(1);

            Assert.That(progress.ClearLockedRoom(), Is.True);

            Assert.That(clearedEvent, Is.EqualTo(1));
            Assert.That(progress.IsCleared(1), Is.True);
            Assert.That(progress.LockedRoom, Is.EqualTo(-1));
            Assert.That(LockedDoors(progress), Is.Empty);

            Assert.That(progress.Enter(0), Is.False, "깬 방은 다시 잠기지 않는다");
            Assert.That(progress.Enter(1), Is.False);
            Assert.That(progress.CurrentRoom, Is.EqualTo(1));
            Assert.That(progress.Enter(2), Is.True, "다음 방은 잠긴다");
        }

        [Test]
        public void ClearWithoutLockedRoom_DoesNothing()
        {
            var progress = NewProgress();
            bool fired = false;
            progress.RoomCleared += _ => fired = true;

            Assert.That(progress.ClearLockedRoom(), Is.False);
            Assert.That(fired, Is.False);
        }

        [Test]
        public void Visibility_StartVisitedNeighboursKnownRestHidden()
        {
            var progress = NewProgress();

            Assert.That(progress.Visibility(0), Is.EqualTo(RoomVisibility.Visited));
            Assert.That(progress.Visibility(1), Is.EqualTo(RoomVisibility.Known), "시작 방과 문으로 이어짐");
            Assert.That(progress.Visibility(2), Is.EqualTo(RoomVisibility.Hidden));
            Assert.That(progress.IsVisited(1), Is.False);
        }

        [Test]
        public void Entering_RevealsRoomAndItsNeighbours()
        {
            var progress = NewProgress();
            int enteredEvent = -1;
            progress.RoomEntered += id => enteredEvent = id;

            progress.Enter(1);

            Assert.That(enteredEvent, Is.EqualTo(1));
            Assert.That(progress.Visibility(1), Is.EqualTo(RoomVisibility.Visited));
            foreach (int neighbour in new[] { 2, 8, 9 }) Assert.That(progress.Visibility(neighbour), Is.EqualTo(RoomVisibility.Known));
            Assert.That(progress.Visibility(3), Is.EqualTo(RoomVisibility.Hidden));
        }

        [Test]
        public void EnteringCurrentRoom_DoesNothing()
        {
            var progress = NewProgress();
            bool fired = false;
            progress.RoomEntered += _ => fired = true;

            Assert.That(progress.Enter(0), Is.False);
            Assert.That(fired, Is.False);
        }

        [Test]
        public void UnknownRoom_Throws()
        {
            var progress = NewProgress();

            Assert.Throws<ArgumentOutOfRangeException>(() => progress.Enter(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => progress.Enter(progress.Layout.Rooms.Count));
            Assert.Throws<ArgumentOutOfRangeException>(() => progress.IsCleared(99));
            Assert.Throws<ArgumentNullException>(() => new FloorProgress(null));
        }
    }
}
