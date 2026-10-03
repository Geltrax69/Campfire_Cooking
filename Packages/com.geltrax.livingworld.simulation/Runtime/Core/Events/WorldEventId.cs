using System;
using System.Globalization;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Identifies world truth by a positive, log-assigned sequence number; default is invalid.</summary>
    public readonly struct WorldEventId : IEquatable<WorldEventId>, IComparable<WorldEventId>
    {
        public WorldEventId(long value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(WorldEventId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WorldEventId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(WorldEventId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
        public static bool operator ==(WorldEventId left, WorldEventId right) => left.Equals(right);
        public static bool operator !=(WorldEventId left, WorldEventId right) => !left.Equals(right);
        public static bool operator <(WorldEventId left, WorldEventId right) => left.CompareTo(right) < 0;
        public static bool operator >(WorldEventId left, WorldEventId right) => left.CompareTo(right) > 0;
        public static bool operator <=(WorldEventId left, WorldEventId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(WorldEventId left, WorldEventId right) => left.CompareTo(right) >= 0;
    }
}
