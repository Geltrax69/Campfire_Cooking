using System;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Identifies a trade route by its ordinal content key (e.g. "route_millbrook_kings_rest"); default is invalid.</summary>
    public readonly struct TradeRouteId : IEquatable<TradeRouteId>, IComparable<TradeRouteId>
    {
        public TradeRouteId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A trade route ID must not be null, empty or whitespace.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => Value != null;
        public bool Equals(TradeRouteId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TradeRouteId other && Equals(other);
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
        public int CompareTo(TradeRouteId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(TradeRouteId left, TradeRouteId right) => left.Equals(right);
        public static bool operator !=(TradeRouteId left, TradeRouteId right) => !left.Equals(right);
        public static bool operator <(TradeRouteId left, TradeRouteId right) => left.CompareTo(right) < 0;
        public static bool operator >(TradeRouteId left, TradeRouteId right) => left.CompareTo(right) > 0;
        public static bool operator <=(TradeRouteId left, TradeRouteId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(TradeRouteId left, TradeRouteId right) => left.CompareTo(right) >= 0;
    }
}
