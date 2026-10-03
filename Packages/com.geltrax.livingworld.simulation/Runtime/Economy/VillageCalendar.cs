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

        /// <summary>
        /// The Harvest Feast falls on the last Restday of autumn (WORLD.md festivals).
        /// Day 1 is Thirdday and Restday is weekday index 6 (see NpcSchedule), so a day
        /// is a Restday exactly when (day+1) is divisible by 7 with remainder 6; the
        /// largest such day in autumn (days 1–90) is day 89. Returns the feast day of
        /// the year containing the given day.
        /// </summary>
        public static long HarvestFeastDay(long day)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            long yearStart = (day - 1) / DaysPerYear * DaysPerYear;
            long feast = yearStart + DaysPerSeason;
            while ((feast + 1) % 7 != 6) feast--;
            return feast;
        }
    }
}
