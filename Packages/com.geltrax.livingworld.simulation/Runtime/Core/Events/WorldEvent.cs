using System;
using System.Collections.Generic;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Classifies a fact without implying that any NPC knows about it.</summary>
    public enum WorldEventType
    {
        Purchase, PartialPurchase, FailedPurchase, Theft, StockCounted, Conversation, Departure, Arrival,
        RestockOrdered, Produced, Restocked, PriceChanged, ReputationChanged,
        Gift, DebtMissed, RelationshipShift, TurnedStale, Spoiled,
        /// <summary>
        /// An NPC ate a cooked meal. P3-03 addition (flagged for orchestrator
        /// ratification: the Agents folder may not edit Core): itemType is the meal,
        /// quantity carries the meal quality 0-100 (one meal is one event, so no
        /// count is needed), actor is the eater.
        /// </summary>
        MealEaten,
        /// <summary>
        /// A predator killed an animal. P4-02 addition (flagged for orchestrator
        /// ratification: the Agents folder may not edit Core): the wolf pack took a
        /// deer, or took livestock in winter. Actor is null for wild kills (there is
        /// no NPC or player actor to blame); quantity is 1; copper carries the
        /// livestock's economic value when the victim was livestock.
        /// </summary>
        Predation,
        /// <summary>
        /// Young animals were born/hatched in spring. P4-02 addition (flagged for
        /// orchestrator ratification: the Agents folder may not edit Core):
        /// quantity is the litter size; one event per species per breeding.
        /// </summary>
        AnimalBirth,
        /// <summary>
        /// Excess young were removed by the population cap ("eaten or traded").
        /// P4-02 addition (flagged for orchestrator ratification: the Agents folder
        /// may not edit Core): quantity is the number removed.
        /// </summary>
        AnimalCulled,
        /// <summary>
        /// Wintering deer damaged farm crops. P4-02 addition (flagged for
        /// orchestrator ratification: the Agents folder may not edit Core): copper
        /// carries the recorded economic loss; the town-development phase reads it.
        /// </summary>
        CropDamage,
        /// <summary>
        /// A taming bond formed between an actor and an animal. P4-04 addition
        /// (flagged for orchestrator ratification: the Agents folder may not edit
        /// Core): actor is the new owner; location is where the animal was bonded.
        /// The bond itself (AnimalState.Owner) is the primary truth record; this
        /// event lets perception, memory and rumor see it. Emission from
        /// TamingSystem is future work.
        /// </summary>
        Bonded,
        /// <summary>
        /// A taming bond broke (cruelty, neglect or starvation). P4-04 addition
        /// (flagged for orchestrator ratification: the Agents folder may not edit
        /// Core): actor is the one whose handling broke it, when there is one;
        /// location is where the animal was. Emission from TamingSystem is
        /// future work.
        /// </summary>
        BondBroken,
        /// <summary>
        /// An emergent town event fired (P5-04 addition, orchestrator-authorized:
        /// the Agents folder may not edit Core): the town-level condition was met
        /// and the event became active. Location is the market square; quantity
        /// carries the event's ordinal index among the 10 defined events (0-9 in
        /// EmergentEventId declaration order); visibility reflects the event's
        /// noticeability (Loud for shortage/wolf attack/festival/fire, Normal for
        /// the rest). The EmergentEventState is the primary truth record.
        /// </summary>
        EmergentEventFired,
        /// <summary>
        /// An emergent town event ended (P5-04 addition, orchestrator-authorized):
        /// the end condition was met and the event deactivated. Same payload
        /// convention as EmergentEventFired.
        /// </summary>
        EmergentEventEnded,
        /// <summary>
        /// A trade merchant departed along a route (P6-02 addition, flagged for
        /// orchestrator ratification: the Economy folder may not edit Core):
        /// location is the origin village's anchor; quantity is total cargo
        /// units; copper is total bought at the origin. Actor is null (the
        /// merchant is abstract, not an NPC). The MerchantJourney is the primary
        /// truth record.
        /// </summary>
        MerchantDeparted,
        /// <summary>
        /// A trade merchant arrived at a route destination (P6-02 addition,
        /// flagged for orchestrator ratification: the Economy folder may not
        /// edit Core): location is the destination village's anchor; quantity is
        /// total cargo units; copper is total sold at the destination.
        /// </summary>
        MerchantArrived,
        /// <summary>
        /// An NPC died of old age (P7-01 addition, flagged for orchestrator
        /// ratification: the Agents folder may not edit Core): actor is the
        /// deceased NPC; location is the NPC's home. The NpcState.IsDeceased flag
        /// is the primary truth record; this event lets perception, memory and
        /// rumor see it. ValidateType's upper bound was extended to cover it.
        /// </summary>
        Death,
        /// <summary>
        /// A baby was born to village parents (P7-02 addition, flagged for
        /// orchestrator ratification: the Agents folder may not edit Core):
        /// actor is the newborn NPC; targets are the mother and father;
        /// location is the household home. The NpcState parent links are the
        /// primary truth record; this event lets perception, memory and rumor
        /// see it. ValidateType's upper bound was extended to cover it.
        /// </summary>
        Birth,
        /// <summary>
        /// An inheritance transfer from a deceased NPC's estate (P7-03
        /// addition, flagged for orchestrator ratification: the Agents
        /// folder may not edit Core): actor is the deceased NPC; targets
        /// name the heir receiving this transfer (empty when the village
        /// fund receives it); copper carries a money share, quantity the
        /// item units in an item transfer. ValidateType's upper bound was
        /// extended to cover it.
        /// </summary>
        Inheritance
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
            if (type < WorldEventType.Purchase || type > WorldEventType.Inheritance)
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
