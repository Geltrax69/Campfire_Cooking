using System;
using System.Linq;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Core.Events
{
    /// <summary>Proves event-log snapshots preserve identity and append rules across save/load.</summary>
    public sealed class EventLogRestoreTests
    {
        private static readonly LocationId Shop = new LocationId("shop");

        [Test]
        public void PrunedLogRoundTripPreservesHighWaterMarksAndNextId()
        {
            var log = new EventLog();
            var pruned = log.Append(new GameTime(10), Shop, WorldEventType.Arrival);
            var kept = log.Append(new GameTime(20), Shop, WorldEventType.Theft);
            var recent = log.Append(new GameTime(30), Shop, WorldEventType.Departure);
            Assert.That(log.PruneBefore(new GameTime(15), new[] { kept.Id }), Is.EqualTo(1));

            EventLogState snapshot = log.CaptureState();
            Assert.That(snapshot.Events, Is.EqualTo(new[] { kept, recent }));
            Assert.That(snapshot.LastIssuedId, Is.EqualTo(3));
            Assert.That(snapshot.LastIssuedTime, Is.EqualTo(new GameTime(30)));

            // Later appends must not leak into the captured snapshot.
            log.Append(new GameTime(40), Shop, WorldEventType.Purchase);
            Assert.That(snapshot.Events.Count, Is.EqualTo(2));

            var restored = new EventLog();
            restored.RestoreState(snapshot);
            Assert.That(restored.Query(), Is.EqualTo(new[] { kept, recent }));
            Assert.That(restored.Count, Is.EqualTo(2));

            // The next append continues IDs and enforces the restored time rule.
            WorldEvent next = restored.Append(new GameTime(30), Shop, WorldEventType.Arrival);
            Assert.That(next.Id.Value, Is.EqualTo(4));
            Assert.Throws<ArgumentException>(() => restored.Append(new GameTime(29), Shop, WorldEventType.Arrival));
            Assert.That(restored.Query().Count, Is.EqualTo(3));
            Assert.That(pruned.Id.Value, Is.EqualTo(1));
        }

        [Test]
        public void FullyPrunedLogRoundTripKeepsMarksOnEmptyLog()
        {
            var log = new EventLog();
            log.Append(new GameTime(5), Shop, WorldEventType.Arrival);
            log.Append(new GameTime(7), Shop, WorldEventType.Departure);
            Assert.That(log.PruneBefore(new GameTime(8), Array.Empty<WorldEventId>()), Is.EqualTo(2));

            EventLogState snapshot = log.CaptureState();
            Assert.That(snapshot.Events, Is.Empty);
            Assert.That(snapshot.LastIssuedId, Is.EqualTo(2));
            Assert.That(snapshot.LastIssuedTime, Is.EqualTo(new GameTime(7)));

            var restored = new EventLog();
            restored.RestoreState(snapshot);
            Assert.That(restored.Count, Is.Zero);
            WorldEvent next = restored.Append(new GameTime(7), Shop, WorldEventType.Arrival);
            Assert.That(next.Id.Value, Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => restored.Append(new GameTime(6), Shop, WorldEventType.Arrival));
        }

        [Test]
        public void EmptyLogRoundTripBehavesLikeFreshLog()
        {
            var restored = new EventLog();
            restored.RestoreState(new EventLog().CaptureState());
            Assert.That(restored.Count, Is.Zero);
            WorldEvent first = restored.Append(new GameTime(0), Shop, WorldEventType.Arrival);
            Assert.That(first.Id.Value, Is.EqualTo(1));
        }

        [Test]
        public void RestoreReplacesExistingContents()
        {
            var log = new EventLog();
            log.Append(new GameTime(1), Shop, WorldEventType.Arrival);
            log.Append(new GameTime(2), Shop, WorldEventType.Departure);

            var donor = new EventLog();
            var kept = donor.Append(new GameTime(9), Shop, WorldEventType.Theft);

            log.RestoreState(donor.CaptureState());
            Assert.That(log.Query(), Is.EqualTo(new[] { kept }));
            Assert.That(log.Append(new GameTime(9), Shop, WorldEventType.Arrival).Id.Value, Is.EqualTo(2));
        }

        [Test]
        public void InvalidSnapshotsAreRejectedAtomically()
        {
            var log = new EventLog();
            var first = log.Append(new GameTime(5), Shop, WorldEventType.Arrival);
            var second = log.Append(new GameTime(6), Shop, WorldEventType.Departure);

            TestDelegate[] invalidConstructions =
            {
                () => new EventLogState(null, 2, new GameTime(6)),
                () => new EventLogState(new WorldEvent[] { first, null }, 2, new GameTime(6)),
                () => new EventLogState(new[] { second, first }, 2, new GameTime(6)),
                () => new EventLogState(new[] { first, first }, 2, new GameTime(6)),
                () => new EventLogState(new[] { first, second }, -1, new GameTime(6)),
                () => new EventLogState(new[] { first, second }, 1, new GameTime(6)),
                () => new EventLogState(new[] { first, second }, 2, new GameTime(5)),
            };
            foreach (TestDelegate attempt in invalidConstructions)
                Assert.That(attempt, Throws.InstanceOf<ArgumentException>());

            // A snapshot whose times run backwards is corrupt, even with ordered IDs.
            // A live log can never produce this, so the corrupt records are built directly.
            var backwardsFirst = new WorldEvent(new WorldEventId(1), new GameTime(9), Shop,
                WorldEventType.Arrival, null, null, EventVisibility.Normal, null, null, null, null, null);
            var backwardsSecond = new WorldEvent(new WorldEventId(2), new GameTime(8), Shop,
                WorldEventType.Departure, null, null, EventVisibility.Normal, null, null, null, null, null);
            Assert.That(() => new EventLogState(new[] { backwardsFirst, backwardsSecond }, 2, new GameTime(9)),
                Throws.InstanceOf<ArgumentException>());

            Assert.Throws<ArgumentNullException>(() => log.RestoreState(null));

            // Nothing above may have touched the log.
            Assert.That(log.Query(), Is.EqualTo(new[] { first, second }));
            Assert.That(log.Append(new GameTime(6), Shop, WorldEventType.Arrival).Id.Value, Is.EqualTo(3));
        }

        [Test]
        public void SnapshotEventsAreImmutable()
        {
            var log = new EventLog();
            log.Append(new GameTime(1), Shop, WorldEventType.Arrival);
            EventLogState snapshot = log.CaptureState();
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.Generic.IList<WorldEvent>)snapshot.Events).Add(
                    log.Append(new GameTime(2), Shop, WorldEventType.Departure)));
            Assert.That(snapshot.Events.Count, Is.EqualTo(1));
        }
    }
}
