using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Classifies the small set of structured claims supported by the Apple Test.</summary>
    public enum BeliefClaimKind
    {
        StockAvailable,
        StockMissing,
        TheftObserved,
        Presence,
        /// <summary>Attributed interaction memory: the subject wronged the holder (witnessed theft, missed debt).</summary>
        WrongedBy,
        /// <summary>Attributed interaction memory: the subject gave the holder a gift.</summary>
        GiftFrom,
        /// <summary>Attributed interaction memory: the subject traded fairly with the holder.</summary>
        FairTradeWith
    }

    /// <summary>Immutable statement an NPC may believe, independent of whether it is world truth.</summary>
    public sealed class BeliefClaim : IEquatable<BeliefClaim>, IComparable<BeliefClaim>
    {
        public BeliefClaim(BeliefClaimKind kind, LocationId location, ItemTypeId? itemType = null,
            ActorId? subject = null, int? quantity = null)
        {
            if (kind < BeliefClaimKind.StockAvailable || kind > BeliefClaimKind.FairTradeWith)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!location.IsValid) throw new ArgumentException("A claim needs a valid location.", nameof(location));
            if (itemType.HasValue && !itemType.Value.IsValid) throw new ArgumentException("Invalid item type.", nameof(itemType));
            if (subject.HasValue && !subject.Value.IsValid) throw new ArgumentException("Invalid subject.", nameof(subject));
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));

            bool isStock = kind == BeliefClaimKind.StockAvailable || kind == BeliefClaimKind.StockMissing;
            if (isStock && (!itemType.HasValue || !quantity.HasValue))
                throw new ArgumentException("Stock claims require an item and quantity.");
            if (isStock && subject.HasValue)
                throw new ArgumentException("Stock claims cannot identify an actor.", nameof(subject));
            if (kind == BeliefClaimKind.Presence && !subject.HasValue)
                throw new ArgumentException("Presence claims require a subject.", nameof(subject));
            if (kind == BeliefClaimKind.Presence && (itemType.HasValue || quantity.HasValue))
                throw new ArgumentException("Presence claims cannot contain item details.");

            // Attributed interaction memories name an actor and a kind of interaction
            // ("Ralf cheated me", "Bessa gave me bread"); they never carry item details.
            bool isAttributed = kind == BeliefClaimKind.WrongedBy || kind == BeliefClaimKind.GiftFrom ||
                kind == BeliefClaimKind.FairTradeWith;
            if (isAttributed && !subject.HasValue)
                throw new ArgumentException("Attributed interaction claims require a subject.", nameof(subject));
            if (isAttributed && (itemType.HasValue || quantity.HasValue))
                throw new ArgumentException("Attributed interaction claims cannot contain item details.");

            Kind = kind;
            Location = location;
            ItemType = itemType;
            Subject = subject;
            Quantity = quantity;
        }

        public BeliefClaimKind Kind { get; }
        public LocationId Location { get; }
        public ItemTypeId? ItemType { get; }
        public ActorId? Subject { get; }
        public int? Quantity { get; }

        public bool Equals(BeliefClaim other) => other != null && Kind == other.Kind && Location == other.Location &&
            ItemType == other.ItemType && Subject == other.Subject && Quantity == other.Quantity;
        public override bool Equals(object obj) => Equals(obj as BeliefClaim);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 397 ^ Location.GetHashCode();
                hash = hash * 397 ^ ItemType.GetHashCode();
                hash = hash * 397 ^ Subject.GetHashCode();
                return hash * 397 ^ Quantity.GetHashCode();
            }
        }

        public int CompareTo(BeliefClaim other)
        {
            if (other == null) return 1;
            int comparison = Kind.CompareTo(other.Kind);
            if (comparison != 0) return comparison;
            comparison = Location.CompareTo(other.Location);
            if (comparison != 0) return comparison;
            comparison = Compare(ItemType, other.ItemType, (left, right) => left.CompareTo(right));
            if (comparison != 0) return comparison;
            comparison = Compare(Subject, other.Subject, CompareActors);
            if (comparison != 0) return comparison;
            return Nullable.Compare(Quantity, other.Quantity);
        }

        private static int Compare<T>(T? left, T? right, Func<T, T, int> comparison) where T : struct
        {
            if (!left.HasValue) return right.HasValue ? -1 : 0;
            return right.HasValue ? comparison(left.Value, right.Value) : 1;
        }

        private static int CompareActors(ActorId left, ActorId right)
        {
            if (left.IsPlayer != right.IsPlayer) return left.IsPlayer ? -1 : 1;
            if (left.IsPlayer) return 0;
            return left.Npc.Value.CompareTo(right.Npc.Value);
        }
    }
}
