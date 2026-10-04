using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable caller configuration for one laying coop: where the hens are and whose inventory the eggs land in.</summary>
    public sealed class EggConfiguration
    {
        public EggConfiguration(string id, LocationId coop, Inventory coopInventory,
            EventVisibility visibility)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A configuration needs an ID.", nameof(id));
            if (!coop.IsValid) throw new ArgumentException("A coop location is required.", nameof(coop));
            if (coopInventory == null) throw new ArgumentNullException(nameof(coopInventory));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            Coop = coop;
            CoopInventory = coopInventory;
            Visibility = visibility;
        }

        public string Id { get; }
        public LocationId Coop { get; }
        public Inventory CoopInventory { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of egg production: the last day laying ran.
    /// Starts uninitialized; the system stays quiet until the world-build step
    /// initializes it.
    /// </summary>
    public sealed class EggProductionState
    {
        public EggProductionState(bool initialized = false, long lastLayDay = 0)
        {
            IsInitialized = initialized;
            LastLayDay = lastLayDay;
        }

        public bool IsInitialized { get; }
        public long LastLayDay { get; internal set; }
    }

    /// <summary>
    /// Egg production (economy.egg, Economy phase): hens lay ~0.7 eggs/day in warm
    /// months and ~0.2 in winter (docs/design/ANIMALS.md). Each adult hen at a
    /// configured coop lays with that daily chance from the shared seeded RNG;
    /// the first two adults by ID are the roosters and never lay. The day's eggs
    /// land in the coop's inventory and are recorded as a Produced truth event.
    /// This feeds the village economy (Maren's egg sales).
    /// </summary>
    public sealed class EggProductionSystem : IWorldSystem
    {
        /// <summary>Egg, per Content/items/items.json.</summary>
        public static readonly ItemTypeId Egg = new ItemTypeId("item_egg");
        /// <summary>Daily lay chance per hen in warm months, per hundred.</summary>
        public const int WarmLayChancePerHundred = 70;
        /// <summary>Daily lay chance per hen in winter, per hundred.</summary>
        public const int WinterLayChancePerHundred = 20;
        /// <summary>The design keeps two roosters in the village flock.</summary>
        public const int RoostersPerCoop = 2;

        private static readonly SpeciesId Chicken = new SpeciesId("species_chicken");

        private readonly List<EggConfiguration> _configurations;

        public EggProductionSystem(IEnumerable<EggConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<EggConfiguration>();
            foreach (EggConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Egg configurations cannot contain null.",
                        nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "economy.egg";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.EggProduction.IsInitialized) return;
            EggProductionState progress = state.EggProduction;
            while (progress.LastLayDay < state.Clock.Day)
            {
                progress.LastLayDay++;
                foreach (EggConfiguration configuration in _configurations)
                    LayForDay(state, configuration, progress.LastLayDay);
            }
        }

        private static void LayForDay(WorldState state, EggConfiguration configuration, long day)
        {
            var time = new GameTime((day - 1) * 1440);
            bool winter = VillageCalendar.SeasonAt(time) == Season.Winter;
            int layChance = winter ? WinterLayChancePerHundred : WarmLayChancePerHundred;

            int skippedRoosters = 0;
            int eggs = 0;
            foreach (AnimalState chicken in state.Animals.GetBySpecies(Chicken))
            {
                if (chicken.Location != configuration.Coop) continue;
                if (chicken.Age != AnimalAge.Adult) continue;
                if (skippedRoosters < RoostersPerCoop) { skippedRoosters++; continue; }
                if (state.Rng.NextInt(100) < layChance) eggs++;
            }
            if (eggs <= 0) return;
            configuration.CoopInventory.Add(Egg, eggs);
            state.Events.Append(time, configuration.Coop, WorldEventType.Produced,
                visibility: configuration.Visibility, itemType: Egg, quantity: eggs);
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (EggConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Egg configuration IDs must be unique.",
                        "configurations");
        }
    }
}
