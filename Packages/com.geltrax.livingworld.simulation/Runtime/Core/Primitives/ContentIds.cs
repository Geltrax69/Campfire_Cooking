using System;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Identifies an NPC by its ordinal content key; default is invalid.</summary>
    public readonly struct NpcId : IEquatable<NpcId>, IComparable<NpcId>
    {
        public NpcId(string value) { Value = ContentIdValue.Validate(value); }
        public string Value { get; }
        public bool IsValid => Value != null;
        public bool Equals(NpcId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is NpcId other && Equals(other);
        public override int GetHashCode() => ContentIdValue.Hash(Value);
        public int CompareTo(NpcId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(NpcId left, NpcId right) => left.Equals(right);
        public static bool operator !=(NpcId left, NpcId right) => !left.Equals(right);
        public static bool operator <(NpcId left, NpcId right) => left.CompareTo(right) < 0;
        public static bool operator >(NpcId left, NpcId right) => left.CompareTo(right) > 0;
        public static bool operator <=(NpcId left, NpcId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(NpcId left, NpcId right) => left.CompareTo(right) >= 0;
    }

    /// <summary>Identifies a location by its ordinal content key; default is invalid.</summary>
    public readonly struct LocationId : IEquatable<LocationId>, IComparable<LocationId>
    {
        public LocationId(string value) { Value = ContentIdValue.Validate(value); }
        public string Value { get; }
        public bool IsValid => Value != null;
        public bool Equals(LocationId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is LocationId other && Equals(other);
        public override int GetHashCode() => ContentIdValue.Hash(Value);
        public int CompareTo(LocationId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(LocationId left, LocationId right) => left.Equals(right);
        public static bool operator !=(LocationId left, LocationId right) => !left.Equals(right);
        public static bool operator <(LocationId left, LocationId right) => left.CompareTo(right) < 0;
        public static bool operator >(LocationId left, LocationId right) => left.CompareTo(right) > 0;
        public static bool operator <=(LocationId left, LocationId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(LocationId left, LocationId right) => left.CompareTo(right) >= 0;
    }

    /// <summary>Identifies an item type by its ordinal content key; default is invalid.</summary>
    public readonly struct ItemTypeId : IEquatable<ItemTypeId>, IComparable<ItemTypeId>
    {
        public ItemTypeId(string value) { Value = ContentIdValue.Validate(value); }
        public string Value { get; }
        public bool IsValid => Value != null;
        public bool Equals(ItemTypeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ItemTypeId other && Equals(other);
        public override int GetHashCode() => ContentIdValue.Hash(Value);
        public int CompareTo(ItemTypeId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ItemTypeId left, ItemTypeId right) => left.Equals(right);
        public static bool operator !=(ItemTypeId left, ItemTypeId right) => !left.Equals(right);
        public static bool operator <(ItemTypeId left, ItemTypeId right) => left.CompareTo(right) < 0;
        public static bool operator >(ItemTypeId left, ItemTypeId right) => left.CompareTo(right) > 0;
        public static bool operator <=(ItemTypeId left, ItemTypeId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(ItemTypeId left, ItemTypeId right) => left.CompareTo(right) >= 0;
    }

    /// <summary>Identifies a reputation group by its ordinal content key; default is invalid.</summary>
    public readonly struct ReputationGroupId : IEquatable<ReputationGroupId>, IComparable<ReputationGroupId>
    {
        public ReputationGroupId(string value) { Value = ContentIdValue.Validate(value); }
        public string Value { get; }
        public bool IsValid => Value != null;
        public bool Equals(ReputationGroupId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ReputationGroupId other && Equals(other);
        public override int GetHashCode() => ContentIdValue.Hash(Value);
        public int CompareTo(ReputationGroupId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ReputationGroupId left, ReputationGroupId right) => left.Equals(right);
        public static bool operator !=(ReputationGroupId left, ReputationGroupId right) => !left.Equals(right);
        public static bool operator <(ReputationGroupId left, ReputationGroupId right) => left.CompareTo(right) < 0;
        public static bool operator >(ReputationGroupId left, ReputationGroupId right) => left.CompareTo(right) > 0;
        public static bool operator <=(ReputationGroupId left, ReputationGroupId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(ReputationGroupId left, ReputationGroupId right) => left.CompareTo(right) >= 0;
    }

    /// <summary>Identifies a village by its ordinal content key; default is invalid.</summary>
    public readonly struct VillageId : IEquatable<VillageId>, IComparable<VillageId>
    {
        public VillageId(string value) { Value = ContentIdValue.Validate(value); }
        public string Value { get; }
        public bool IsValid => Value != null;
        public bool Equals(VillageId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is VillageId other && Equals(other);
        public override int GetHashCode() => ContentIdValue.Hash(Value);
        public int CompareTo(VillageId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(VillageId left, VillageId right) => left.Equals(right);
        public static bool operator !=(VillageId left, VillageId right) => !left.Equals(right);
        public static bool operator <(VillageId left, VillageId right) => left.CompareTo(right) < 0;
        public static bool operator >(VillageId left, VillageId right) => left.CompareTo(right) > 0;
        public static bool operator <=(VillageId left, VillageId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(VillageId left, VillageId right) => left.CompareTo(right) >= 0;
    }

    internal static class ContentIdValue
    {
        internal static string Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A content ID must not be null, empty or whitespace.", nameof(value));
            return value;
        }

        internal static int Hash(string value)
        {
            if (value == null) return 0;
            // FNV-1a over UTF-16 code units avoids randomized string hashes and byte-order differences.
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value) hash = (hash ^ character) * 16777619;
                return (int)hash;
            }
        }
    }
}
