using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Owns one NPC's beliefs, keyed and ordered by structured claim.</summary>
    public sealed class BeliefStore
    {
        private readonly SortedDictionary<BeliefClaim, Belief> _beliefs = new SortedDictionary<BeliefClaim, Belief>();

        public BeliefStore(NpcId owner)
        {
            if (!owner.IsValid) throw new ArgumentException("A belief store needs a valid owner.", nameof(owner));
            Owner = owner;
        }

        public NpcId Owner { get; }
        public int Count => _beliefs.Count;

        internal void RemoveStockMissing(LocationId location, ItemTypeId itemType)
        {
            var removals = new List<BeliefClaim>();
            foreach (var belief in _beliefs.Values)
                if (belief.Claim.Kind == BeliefClaimKind.StockMissing && belief.Claim.Location == location &&
                    belief.Claim.ItemType == itemType)
                    removals.Add(belief.Claim);
            foreach (var claim in removals)
                _beliefs.Remove(claim);
        }

        public void Set(Belief belief)
        {
            if (belief == null) throw new ArgumentNullException(nameof(belief));
            if (_beliefs.TryGetValue(belief.Claim, out var existing) && belief.LearnedAt < existing.LearnedAt)
                throw new InvalidOperationException("A belief cannot be replaced by an older observation.");
            _beliefs[belief.Claim] = belief;
        }

        public IReadOnlyList<Belief> Query(BeliefClaimKind? kind = null, LocationId? location = null,
            ItemTypeId? itemType = null, ActorId? subject = null, int? quantity = null)
        {
            if (kind.HasValue && (kind < BeliefClaimKind.StockAvailable || kind > BeliefClaimKind.Presence))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (location.HasValue && !location.Value.IsValid) throw new ArgumentException("Invalid location.", nameof(location));
            if (itemType.HasValue && !itemType.Value.IsValid) throw new ArgumentException("Invalid item type.", nameof(itemType));
            if (subject.HasValue && !subject.Value.IsValid) throw new ArgumentException("Invalid subject.", nameof(subject));
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));

            var matches = new List<Belief>();
            foreach (var belief in _beliefs.Values)
                if ((!kind.HasValue || belief.Claim.Kind == kind.Value) &&
                    (!location.HasValue || belief.Claim.Location == location.Value) &&
                    (!itemType.HasValue || belief.Claim.ItemType == itemType) &&
                    (!subject.HasValue || belief.Claim.Subject == subject) &&
                    (!quantity.HasValue || belief.Claim.Quantity == quantity))
                    matches.Add(belief);
            return matches.AsReadOnly();
        }
    }
}
