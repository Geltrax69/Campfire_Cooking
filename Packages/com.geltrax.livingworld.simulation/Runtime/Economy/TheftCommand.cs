using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Moves a requested aggregate item count and records only the resulting theft truth.</summary>
    public sealed class TheftCommand : IWorldCommand
    {
        public TheftCommand(LocationId location, ActorId thief, ActorId sourceOwner,
            Inventory source, Inventory destination, ItemTypeId item, int quantity,
            EventVisibility visibility)
        {
            if (!location.IsValid) throw new ArgumentException("A valid theft location is required.", nameof(location));
            if (!thief.IsValid) throw new ArgumentException("A valid thief actor is required.", nameof(thief));
            if (!sourceOwner.IsValid) throw new ArgumentException("A valid source owner is required.", nameof(sourceOwner));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (ReferenceEquals(source, destination))
                throw new ArgumentException("Source and destination inventories must be distinct.", nameof(destination));
            if (!item.IsValid) throw new ArgumentException("A valid item type is required.", nameof(item));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));

            // Catalogs are immutable, so validating support once is enough for every later execution.
            _ = source.Count(item);
            _ = destination.Count(item);
            Location = location;
            Thief = thief;
            SourceOwner = sourceOwner;
            Source = source;
            Destination = destination;
            Item = item;
            Quantity = quantity;
            Visibility = visibility;
        }

        public LocationId Location { get; }
        public ActorId Thief { get; }
        public ActorId SourceOwner { get; }
        public Inventory Source { get; }
        public Inventory Destination { get; }
        public ItemTypeId Item { get; }
        public int Quantity { get; }
        public EventVisibility Visibility { get; }

        public void Execute(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (Source.Count(Item) < Quantity) return;
            try { Destination.EnsureCanReceive(Item, Quantity); }
            catch (OverflowException) { return; }

            // Append first: the following single-threaded transfer was preflighted and cannot fail.
            state.Events.Append(state.Clock, Location, WorldEventType.Theft, Thief,
                new[] { SourceOwner }, Visibility, Item, Quantity);
            if (!Source.TransferTo(Destination, Item, Quantity))
                throw new InvalidOperationException("Preflighted theft transfer unexpectedly failed.");
        }
    }
}
