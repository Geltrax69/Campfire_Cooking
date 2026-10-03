using System;
using System.Collections.Generic;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Classifies a fact without implying that any NPC knows about it.</summary>
    public enum WorldEventType
    {
        Purchase, PartialPurchase, FailedPurchase, Theft, StockCounted, Conversation, Departure, Arrival
    }

    /// <summary>Describes noticeability, not whether anybody actually perceived the event.</summary>
    public enum EventVisibility { Hidden, Quiet, Normal, Loud }

    /// <summary>Immutable world truth, created by its event log with defensively copied actor targets.</summary>
    public sealed class WorldEvent
    {
        internal WorldEvent(WorldEventId id, GameTime time, LocationId location, WorldEventType type,
            ActorId? actor, IEnumerable<ActorId> targets, EventVisibility visibility,
            ItemTypeId? itemType, int? quantity, int? copper)
        {
            if (!id.IsValid) throw new ArgumentException("Invalid event ID.", nameof(id));
            if (!location.IsValid) throw new ArgumentException("Invalid location ID.", nameof(location));
            ValidateType(type);
            if (actor.HasValue && !actor.Value.IsValid) throw new ArgumentException("Invalid actor.", nameof(actor));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            if (itemType.HasValue && !itemType.Value.IsValid) throw new ArgumentException("Invalid item type.", nameof(itemType));
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (copper < 0) throw new ArgumentOutOfRangeException(nameof(copper));
            var copiedTargets = new List<ActorId>();
            if (targets != null)
                foreach (var target in targets)
                {
                    if (!target.IsValid) throw new ArgumentException("Invalid target actor.", nameof(targets));
                    copiedTargets.Add(target);
                }
            Id = id;
            Time = time;
            Location = location;
            Type = type;
            Actor = actor;
            Targets = copiedTargets.AsReadOnly();
            Visibility = visibility;
            ItemType = itemType;
            Quantity = quantity;
            Copper = copper;
        }

        public WorldEventId Id { get; }
        public GameTime Time { get; }
        public LocationId Location { get; }
        public WorldEventType Type { get; }
        public ActorId? Actor { get; }
        public IReadOnlyList<ActorId> Targets { get; }
        public EventVisibility Visibility { get; }
        public ItemTypeId? ItemType { get; }
        public int? Quantity { get; }
        public int? Copper { get; }

        internal static void ValidateType(WorldEventType type)
        {
            if (type < WorldEventType.Purchase || type > WorldEventType.Arrival)
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
