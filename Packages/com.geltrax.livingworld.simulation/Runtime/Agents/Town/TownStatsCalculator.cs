using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Computes the 11 town stats from world truth only (P5-01, TOWN.md section 1).
    /// Reads inventories, NPCs, the event log, funds and town state; never beliefs.
    /// Deterministic: no RNG, no wall clock, and every iteration follows an ordered
    /// collection or ID-ordered event query.
    /// </summary>
    public static class TownStatsCalculator
    {
        /// <summary>Days counted as "recent" for crime, fear events and festivals.</summary>
        public const int RecentWindowDays = 30;

        /// <summary>Safety lost per wolf incident this winter (TOWN.md).</summary>
        public const int WolfIncidentSafetyPenalty = 15;

        /// <summary>Safety lost per unresolved crime (TOWN.md).</summary>
        public const int UnresolvedCrimeSafetyPenalty = 5;

        /// <summary>Safety gained while the night watch is active (TOWN.md).</summary>
        public const int NightWatchSafetyBonus = 10;

        /// <summary>Trade copper of a peak warm month (TOWN.md).</summary>
        public const int PeakMonthlyTradeCopper = 1700;

        /// <summary>Working age bounds; children and elders are excluded from employment (TOWN.md).</summary>
        public const int WorkingAgeMin = 15;
        public const int WorkingAgeMax = 64;

        /// <summary>A roof at or above this condition is sound (P5-01).</summary>
        public const int SoundRoofCondition = 50;

        /// <summary>
        /// Occupations that are not productive work. Wives count as productive: the
        /// design's household economy treats them as workers (TOWN.md: "almost
        /// everyone works", and the starting roster has no other unemployed adults).
        /// </summary>
        private static readonly HashSet<string> NonProductiveOccupations =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "child", "retired", "none", "unemployed"
            };

        public static TownStats Compute(WorldState state, ItemCatalog catalog)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            int population = state.Npcs.Count + TownStatNumbers.BackgroundVillagers;
            int crime = CountRecent(state, WorldEventType.Theft);
            int wolfIncidents = CountWolfIncidentsThisWinter(state);

            return new TownStats(
                population: population,
                wealthCopper: WealthCalculator.ComputeWealthCopper(state, catalog),
                foodSupply: FoodSupplyCalculator.ComputeFoodSupply(state, catalog),
                safety: ComputeSafety(state, wolfIncidents, crime),
                housing: ComputeHousing(state),
                employment: ComputeEmployment(state),
                trade: ComputeTrade(state),
                happiness: ComputeHappiness(state, crime),
                crime: crime,
                infrastructure: ComputeInfrastructure(state),
                reputation: ComputeReputation(state));
        }

        private static int ComputeSafety(WorldState state, int wolfIncidents, int unresolvedCrimes)
        {
            int safety = 100
                - wolfIncidents * WolfIncidentSafetyPenalty
                - unresolvedCrimes * UnresolvedCrimeSafetyPenalty
                + (state.NightWatch.IsActive ? NightWatchSafetyBonus : 0)
                + state.Infrastructure.PalisadeCondition / 10;
            return Clamp(safety);
        }

        private static int ComputeHousing(WorldState state)
        {
            var homes = new SortedSet<LocationId>();
            foreach (NpcState npc in state.Npcs.Npcs)
                homes.Add(npc.Definition.Home);
            int sound = 0;
            foreach (LocationId home in homes)
                if (state.Housing.RoofConditionOrDefault(home) >= SoundRoofCondition)
                    sound++;
            sound += state.Housing.BackgroundSoundRoofs;
            int total = homes.Count + state.Housing.BackgroundHouseholds;
            if (total == 0) return 100;
            return (int)Math.Round(100.0 * sound / total, MidpointRounding.AwayFromZero);
        }

        private static int ComputeEmployment(WorldState state)
        {
            int workingAge = 0;
            int employed = 0;
            foreach (NpcState npc in state.Npcs.Npcs)
            {
                int age = npc.Definition.Age;
                if (age < WorkingAgeMin || age > WorkingAgeMax) continue;
                workingAge++;
                if (!NonProductiveOccupations.Contains(npc.Definition.Occupation))
                    employed++;
            }
            if (workingAge == 0) return 100;
            return (int)Math.Round(100.0 * employed / workingAge, MidpointRounding.AwayFromZero);
        }

        private static int ComputeTrade(WorldState state)
        {
            long travelerCopper = 0;
            foreach (int paid in state.TravelerSpend.PaidThisMonth)
                travelerCopper += paid;
            long total = travelerCopper + state.MerchantSchedule.CopperThisMonth;
            double trade = 100.0 * total / PeakMonthlyTradeCopper;
            if (trade > 100.0) trade = 100.0;
            return (int)Math.Round(trade, MidpointRounding.AwayFromZero);
        }

        private static int ComputeHappiness(WorldState state, int thefts)
        {
            int fearEvents = thefts + CountRecent(state, WorldEventType.Predation);
            bool festival = RecentFestival(state);
            double total = 0;
            int count = 0;
            foreach (NpcState npc in state.Npcs.Npcs)
            {
                // TOWN.md: social need met x 0.4 + food security x 0.3 + recent festival x 0.2
                // - fear events x 0.1, on 0-100 scales. Food security is read as the inverse
                // of the hunger need: a well-fed NPC is food-secure.
                double score = npc.Needs.Social * 0.4
                    + (100 - npc.Needs.Hunger) * 0.3
                    + (festival ? 100 : 0) * 0.2
                    - fearEvents * 0.1;
                total += score;
                count++;
            }
            if (count == 0) return 50;
            return Clamp((int)Math.Round(total / count, MidpointRounding.AwayFromZero));
        }

        private static int ComputeInfrastructure(WorldState state)
        {
            TownInfrastructureState infrastructure = state.Infrastructure;
            double value = infrastructure.WheelCondition * 0.4
                + infrastructure.PalisadeCondition * 0.25
                + infrastructure.WellAndRoadsCondition * 0.2
                + infrastructure.GranaryBuildingCondition * 0.15;
            return Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        private static int ComputeReputation(WorldState state)
        {
            OutwardReputationState reputation = state.Reputation;
            double value = (reputation.AppleFame + reputation.RoadSafety + reputation.Hospitality) / 3.0;
            return Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        /// <summary>Events of a type within the last 30 days (both bounds inclusive).</summary>
        private static int CountRecent(WorldState state, WorldEventType type)
        {
            const long minutesPerDay = 1440;
            long cutoffMinutes = state.Clock.TotalMinutes >= RecentWindowDays * minutesPerDay
                ? state.Clock.TotalMinutes - RecentWindowDays * minutesPerDay
                : 0;
            return state.Events.Query(from: new GameTime(cutoffMinutes), type: type).Count;
        }

        /// <summary>
        /// Livestock predations since the start of the most recent winter (TOWN.md:
        /// "wolf incidents this winter"). A predation counts as a wolf incident only
        /// when it took livestock, i.e. the event carries a copper value.
        /// </summary>
        private static int CountWolfIncidentsThisWinter(WorldState state)
        {
            long today = state.Clock.Day;
            long yearStart = (today - 1) / VillageCalendar.DaysPerYear * VillageCalendar.DaysPerYear;
            // Winter is days 91-180 of the 360-day year (VillageCalendar.SeasonAt).
            long winterStart = yearStart + 91;
            if (today < winterStart) winterStart -= VillageCalendar.DaysPerYear;
            // Before day 91 of the first year there is no winter history to count:
            // clamp the cutoff to the start of time (no event predates day 1).
            long cutoffDay = winterStart < 1 ? 1 : winterStart;
            var cutoff = new GameTime((cutoffDay - 1) * 1440);
            int incidents = 0;
            foreach (WorldEvent entry in state.Events.Query(from: cutoff, type: WorldEventType.Predation))
                if (entry.Copper.HasValue && entry.Copper.Value > 0)
                    incidents++;
            return incidents;
        }

        /// <summary>
        /// True when a festival fell within the last 30 days. Festivals are calendar
        /// facts (TOWN.md section 3): the Harvest Feast, Thawday (first Restday of
        /// spring) and Longnight (midwinter, day 135 of the year).
        /// </summary>
        private static bool RecentFestival(WorldState state)
        {
            long today = state.Clock.Day;
            long[] festivals =
            {
                MostRecent(today, VillageCalendar.HarvestFeastDay(today)),
                MostRecent(today, FirstThawdayOfYear(today)),
                MostRecent(today, LongnightOfYear(today)),
            };
            foreach (long festival in festivals)
                if (today - festival < RecentWindowDays)
                    return true;
            return false;
        }

        private static long MostRecent(long today, long thisYearDay) =>
            thisYearDay <= today ? thisYearDay : thisYearDay - VillageCalendar.DaysPerYear;

        private static long FirstThawdayOfYear(long today)
        {
            long yearStart = (today - 1) / VillageCalendar.DaysPerYear * VillageCalendar.DaysPerYear;
            // Spring starts at day 181; Restday is weekday index 6: (day + 1) % 7 == 6.
            long day = yearStart + 181;
            while ((day + 1) % 7 != 6) day++;
            return day;
        }

        private static long LongnightOfYear(long today)
        {
            long yearStart = (today - 1) / VillageCalendar.DaysPerYear * VillageCalendar.DaysPerYear;
            return yearStart + 135;
        }

        private static int Clamp(int value) => value < 0 ? 0 : value > 100 ? 100 : value;
    }
}
