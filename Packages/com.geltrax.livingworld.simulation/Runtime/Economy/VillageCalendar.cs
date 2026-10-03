using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>The village's seasons. The game starts on a Thirdday morning in early autumn.</summary>
    public enum Season
    {
        Autumn,
        Winter,
        Spring,
        Summer
    }

    /// <summary>
    /// The village calendar: a 360-day year of four 90-day seasons, starting in early autumn
    /// on day 1 (ECONOMY.md: the game opens in early autumn, the harvest season). Seasons drive
    /// the money gates — traveling merchants only come in warm months, travelers vanish in
    /// winter, wolf bounties pay only in winter. Months are 30-day blocks aligned to the year,
    /// so a month never straddles two seasons.
    /// </summary>
    public static class VillageCalendar
    {
        public const int DaysPerSeason = 90;
        public const int DaysPerYear = 360;
        public const int DaysPerMonth = 30;

        /// <summary>Returns the season containing the given time. Deterministic and pure.</summary>
        public static Season SeasonAt(GameTime time)
        {
            long dayOfYear = (time.Day - 1) % DaysPerYear;
            if (dayOfYear < DaysPerSeason) return Season.Autumn;
            if (dayOfYear < 2 * DaysPerSeason) return Season.Winter;
            if (dayOfYear < 3 * DaysPerSeason) return Season.Spring;
            return Season.Summer;
        }

        /// <summary>Warm months are when the Alder Road is passable: everything but winter.</summary>
        public static bool IsWarm(Season season) => season != Season.Winter;

        /// <summary>Zero-based month index within the year (0-11).</summary>
        public static int MonthIndex(GameTime time) => (int)((time.Day - 1) % DaysPerYear / DaysPerMonth);

        /// <summary>One-based day within the 30-day month (1-30).</summary>
        public static int DayOfMonth(GameTime time) => (int)((time.Day - 1) % DaysPerMonth) + 1;

        /// <summary>
        /// Returns the first day of the spring following the given day number. Traveling
        /// merchants wait out the winter (the ford runs high and cold) and return in spring.
        /// </summary>
        public static long FirstSpringDayOnOrAfter(long day)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            long year = (day - 1) / DaysPerYear;
            long springStart = year * DaysPerYear + 2 * DaysPerSeason + 1;
            return day <= springStart ? springStart : springStart + DaysPerYear;
        }
    }
}
