using System;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Identifies an animal species by its ordinal content key (e.g. "species_chicken");
    /// default is invalid. Lives in Agents (not Core) so animal state stays inside
    /// the Agents folder; the shape matches the Core content-ID pattern exactly.
    /// </summary>
    public readonly struct SpeciesId : IEquatable<SpeciesId>, IComparable<SpeciesId>
    {
        public SpeciesId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A species ID must not be null, empty or whitespace.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => Value != null;
        public bool Equals(SpeciesId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SpeciesId other && Equals(other);
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
        public int CompareTo(SpeciesId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(SpeciesId left, SpeciesId right) => left.Equals(right);
        public static bool operator !=(SpeciesId left, SpeciesId right) => !left.Equals(right);
        public static bool operator <(SpeciesId left, SpeciesId right) => left.CompareTo(right) < 0;
        public static bool operator >(SpeciesId left, SpeciesId right) => left.CompareTo(right) > 0;
        public static bool operator <=(SpeciesId left, SpeciesId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(SpeciesId left, SpeciesId right) => left.CompareTo(right) >= 0;
    }
}
