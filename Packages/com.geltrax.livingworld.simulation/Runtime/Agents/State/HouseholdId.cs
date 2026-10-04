using System;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Identifies a household (the NPCs living under one roof); default is invalid.
    /// Lives in Agents (not Core) so families stay inside the Agents folder;
    /// the shape matches the Core content-ID pattern exactly.
    /// </summary>
    public readonly struct HouseholdId : IEquatable<HouseholdId>, IComparable<HouseholdId>
    {
        public HouseholdId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A household ID must not be null, empty or whitespace.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => Value != null;
        public bool Equals(HouseholdId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is HouseholdId other && Equals(other);
        public override int GetHashCode()
        {
            if (Value == null) return 0;
            // FNV-1a over UTF-16 code units, matching the Core content-ID hash.
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in Value) hash = (hash ^ character) * 16777619;
                return (int)hash;
            }
        }
        public int CompareTo(HouseholdId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(HouseholdId left, HouseholdId right) => left.Equals(right);
        public static bool operator !=(HouseholdId left, HouseholdId right) => !left.Equals(right);
        public static bool operator <(HouseholdId left, HouseholdId right) => left.CompareTo(right) < 0;
        public static bool operator >(HouseholdId left, HouseholdId right) => left.CompareTo(right) > 0;
        public static bool operator <=(HouseholdId left, HouseholdId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(HouseholdId left, HouseholdId right) => left.CompareTo(right) >= 0;
    }
}
