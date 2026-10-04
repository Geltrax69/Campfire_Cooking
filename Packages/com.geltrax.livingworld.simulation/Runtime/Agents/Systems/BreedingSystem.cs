using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Immutable caller configuration for spring breeding, including population
    /// caps. Caps are global to the world, so the system uses the first
    /// registered configuration.
    /// </summary>
    public sealed class BreedingConfiguration
    {        public BreedingConfiguration(string id,
            int deerCap = DefaultDeerCap,
            int wolfCap = DefaultWolfCap,
            int chickenCap = DefaultChickenCap,
            int pigCap = DefaultPigCap,
            int bramblebackCap = DefaultBramblebackCap)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A configuration needs an ID.", nameof(id));
            if (deerCap <= 0) throw new ArgumentOutOfRangeException(nameof(deerCap));
            if (wolfCap <= 0) throw new ArgumentOutOfRangeException(nameof(wolfCap));
            if (chickenCap <= 0) throw new ArgumentOutOfRangeException(nameof(chickenCap));
            if (pigCap <= 0) throw new ArgumentOutOfRangeException(nameof(pigCap));
            if (bramblebackCap <= 0) throw new ArgumentOutOfRangeException(nameof(bramblebackCap));
            Id = id;
            DeerCap = deerCap;
            WolfCap = wolfCap;
            ChickenCap = chickenCap;
            PigCap = pigCap;
            BramblebackCap = bramblebackCap;
        }

        /// <summary>All the worked forest's browse supports (docs/design/ANIMALS.md).</summary>
        public const int DefaultDeerCap = 40;
        /// <summary>One pack is all the forest's game supports.</summary>
        public const int DefaultWolfCap = 7;
        /// <summary>Two coops' worth of birds plus one spring hatch.</summary>
        public const int DefaultChickenCap = 30;
        /// <summary>The farm's pens plus one spring farrowing.</summary>
        public const int DefaultPigCap = 16;
        /// <summary>Stable, not booming (docs/design/ANIMALS.md).</summary>
        public const int DefaultBramblebackCap = 35;

        public string Id { get; }
        public int DeerCap { get; }
        public int WolfCap { get; }
        public int ChickenCap { get; }
        public int PigCap { get; }
        public int BramblebackCap { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of breeding: the last day processed, the last
    /// year bred, and the next birth ordinal (birth IDs must stay unique across
    /// save/load, so the counter is part of the saved state).
    /// </summary>
    public sealed class BreedingState
    {
        public BreedingState(bool initialized = false, long lastBreedingDay = 0,
            long lastBreedingYear = -1, int nextBirthOrdinal = 1)
        {
            IsInitialized = initialized;
            LastBreedingDay = lastBreedingDay;
            LastBreedingYear = lastBreedingYear;
            NextBirthOrdinal = nextBirthOrdinal;
        }

        public bool IsInitialized { get; }
        public long LastBreedingDay { get; internal set; }
        public long LastBreedingYear { get; internal set; }
        public int NextBirthOrdinal { get; internal set; }
    }

    /// <summary>
    /// Spring breeding (agents.breeding, Actions phase): on the first day of spring
    /// each year, hens hatch 6-10 chicks, sows farrow 6-8 piglets, does drop a
    /// fawn each, the wolf pack has 3-4 pups, and brambleback pairs raise 2-3
    /// young (docs/design/ANIMALS.md). New animals are Young with trust 0 (wild)
    /// or 20 (domestic). Populations are then capped at the design maxima; excess
    /// young are removed ("eaten or traded") with an AnimalCulled truth event.
    /// </summary>
    public sealed class BreedingSystem : IWorldSystem
    {
        private static readonly SpeciesId Chicken = new SpeciesId("species_chicken");
        private static readonly SpeciesId Pig = new SpeciesId("species_pig");
        private static readonly SpeciesId Deer = new SpeciesId("species_deer");
        private static readonly SpeciesId Wolf = new SpeciesId("species_wolf");
        private static readonly SpeciesId Brambleback = new SpeciesId("species_brambleback");

        private readonly List<BreedingConfiguration> _configurations;

        public BreedingSystem(IEnumerable<BreedingConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<BreedingConfiguration>();
            foreach (BreedingConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Breeding configurations cannot contain null.",
                        nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "agents.breeding";
        public SimulationPhase Phase => SimulationPhase.Actions;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Breeding.IsInitialized) return;
            BreedingState progress = state.Breeding;
            while (progress.LastBreedingDay < state.Clock.Day)
            {
                progress.LastBreedingDay++;
                MaybeBreed(state, progress.LastBreedingDay);
            }
        }

        private void MaybeBreed(WorldState state, long day)
        {
            var time = new GameTime((day - 1) * 1440);
            if (VillageCalendar.SeasonAt(time) != Season.Spring) return;
            long year = (day - 1) / VillageCalendar.DaysPerYear;
            if (state.Breeding.LastBreedingYear == year) return;
            if (_configurations.Count == 0) return;
            state.Breeding.LastBreedingYear = year;
            // Caps are global to the world: one configuration drives the breeding.
            BreedOnce(state, _configurations[0], time, year);
        }

        private static void BreedOnce(WorldState state, BreedingConfiguration configuration,
            GameTime time, long year)
        {
            int born = HatchChicks(state, time, year);
            RecordBirth(state, time, born, Chicken);
            born = FarrowPiglets(state, time, year);
            RecordBirth(state, time, born, Pig);
            born = DropFawns(state, time, year);
            RecordBirth(state, time, born, Deer);
            born = BirthWolfPups(state, time, year);
            RecordBirth(state, time, born, Wolf);
            born = RaiseBramblebackYoung(state, time, year);
            RecordBirth(state, time, born, Brambleback);

            EnforceCap(state, configuration, Chicken, configuration.ChickenCap, time);
            EnforceCap(state, configuration, Pig, configuration.PigCap, time);
            EnforceCap(state, configuration, Deer, configuration.DeerCap, time);
            EnforceCap(state, configuration, Wolf, configuration.WolfCap, time);
            EnforceCap(state, configuration, Brambleback, configuration.BramblebackCap, time);
        }

        private static void RecordBirth(WorldState state, GameTime time, int born,
            SpeciesId species)
        {
            if (born <= 0) return;
            var first = state.Animals.GetBySpecies(species)[0];
            state.Events.Append(time, first.Location, WorldEventType.AnimalBirth,
                visibility: EventVisibility.Quiet, quantity: born);
        }

        private static AnimalId NextBirthId(BreedingState progress, string prefix, long year)
        {
            string id = "animal_" + prefix + "_b" + year + "_"
                + progress.NextBirthOrdinal.ToString("D3");
            progress.NextBirthOrdinal++;
            return new AnimalId(id);
        }

        private static int HatchChicks(WorldState state, GameTime time, long year)
        {
            int born = 0;
            foreach (AnimalState hen in state.Animals.GetBySpecies(Chicken))
            {
                if (hen.Age != AnimalAge.Adult) continue;
                // "Hens hatch 6-10 chicks each spring if allowed": not every hen broods.
                if (state.Rng.NextInt(4) != 0) continue;
                int clutch = 6 + state.Rng.NextInt(5);
                for (int i = 0; i < clutch; i++)
                {
                    state.Animals.Add(AnimalState.Create(
                        NextBirthId(state.Breeding, "chicken", year), Chicken, hen.Location,
                        20, null, AnimalAge.Young, AnimalState.MaxHealth));
                    born++;
                }
            }
            return born;
        }

        private static int FarrowPiglets(WorldState state, GameTime time, long year)
        {
            int born = 0;
            foreach (AnimalState sow in state.Animals.GetBySpecies(Pig))
            {
                if (sow.Age != AnimalAge.Adult) continue;
                if (sow.Trust <= AnimalState.MinTrust) continue; // wild boars do not farrow here
                int litter = 6 + state.Rng.NextInt(3);
                for (int i = 0; i < litter; i++)
                {
                    state.Animals.Add(AnimalState.Create(
                        NextBirthId(state.Breeding, "pig", year), Pig, sow.Location,
                        20, null, AnimalAge.Young, AnimalState.MaxHealth));
                    born++;
                }
            }
            return born;
        }

        private static int DropFawns(WorldState state, GameTime time, long year)
        {
            int born = 0;
            int index = 0;
            foreach (AnimalState deer in state.Animals.GetBySpecies(Deer))
            {
                if (deer.Age != AnimalAge.Adult) continue;
                // Sex is not tracked; every other adult by ID order counts as a doe.
                if (index % 2 == 0)
                {
                    state.Animals.Add(AnimalState.Create(
                        NextBirthId(state.Breeding, "deer", year), Deer, deer.Location,
                        0, null, AnimalAge.Young, AnimalState.MaxHealth));
                    born++;
                }
                index++;
            }
            return born;
        }

        private static int BirthWolfPups(WorldState state, GameTime time, long year)
        {
            AnimalState den = null;
            foreach (AnimalState wolf in state.Animals.GetBySpecies(Wolf))
                if (wolf.Age == AnimalAge.Adult) { den = wolf; break; }
            if (den == null) return 0;
            int born = 3 + state.Rng.NextInt(2);
            for (int i = 0; i < born; i++)
                state.Animals.Add(AnimalState.Create(
                    NextBirthId(state.Breeding, "wolf", year), Wolf, den.Location,
                    0, null, AnimalAge.Young, AnimalState.MaxHealth));
            return born;
        }

        private static int RaiseBramblebackYoung(WorldState state, GameTime time, long year)
        {
            var adults = new List<AnimalState>();
            foreach (AnimalState animal in state.Animals.GetBySpecies(Brambleback))
                if (animal.Age == AnimalAge.Adult) adults.Add(animal);
            int born = 0;
            for (int pair = 0; pair + 1 < adults.Count; pair += 2)
            {
                int litter = 2 + state.Rng.NextInt(2);
                for (int i = 0; i < litter; i++)
                {
                    state.Animals.Add(AnimalState.Create(
                        NextBirthId(state.Breeding, "brambleback", year), Brambleback,
                        adults[pair].Location, 0, null, AnimalAge.Young,
                        AnimalState.MaxHealth));
                    born++;
                }
            }
            return born;
        }

        private static void EnforceCap(WorldState state, BreedingConfiguration configuration,
            SpeciesId species, int cap, GameTime time)
        {
            IReadOnlyList<AnimalState> animals = state.Animals.GetBySpecies(species);
            int excess = animals.Count - cap;
            if (excess <= 0) return;
            // The young go first ("eaten or traded"), then the oldest by ID order.
            var culls = new List<AnimalState>();
            foreach (AnimalState animal in animals)
                if (animal.Age == AnimalAge.Young) culls.Add(animal);
            foreach (AnimalState animal in animals)
                if (animal.Age == AnimalAge.Adult) culls.Add(animal);
            LocationId location = culls[0].Location;
            for (int i = 0; i < excess; i++)
                state.Animals.Remove(culls[i].Id);
            state.Events.Append(time, location, WorldEventType.AnimalCulled,
                visibility: EventVisibility.Quiet, quantity: excess);
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BreedingConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Breeding configuration IDs must be unique.",
                        "configurations");
        }
    }
}
