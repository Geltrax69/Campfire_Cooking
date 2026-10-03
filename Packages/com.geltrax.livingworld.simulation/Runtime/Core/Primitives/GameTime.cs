using System;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Immutable nonnegative game minutes since world start, with one-based days.</summary>
    public readonly struct GameTime : IEquatable<GameTime>, IComparable<GameTime>
    {
        private const int MinutesPerHour = 60;
        private const int HoursPerDay = 24;
        private const int MinutesPerDay = MinutesPerHour * HoursPerDay;

        public GameTime(long totalMinutes)
        {
            if (totalMinutes < 0) throw new ArgumentOutOfRangeException(nameof(totalMinutes));
            TotalMinutes = totalMinutes;
        }

        public long TotalMinutes { get; }
        public long Day => TotalMinutes / MinutesPerDay + 1;
        public int Hour => (int)(TotalMinutes / MinutesPerHour % HoursPerDay);
        public int Minute => (int)(TotalMinutes % MinutesPerHour);

        public GameTime Advance(long minutes)
        {
            if (minutes < 0) throw new ArgumentOutOfRangeException(nameof(minutes));
            return new GameTime(checked(TotalMinutes + minutes));
        }

        public bool Equals(GameTime other) => TotalMinutes == other.TotalMinutes;
        public override bool Equals(object obj) => obj is GameTime other && Equals(other);
        public override int GetHashCode() => TotalMinutes.GetHashCode();
        public int CompareTo(GameTime other) => TotalMinutes.CompareTo(other.TotalMinutes);
        public static bool operator ==(GameTime left, GameTime right) => left.Equals(right);
        public static bool operator !=(GameTime left, GameTime right) => !left.Equals(right);
        public static bool operator <(GameTime left, GameTime right) => left.CompareTo(right) < 0;
        public static bool operator >(GameTime left, GameTime right) => left.CompareTo(right) > 0;
        public static bool operator <=(GameTime left, GameTime right) => left.CompareTo(right) <= 0;
        public static bool operator >=(GameTime left, GameTime right) => left.CompareTo(right) >= 0;
    }
}
