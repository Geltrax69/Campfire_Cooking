using System;
using System.Globalization;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Identifies inter-village news by a positive, store-assigned sequence number
    /// (P6-03); default is invalid. Mirrors the WorldEventId pattern.
    /// </summary>
    public readonly struct NewsId : IEquatable<NewsId>, IComparable<NewsId>
    {
        public NewsId(long value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(NewsId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is NewsId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(NewsId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
        public static bool operator ==(NewsId left, NewsId right) => left.Equals(right);
        public static bool operator !=(NewsId left, NewsId right) => !left.Equals(right);
        public static bool operator <(NewsId left, NewsId right) => left.CompareTo(right) < 0;
        public static bool operator >(NewsId left, NewsId right) => left.CompareTo(right) > 0;
        public static bool operator <=(NewsId left, NewsId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(NewsId left, NewsId right) => left.CompareTo(right) >= 0;
    }
}
