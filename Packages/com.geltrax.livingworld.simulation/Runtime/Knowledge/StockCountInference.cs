using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Captures an owner's expected ledger count and observed physical stock count.</summary>
    public sealed class StockCountSnapshot
    {
        public StockCountSnapshot(NpcId owner, LocationId location, ItemTypeId item, int expectedCount,
            int observedCount)
        {
            if (!owner.IsValid) throw new ArgumentException("A stock count needs a valid owner.", nameof(owner));
            if (!location.IsValid) throw new ArgumentException("A stock count needs a valid location.", nameof(location));
            if (!item.IsValid) throw new ArgumentException("A stock count needs a valid item.", nameof(item));
            if (expectedCount < 0) throw new ArgumentOutOfRangeException(nameof(expectedCount));
            if (observedCount < 0) throw new ArgumentOutOfRangeException(nameof(observedCount));

            Owner = owner;
            Location = location;
            Item = item;
            ExpectedCount = expectedCount;
            ObservedCount = observedCount;
        }

        public NpcId Owner { get; }
        public LocationId Location { get; }
        public ItemTypeId Item { get; }
        public int ExpectedCount { get; }
        public int ObservedCount { get; }
    }

    /// <summary>Records a physical count and gives its owner only the deficit that count proves.</summary>
    public static class StockCountInference
    {
        public static WorldEvent Record(WorldState state, StockCountSnapshot snapshot, int confidence)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (confidence < 0 || confidence > 100) throw new ArgumentOutOfRangeException(nameof(confidence));
            if (!state.Knowledge.TryGet(snapshot.Owner, out var store))
                throw new InvalidOperationException("The stock owner needs a knowledge store before counting stock.");

            int missing = snapshot.ExpectedCount - snapshot.ObservedCount;
            BeliefClaim claim = null;
            if (missing > 0)
                claim = new BeliefClaim(BeliefClaimKind.StockMissing, snapshot.Location, snapshot.Item,
                    quantity: missing);

            var existingMissing = store.Query(BeliefClaimKind.StockMissing, snapshot.Location, snapshot.Item);
            foreach (var existing in existingMissing)
                if (state.Clock < existing.LearnedAt)
                    throw new InvalidOperationException("A belief cannot be replaced by an older observation.");

            WorldEvent counted = state.Events.Append(state.Clock, snapshot.Location, WorldEventType.StockCounted,
                ActorId.ForNpc(snapshot.Owner), visibility: EventVisibility.Normal, itemType: snapshot.Item,
                quantity: snapshot.ObservedCount);
            store.RemoveStockMissing(snapshot.Location, snapshot.Item);
            if (claim != null)
            {
                var source = new BeliefSource(BeliefSourceKind.Inferred, originEventId: counted.Id);
                store.Set(new Belief(claim, source, confidence, state.Clock));
            }

            return counted;
        }
    }
}
