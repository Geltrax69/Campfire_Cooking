using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Classifies approved scheduled and utility-selected NPC activities.</summary>
    public enum ActivityKind { Eat, Sleep, Work, Socialize, Shop, Rest, Chores, Pray, Learn, Play }

    /// <summary>Immutable daily activity interval with a destination.</summary>
    public sealed class ScheduleEntry
    {
        public ScheduleEntry(ActivityKind kind, int fromMinute, int toMinute, LocationId destination)
        {
            ValidateKind(kind);
            if (fromMinute < 0 || fromMinute >= NpcSchedule.MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(fromMinute));
            if (toMinute < 0 || toMinute >= NpcSchedule.MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(toMinute));
            if (fromMinute == toMinute) throw new ArgumentException("Schedule entries must have a positive duration.");
            if (!destination.IsValid) throw new ArgumentException("A schedule destination must be valid.", nameof(destination));
            Kind = kind;
            FromMinute = fromMinute;
            ToMinute = toMinute;
            Destination = destination;
        }

        public ActivityKind Kind { get; }
        public int FromMinute { get; }
        public int ToMinute { get; }
        public LocationId Destination { get; }
        public bool IsOvernight => ToMinute < FromMinute;

        internal bool ContainsSameDay(int minute) => IsOvernight
            ? minute >= FromMinute : minute >= FromMinute && minute < ToMinute;

        internal bool ContainsFollowingDay(int minute) => IsOvernight && minute < ToMinute;

        internal static void ValidateKind(ActivityKind kind)
        {
            if (kind < ActivityKind.Eat || kind > ActivityKind.Play)
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>Immutable approved workday and Restday schedules with calendar-aware lookup.</summary>
    public sealed class NpcSchedule
    {
        internal const int MinutesPerDay = 1440;

        public NpcSchedule(IEnumerable<ScheduleEntry> workday, IEnumerable<ScheduleEntry> restday)
        {
            Workday = CopyAndValidate(workday, nameof(workday));
            Restday = CopyAndValidate(restday, nameof(restday));
        }

        public IReadOnlyList<ScheduleEntry> Workday { get; }
        public IReadOnlyList<ScheduleEntry> Restday { get; }

        public ScheduleEntry At(GameTime time)
        {
            int minute = time.Hour * 60 + time.Minute;
            IReadOnlyList<ScheduleEntry> current = IsRestday(time.Day) ? Restday : Workday;
            foreach (ScheduleEntry entry in current)
                if (entry.ContainsSameDay(minute)) return entry;

            long previousDay = time.Day - 1;
            IReadOnlyList<ScheduleEntry> previous = IsRestday(previousDay) ? Restday : Workday;
            foreach (ScheduleEntry entry in previous)
                if (entry.ContainsFollowingDay(minute)) return entry;
            return null;
        }

        private static bool IsRestday(long oneBasedDay)
        {
            // Day 1 is Thirdday (calendar index 2); Restday has index 6.
            long zeroBased = oneBasedDay - 1;
            long index = ((zeroBased + 2) % 7 + 7) % 7;
            return index == 6;
        }

        private static IReadOnlyList<ScheduleEntry> CopyAndValidate(
            IEnumerable<ScheduleEntry> entries, string parameterName)
        {
            if (entries == null) throw new ArgumentNullException(parameterName);
            var copied = new List<ScheduleEntry>();
            foreach (ScheduleEntry entry in entries)
            {
                if (entry == null) throw new ArgumentException("Schedules cannot contain null entries.", parameterName);
                foreach (ScheduleEntry existing in copied)
                    if (Overlaps(existing, entry))
                        throw new ArgumentException("Schedule entries cannot overlap.", parameterName);
                copied.Add(entry);
            }
            return new ReadOnlyCollection<ScheduleEntry>(copied);
        }

        private static bool Overlaps(ScheduleEntry left, ScheduleEntry right)
        {
            return SegmentsOverlap(left.FromMinute, left.IsOvernight ? MinutesPerDay : left.ToMinute,
                       right.FromMinute, right.IsOvernight ? MinutesPerDay : right.ToMinute)
                || (left.IsOvernight && SegmentsOverlap(0, left.ToMinute,
                       right.FromMinute, right.IsOvernight ? MinutesPerDay : right.ToMinute))
                || (right.IsOvernight && SegmentsOverlap(left.FromMinute,
                       left.IsOvernight ? MinutesPerDay : left.ToMinute, 0, right.ToMinute))
                || (left.IsOvernight && right.IsOvernight && SegmentsOverlap(0, left.ToMinute, 0, right.ToMinute));
        }

        private static bool SegmentsOverlap(int leftStart, int leftEnd, int rightStart, int rightEnd) =>
            leftStart < rightEnd && rightStart < leftEnd;
    }
}
