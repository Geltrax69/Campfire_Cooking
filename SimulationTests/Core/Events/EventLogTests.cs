using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Core.Events
{
    /// <summary>Protects ordered world truth, immutable snapshots and reference-safe pruning.</summary>
    public sealed class EventLogTests
    {
        private static readonly LocationId Shop = new LocationId("shop");
        private static readonly LocationId Farm = new LocationId("farm");
        private static readonly ActorId Owner = ActorId.ForNpc(new NpcId("owner"));

        [Test]
        public void TypedIdsRejectInvalidValuesAndKeepPlayerDistinctFromNpc()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEventId(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEventId(-1));
            Assert.That(default(WorldEventId).IsValid, Is.False);
            Assert.That(new WorldEventId(long.MaxValue).IsValid, Is.True);
            Assert.That(new WorldEventId(1), Is.EqualTo(new WorldEventId(1)));
            Assert.That(new WorldEventId(1) < new WorldEventId(2), Is.True);
            Assert.That(new HashSet<WorldEventId> { new WorldEventId(1), new WorldEventId(1) }.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => ActorId.ForNpc(default));
            Assert.That(default(ActorId).IsValid, Is.False);
            Assert.That(ActorId.Player.IsPlayer, Is.True);
            Assert.That(ActorId.Player.Npc, Is.Null);
            Assert.That(Owner.Npc, Is.EqualTo(new NpcId("owner")));
            Assert.That(ActorId.ForNpc(new NpcId("player")), Is.Not.EqualTo(ActorId.Player));
            Assert.That(Owner, Is.EqualTo(ActorId.ForNpc(new NpcId("owner"))));
        }

        [Test]
        public void AppendPreservesDetailsEqualMinuteOrderAndImmutableSnapshots()
        {
            var log = new EventLog();
            var targets = new[] { Owner };
            var first = log.Append(new GameTime(5), Shop, WorldEventType.Purchase, ActorId.Player,
                targets, EventVisibility.Quiet, new ItemTypeId("apple"), 6, 12);
            var snapshot = log.Query();
            targets[0] = ActorId.Player;
            var second = log.Append(new GameTime(5), Shop, WorldEventType.Conversation);
            Assert.That(first.Id.Value, Is.EqualTo(1));
            Assert.That(second.Id.Value, Is.EqualTo(2));
            Assert.That(first.Time, Is.EqualTo(new GameTime(5)));
            Assert.That(first.Location, Is.EqualTo(Shop));
            Assert.That(first.Type, Is.EqualTo(WorldEventType.Purchase));
            Assert.That(first.Actor, Is.EqualTo(ActorId.Player));
            Assert.That(first.Targets, Is.EqualTo(new[] { Owner }));
            Assert.That(first.Visibility, Is.EqualTo(EventVisibility.Quiet));
            Assert.That(first.ItemType, Is.EqualTo(new ItemTypeId("apple")));
            Assert.That(first.Quantity, Is.EqualTo(6));
            Assert.That(first.Copper, Is.EqualTo(12));
            Assert.That(second.Actor, Is.Null);
            Assert.That(second.Targets, Is.Empty);
            Assert.That(second.ItemType, Is.Null);
            Assert.That(second.Quantity, Is.Null);
            Assert.That(second.Copper, Is.Null);
            Assert.Throws<NotSupportedException>(() => ((IList<ActorId>)first.Targets)[0] = ActorId.Player);
            Assert.Throws<NotSupportedException>(() => ((IList<WorldEvent>)snapshot).Clear());
            Assert.That(snapshot, Is.EqualTo(new[] { first }));
            Assert.That(log.Query(), Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void QueryCombinesInclusiveBoundsLocationAndTypeInIdOrder()
        {
            var log = new EventLog();
            Assert.That(log.Query(), Is.Empty);
            var a = log.Append(new GameTime(0), Shop, WorldEventType.Purchase);
            var b = log.Append(new GameTime(5), Farm, WorldEventType.Theft);
            var c = log.Append(new GameTime(5), Shop, WorldEventType.Purchase);
            var d = log.Append(new GameTime(10), Shop, WorldEventType.Purchase);
            Assert.That(log.Query(new GameTime(5), new GameTime(10), Shop, WorldEventType.Purchase), Is.EqualTo(new[] { c, d }));
            Assert.That(log.Query(new GameTime(5), new GameTime(5)), Is.EqualTo(new[] { b, c }));
            Assert.That(log.Query(to: new GameTime(0)), Is.EqualTo(new[] { a }));
            Assert.That(log.Query(location: Farm), Is.EqualTo(new[] { b }));
            Assert.That(log.Query(type: WorldEventType.Purchase), Is.EqualTo(new[] { a, c, d }));
            Assert.That(log.Query(from: new GameTime(11)), Is.Empty);
            Assert.Throws<ArgumentException>(() => log.Query(new GameTime(10), new GameTime(5)));
            Assert.Throws<ArgumentException>(() => log.Query(location: default(LocationId)));
            Assert.Throws<ArgumentOutOfRangeException>(() => log.Query(type: (WorldEventType)99));
        }

        [Test]
        public void EconomyEventKindsPreserveAppendOrderAndSuppliedFacts()
        {
            var log = new EventLog();
            var item = new ItemTypeId("apple");
            WorldEventType[] types =
            {
                WorldEventType.RestockOrdered,
                WorldEventType.Produced,
                WorldEventType.Restocked,
                WorldEventType.PriceChanged
            };
            Assert.That(new[]
            {
                WorldEventType.Purchase, WorldEventType.PartialPurchase, WorldEventType.FailedPurchase,
                WorldEventType.Theft, WorldEventType.StockCounted, WorldEventType.Conversation,
                WorldEventType.Departure, WorldEventType.Arrival
            }.Select(type => (int)type), Is.EqualTo(Enumerable.Range(0, 8)));
            Assert.That(types.Select(type => (int)type), Is.EqualTo(Enumerable.Range(8, 4)));

            for (int i = 0; i < types.Length; i++)
                log.Append(new GameTime(20), Farm, types[i], Owner, new[] { ActorId.Player },
                    EventVisibility.Quiet, item, i + 1, 10 + i);

            IReadOnlyList<WorldEvent> events = log.Query();
            Assert.That(events.Select(entry => entry.Type), Is.EqualTo(types));
            Assert.That(events.Select(entry => entry.Id.Value), Is.EqualTo(new long[] { 1, 2, 3, 4 }));
            Assert.That(events.Select(entry => entry.Time), Is.All.EqualTo(new GameTime(20)));
            Assert.That(events.Select(entry => entry.Location), Is.All.EqualTo(Farm));
            Assert.That(events.Select(entry => entry.Actor), Is.All.EqualTo(Owner));
            Assert.That(events.SelectMany(entry => entry.Targets), Is.All.EqualTo(ActorId.Player));
            Assert.That(events.Select(entry => entry.Visibility), Is.All.EqualTo(EventVisibility.Quiet));
            Assert.That(events.Select(entry => entry.ItemType), Is.All.EqualTo(item));
            Assert.That(events.Select(entry => entry.Quantity), Is.EqualTo(new int?[] { 1, 2, 3, 4 }));
            Assert.That(events.Select(entry => entry.Copper), Is.EqualTo(new int?[] { 10, 11, 12, 13 }));
        }

        [Test]
        public void ReputationChangesRoundTripSignedFactsAndFollowExistingNumericValues()
        {
            var log = new EventLog();
            var guards = new ReputationGroupId("group_guards");
            var merchants = new ReputationGroupId("group_merchants");
            Assert.That((int)WorldEventType.ReputationChanged, Is.EqualTo(12));

            WorldEvent raised = log.Append(new GameTime(30), Shop, WorldEventType.ReputationChanged,
                ActorId.Player, reputationGroup: guards, reputationDelta: 5);
            WorldEvent lowered = log.Append(new GameTime(31), Shop, WorldEventType.ReputationChanged,
                ActorId.Player, reputationGroup: merchants, reputationDelta: -15);

            Assert.That(log.Query(), Is.EqualTo(new[] { raised, lowered }));
            Assert.That((raised.ReputationGroup, raised.ReputationDelta),
                Is.EqualTo(((ReputationGroupId?)guards, (int?)5)));
            Assert.That((lowered.ReputationGroup, lowered.ReputationDelta),
                Is.EqualTo(((ReputationGroupId?)merchants, (int?)(-15))));
        }

        [Test]
        public void InvalidOrMisplacedReputationFactsFailAtomically()
        {
            var log = new EventLog();
            var first = log.Append(new GameTime(5), Shop, WorldEventType.Arrival);
            var later = new GameTime(100);
            var guards = new ReputationGroupId("group_guards");
            TestDelegate[] invalid =
            {
                () => log.Append(later, Shop, WorldEventType.ReputationChanged),
                () => log.Append(later, Shop, WorldEventType.ReputationChanged, reputationGroup: guards),
                () => log.Append(later, Shop, WorldEventType.ReputationChanged, reputationDelta: 1),
                () => log.Append(later, Shop, WorldEventType.ReputationChanged,
                    reputationGroup: guards, reputationDelta: 0),
                () => log.Append(later, Shop, WorldEventType.ReputationChanged,
                    reputationGroup: guards, reputationDelta: -101),
                () => log.Append(later, Shop, WorldEventType.ReputationChanged,
                    reputationGroup: guards, reputationDelta: 101),
                () => log.Append(later, Shop, WorldEventType.ReputationChanged,
                    reputationGroup: default(ReputationGroupId), reputationDelta: 1),
                () => log.Append(later, Shop, WorldEventType.Arrival, reputationGroup: guards),
                () => log.Append(later, Shop, WorldEventType.Arrival, reputationDelta: -1),
                () => log.Append(later, Shop, WorldEventType.Arrival,
                    reputationGroup: guards, reputationDelta: -1),
                () => log.Append(later, Shop, (WorldEventType)13,
                    reputationGroup: guards, reputationDelta: 1)
            };

            foreach (TestDelegate attempt in invalid)
            {
                Assert.That(attempt, Throws.InstanceOf<ArgumentException>());
                Assert.That(log.Query(), Is.EqualTo(new[] { first }));
            }

            WorldEvent next = log.Append(new GameTime(5), Shop, WorldEventType.ReputationChanged,
                reputationGroup: guards, reputationDelta: 1);
            Assert.That(next.Id.Value, Is.EqualTo(2));
            Assert.That(log.Count, Is.EqualTo(2));
        }

        [Test]
        public void InvalidAppendsLeaveRecordsNextIdAndTimeUnchanged()
        {
            var log = new EventLog();
            var first = log.Append(new GameTime(5), Shop, WorldEventType.Arrival);
            var later = new GameTime(100);
            TestDelegate[] invalid = {
                () => log.Append(new GameTime(4), Shop, WorldEventType.Arrival),
                () => log.Append(later, default, WorldEventType.Arrival),
                () => log.Append(later, Shop, (WorldEventType)(-1)),
                () => log.Append(later, Shop, WorldEventType.Arrival, default(ActorId)),
                () => log.Append(later, Shop, WorldEventType.Arrival, targets: new[] { Owner, default(ActorId) }),
                () => log.Append(later, Shop, WorldEventType.Arrival, visibility: (EventVisibility)99),
                () => log.Append(later, Shop, WorldEventType.Arrival, itemType: default(ItemTypeId)),
                () => log.Append(later, Shop, WorldEventType.Arrival, quantity: -1),
                () => log.Append(later, Shop, WorldEventType.Arrival, copper: -1)
            };
            foreach (var attempt in invalid)
            {
                Assert.That(attempt, Throws.InstanceOf<ArgumentException>());
                Assert.That(log.Query(), Is.EqualTo(new[] { first }));
            }
            Assert.Throws<ApplicationException>(() => log.Append(later, Shop, WorldEventType.Arrival,
                targets: FailingAfter(Owner)));
            var next = log.Append(new GameTime(5), Shop, WorldEventType.FailedPurchase, quantity: 0, copper: 0);
            Assert.That(next.Id.Value, Is.EqualTo(2));
            Assert.That(log.Count, Is.EqualTo(2));
        }

        [Test]
        public void PruneProtectsReferencesAndCutoffWithoutResettingIdsOrChronology()
        {
            var log = new EventLog();
            var old = log.Append(new GameTime(1), Shop, WorldEventType.Theft);
            var retained = log.Append(new GameTime(2), Shop, WorldEventType.StockCounted);
            var boundary = log.Append(new GameTime(3), Shop, WorldEventType.PartialPurchase);
            var recent = log.Append(new GameTime(4), Shop, WorldEventType.Departure);
            var snapshot = log.Query();
            Assert.That(log.PruneBefore(new GameTime(3), new[] { retained.Id, retained.Id, new WorldEventId(99) }), Is.EqualTo(1));
            Assert.That(log.Query(), Is.EqualTo(new[] { retained, boundary, recent }));
            Assert.That(log.PruneBefore(new GameTime(5), Array.Empty<WorldEventId>()), Is.EqualTo(3));
            Assert.That(log.Count, Is.Zero);
            Assert.That(log.PruneBefore(new GameTime(5), Array.Empty<WorldEventId>()), Is.Zero);
            Assert.That(snapshot, Is.EqualTo(new[] { old, retained, boundary, recent }));
            Assert.Throws<ArgumentException>(() => log.Append(new GameTime(3), Shop, WorldEventType.Arrival));
            Assert.That(log.Append(new GameTime(4), Shop, WorldEventType.Arrival).Id.Value, Is.EqualTo(5));
        }

        [Test]
        public void InvalidOrThrowingRetentionCannotPartiallyPrune()
        {
            var log = new EventLog();
            var first = log.Append(default, Shop, WorldEventType.Arrival);
            var second = log.Append(default, Shop, WorldEventType.Departure);
            Assert.Throws<ArgumentNullException>(() => log.PruneBefore(new GameTime(1), null));
            Assert.Throws<ArgumentException>(() => log.PruneBefore(new GameTime(1), new[] { first.Id, default(WorldEventId) }));
            Assert.Throws<ApplicationException>(() => log.PruneBefore(new GameTime(1), FailingAfter(first.Id)));
            Assert.That(log.Query(), Is.EqualTo(new[] { first, second }));
            Assert.That(log.Append(default, Shop, WorldEventType.Arrival).Id.Value, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InputEnumerationCannotReenterMutations(bool pruning)
        {
            var log = new EventLog();
            var first = log.Append(default, Shop, WorldEventType.Arrival);
            Action mutation = () => log.PruneBefore(new GameTime(1), Array.Empty<WorldEventId>());
            if (pruning)
                Assert.Throws<InvalidOperationException>(() => log.PruneBefore(new GameTime(1), DuringEnumeration(first.Id, mutation)));
            else
                Assert.Throws<InvalidOperationException>(() => log.Append(default, Shop, WorldEventType.Arrival,
                    targets: DuringEnumeration(Owner, mutation)));
            Assert.That(log.Query(), Is.EqualTo(new[] { first }));
            Assert.That(log.Append(default, Shop, WorldEventType.Arrival).Id.Value, Is.EqualTo(2));
        }

        [Test]
        public void WorldsOwnIndependentLogsAndRepeatIdenticalRecords()
        {
            var a = new WorldState(42);
            var b = new WorldState(42);
            Assert.That(a.Events, Is.Not.SameAs(b.Events));
            foreach (var state in new[] { a, b })
                foreach (WorldEventType type in Enum.GetValues(typeof(WorldEventType)))
                    if (type == WorldEventType.ReputationChanged)
                        state.Events.Append(state.Clock, Shop, type, Owner, new[] { ActorId.Player },
                            reputationGroup: new ReputationGroupId("group_guards"), reputationDelta: 1);
                    else
                        state.Events.Append(state.Clock, Shop, type, Owner, new[] { ActorId.Player });
            var left = a.Events.Query();
            var right = b.Events.Query();
            Assert.That(left.Select(e => (e.Id, e.Time, e.Location, e.Type, e.Actor, e.Visibility,
                    e.ItemType, e.Quantity, e.Copper, e.ReputationGroup, e.ReputationDelta)),
                Is.EqualTo(right.Select(e => (e.Id, e.Time, e.Location, e.Type, e.Actor, e.Visibility,
                    e.ItemType, e.Quantity, e.Copper, e.ReputationGroup, e.ReputationDelta))));
            Assert.That(left.SelectMany(e => e.Targets), Is.EqualTo(right.SelectMany(e => e.Targets)));
            a.Events.Append(default, Farm, WorldEventType.Arrival);
            Assert.That(a.Events.Count, Is.EqualTo(b.Events.Count + 1));
        }

        private static IEnumerable<T> FailingAfter<T>(T value) =>
            DuringEnumeration(value, () => throw new ApplicationException("Input failed."));

        private static IEnumerable<T> DuringEnumeration<T>(T value, Action action)
        {
            yield return value;
            action();
        }
    }
}
