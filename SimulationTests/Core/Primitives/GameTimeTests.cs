using System;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Core.Primitives
{
    /// <summary>Verifies minute-based time, calendar boundaries and checked advancement.</summary>
    public class GameTimeTests
    {
        [TestCase(0L, 1L, 0, 0)]
        [TestCase(59L, 1L, 0, 59)]
        [TestCase(60L, 1L, 1, 0)]
        [TestCase(1439L, 1L, 23, 59)]
        [TestCase(1440L, 2L, 0, 0)]
        [TestCase(4385L, 4L, 1, 5)]
        [TestCase(long.MaxValue, 6405119470038039L, 18, 7)]
        public void DerivesDayHourAndMinute(long minutes, long day, int hour, int minute)
        {
            var time = new GameTime(minutes);
            Assert.That(time.TotalMinutes, Is.EqualTo(minutes));
            Assert.That(time.Day, Is.EqualTo(day));
            Assert.That(time.Hour, Is.EqualTo(hour));
            Assert.That(time.Minute, Is.EqualTo(minute));
        }

        [Test]
        public void AdvanceReturnsNewTimeAndPreservesOriginal()
        {
            var time = new GameTime(1439);
            Assert.That(time.Advance(1), Is.EqualTo(new GameTime(1440)));
            Assert.That(time.Advance(2882), Is.EqualTo(new GameTime(4321)));
            Assert.That(time.Advance(0), Is.EqualTo(time));
            Assert.That(time.TotalMinutes, Is.EqualTo(1439));
            Assert.That(new GameTime(long.MaxValue - 1).Advance(1).TotalMinutes, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void EqualityOrderingAndDefaultUseTotalMinutes()
        {
            var earlier = new GameTime(59);
            var later = new GameTime(60);
            Assert.That(default(GameTime), Is.EqualTo(new GameTime(0)));
            Assert.That(earlier.Equals((object)new GameTime(59)), Is.True);
            Assert.That(earlier.Equals(null), Is.False);
            Assert.That(earlier.GetHashCode(), Is.EqualTo(new GameTime(59).GetHashCode()));
            Assert.That(earlier == new GameTime(59) && earlier != later, Is.True);
            Assert.That(earlier < later && later > earlier && earlier <= new GameTime(59) && later >= new GameTime(60), Is.True);
            Assert.That(earlier.CompareTo(later), Is.LessThan(0));
            Assert.That(later.CompareTo(earlier), Is.GreaterThan(0));
            Assert.That(earlier.CompareTo(new GameTime(59)), Is.Zero);
        }

        [Test]
        public void RejectsNegativeTimeAndOverflow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameTime(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameTime(10).Advance(-1));
            Assert.Throws<OverflowException>(() => new GameTime(long.MaxValue).Advance(1));
        }
    }
}
