using System;
using System.Collections.Generic;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Classifies a fact without implying that any NPC knows about it.</summary>
    public enum WorldEventType
    {
        Purchase, PartialPurchase, FailedPurchase, Theft, StockCounted, Conversation, Departure, Arrival,
        RestockOrdered, Produced, Restocked, PriceChanged, ReputationChanged
    }

    /// <summary>Describes noticeability, not whether anybody actually perceived the event.</summary>
    public enum EventVisibility { Hidden, Quiet, Normal, Loud }

    /// <summary>Immutable world truth, created by its event log with defensively copied actor targets.</summary>
    public sealed class WorldEvent
    {
        private const int MinimumReputationDelta = -100;
        private const int MaximumReputationDelta = 100;

        internal WorldEvent(WorldEventId id, GameTime time, LocationId location, WorldEventType type,
            ActorId? actor, IEnumerable<ActorId> targets, EventVisibility visibility,
            ItemTypeId? itemType, int? quantity, int? copper,
            ReputationGroupId? reputationGroup, int? reputationDelta)
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
            ValidateReputationFacts(type, reputationGroup, reputationDelta);
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
            ReputationGroup = reputationGroup;
            ReputationDelta = reputationDelta;
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
        public ReputationGroupId? ReputationGroup { get; }
        public int? ReputationDelta { get; }

        internal static void ValidateType(WorldEventType type)
        {
            if (type < WorldEventType.Purchase || type > WorldEventType.ReputationChanged)
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        private static void ValidateReputationFacts(WorldEventType type,
            ReputationGroupId? reputationGroup, int? reputationDelta)
        {
            if (type != WorldEventType.ReputationChanged)
            {
                if (reputationGroup.HasValue || reputationDelta.HasValue)
                    throw new ArgumentException("Reputation facts require a reputation-change event.");
                return;
            }

            if (!reputationGroup.HasValue)
                throw new ArgumentException("A reputation-change event requires a group.", nameof(reputationGroup));
            if (!reputationGroup.Value.IsValid)
                throw new ArgumentException("Invalid reputation group.", nameof(reputationGroup));
            if (!reputationDelta.HasValue)
                throw new ArgumentException("A reputation-change event requires a delta.", nameof(reputationDelta));
            if (reputationDelta.Value < MinimumReputationDelta || reputationDelta.Value > MaximumReputationDelta
                || reputationDelta.Value == 0)
                throw new ArgumentOutOfRangeException(nameof(reputationDelta),
                    "An applied reputation delta must be nonzero and between -100 and 100.");
        }
    }
}
