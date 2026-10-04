using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Immutable caller configuration for winter pressure.</summary>
    public sealed class WinterPressureConfiguration
    {
        public WinterPressureConfiguration(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A configuration needs an ID.", nameof(id));
            Id = id;
        }

        public string Id { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of winter pressure: the last day processed,
    /// the last winter year rolled, the incident days still outstanding this
    /// winter, and whether the deer are currently ranging at the farms.
    /// Starts uninitialized; the system stays quiet until the world-build step
    /// initializes it.
    /// </summary>
    public sealed class WinterPressureState
    {
        public WinterPressureState(bool initialized = false, long lastLossDay = 0,
            long lastWinterYear = -1, IReadOnlyList<long> incidentDays = null,
            bool deerAtFarms = false)
        {
            IsInitialized = initialized;
            LastLossDay = lastLossDay;
            LastWinterYear = lastWinterYear;
            IncidentDays = incidentDays == null
                ? new List<long>().AsReadOnly()
                : new List<long>(incidentDays).AsReadOnly();
            DeerAtFarms = deerAtFarms;
        }

        public bool IsInitialized { get; }
        public long LastLossDay { get; internal set; }
        public long LastWinterYear { get; internal set; }
        public IReadOnlyList<long> IncidentDays { get; internal set; }
        public bool DeerAtFarms { get; internal set; }
    }

    /// <summary>
    /// Winter pressure (agents.winter_pressure, Actions phase): when winter comes,
    /// deep snow pushes the deer to the farm fields and the wolf pack ranges to
    /// the forest edge after them. The pack takes 2-4 livestock across the season
    /// on seeded incident days — 3-4 when the deer herd is scarce (below half its
    /// design size), because the pack must eat (docs/design/ANIMALS.md). The deer
    /// at the farms cause a small recorded crop loss. When spring comes the deer
    /// drift back to the forest.
    /// </summary>
    public sealed class WinterPressureSystem : IWorldSystem
    {
        /// <summary>Crop loss in copper per deer wintering at the farms.</summary>
        public const int CropDamageCopperPerDeer = 2;
        /// <summary>Below half the design herd, wolves lean harder on livestock.</summary>
        public const int DeerScarcityThreshold = 20;

        private static readonly SpeciesId Deer = new SpeciesId("species_deer");
        private static readonly SpeciesId Chicken = new SpeciesId("species_chicken");
        private static readonly SpeciesId Pig = new SpeciesId("species_pig");
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly LocationId ForestEdge = new LocationId("loc_forest_edge");

        private readonly List<WinterPressureConfiguration> _configurations;

        public WinterPressureSystem(IEnumerable<WinterPressureConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<WinterPressureConfiguration>();
            foreach (WinterPressureConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException(
                        "Winter pressure configurations cannot contain null.",
                        nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "agents.winter_pressure";
        public SimulationPhase Phase => SimulationPhase.Actions;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.WinterPressure.IsInitialized) return;
            WinterPressureState progress = state.WinterPressure;
            while (progress.LastLossDay < state.Clock.Day)
            {
                progress.LastLossDay++;
                RunDay(state, progress.LastLossDay);
            }
        }

        private static void RunDay(WorldState state, long day)
        {
            var time = new GameTime((day - 1) * 1440);
            Season season = VillageCalendar.SeasonAt(time);
            long year = (day - 1) / VillageCalendar.DaysPerYear;

            if (season == Season.Winter)
            {
                if (state.WinterPressure.LastWinterYear != year)
                    BeginWinter(state, day, year);
                if (state.WinterPressure.IncidentDays.Contains(day))
                    TakeLivestock(state, time, day);
            }
            else if (season == Season.Spring && state.WinterPressure.DeerAtFarms)
            {
                // Winter is over; the herd drifts back to the worked forest.
                foreach (AnimalState deer in state.Animals.GetBySpecies(Deer))
                    if (deer.Location == Farm)
                        deer.MoveTo(ForestEdge);
                state.WinterPressure.DeerAtFarms = false;
            }
        }

        private static void BeginWinter(WorldState state, long day, long year)
        {
            var time = new GameTime((day - 1) * 1440);
            long winterEnd = year * VillageCalendar.DaysPerYear + 2 * VillageCalendar.DaysPerSeason;

            // 2-4 livestock incidents per winter (WORLD.md, load-bearing), on distinct
            // days spread across the season; a scarce herd pushes it to 3-4.
            int deer = state.Animals.PopulationCount(Deer);
            int incidents = deer < DeerScarcityThreshold
                ? 3 + state.Rng.NextInt(2)
                : 2 + state.Rng.NextInt(3);
            var days = new HashSet<long>();
            while (days.Count < incidents)
                days.Add(day + state.Rng.NextInt((int)(winterEnd - day + 1)));
            var ordered = new List<long>(days);
            ordered.Sort();
            state.WinterPressure.LastWinterYear = year;
            state.WinterPressure.IncidentDays = ordered.AsReadOnly();

            // Deep snow yards the deer near the farms for browse.
            foreach (AnimalState animal in state.Animals.GetBySpecies(Deer))
                animal.MoveTo(Farm);
            state.WinterPressure.DeerAtFarms = true;

            // The deer strip bark and browse: a small loss, recorded for the town.
            if (deer > 0)
                state.Events.Append(time, Farm, WorldEventType.CropDamage,
                    visibility: EventVisibility.Quiet,
                    copper: CropDamageCopperPerDeer * deer);
        }

        private static void TakeLivestock(WorldState state, GameTime time, long day)
        {
            var remaining = new List<long>(state.WinterPressure.IncidentDays);
            remaining.Remove(day);
            state.WinterPressure.IncidentDays = remaining.AsReadOnly();

            var candidates = new List<AnimalState>();
            foreach (AnimalState chicken in state.Animals.GetBySpecies(Chicken))
                candidates.Add(chicken);
            foreach (AnimalState pig in state.Animals.GetBySpecies(Pig))
                if (pig.Location != ForestEdge) // wild boars are never prey
                    candidates.Add(pig);
            candidates.Sort((left, right) =>
                string.CompareOrdinal(left.Id.Value, right.Id.Value));
            if (candidates.Count == 0) return;

            AnimalState victim = candidates[state.Rng.NextInt(candidates.Count)];
            state.Animals.Remove(victim.Id);
            state.Events.Append(time, victim.Location, WorldEventType.Predation,
                visibility: EventVisibility.Quiet, quantity: 1,
                copper: LivestockValue.ForSpecies(victim.Species));
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WinterPressureConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException(
                        "Winter pressure configuration IDs must be unique.", "configurations");
        }
    }
}
