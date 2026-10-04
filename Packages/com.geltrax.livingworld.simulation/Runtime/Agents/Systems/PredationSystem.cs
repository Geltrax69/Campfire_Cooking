using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Immutable caller configuration for wolf predation.</summary>
    public sealed class PredationConfiguration
    {
        public PredationConfiguration(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A configuration needs an ID.", nameof(id));
            Id = id;
        }

        public string Id { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of predation: the last day hunts ran.
    /// Starts uninitialized; the system stays quiet until the world-build step
    /// initializes it.
    /// </summary>
    public sealed class PredationState
    {
        public PredationState(bool initialized = false, long lastHuntDay = 0)
        {
            IsInitialized = initialized;
            LastHuntDay = lastHuntDay;
        }

        public bool IsInitialized { get; }
        public long LastHuntDay { get; internal set; }
    }

    /// <summary>
    /// Wolves hunt deer (agents.predation, Actions phase): each adult wolf has a
    /// daily chance to kill a deer — 0.3% in warm months, 0.6% in winter — which
    /// takes about 8 deer a year from a six-wolf pack (docs/design/ANIMALS.md).
    /// The pack takes the weakest deer first (lowest health, then lowest ID);
    /// boars are never prey. A kill removes the animal and records a Predation
    /// truth event with no actor (wild kills blame nobody). Winter livestock
    /// losses are the WinterPressureSystem's job, not this one's.
    /// </summary>
    public sealed class PredationSystem : IWorldSystem
    {
        /// <summary>Daily kill chance per adult wolf outside winter, per thousand.</summary>
        public const int WarmKillChancePerThousand = 3;
        /// <summary>Daily kill chance per adult wolf in winter, per thousand.</summary>
        public const int WinterKillChancePerThousand = 6;

        private static readonly SpeciesId Wolf = new SpeciesId("species_wolf");
        private static readonly SpeciesId Deer = new SpeciesId("species_deer");

        private readonly List<PredationConfiguration> _configurations;

        public PredationSystem(IEnumerable<PredationConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<PredationConfiguration>();
            foreach (PredationConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Predation configurations cannot contain null.",
                        nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "agents.predation";
        public SimulationPhase Phase => SimulationPhase.Actions;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Predation.IsInitialized) return;
            PredationState progress = state.Predation;
            while (progress.LastHuntDay < state.Clock.Day)
            {
                progress.LastHuntDay++;
                HuntForDay(state, progress.LastHuntDay);
            }
        }

        private static void HuntForDay(WorldState state, long day)
        {
            var time = new GameTime((day - 1) * 1440);
            bool winter = VillageCalendar.SeasonAt(time) == Season.Winter;
            int killChance = winter ? WinterKillChancePerThousand : WarmKillChancePerThousand;

            foreach (AnimalState wolf in state.Animals.GetBySpecies(Wolf))
            {
                if (wolf.Age != AnimalAge.Adult) continue; // pups do not hunt
                if (state.Rng.NextInt(1000) >= killChance) continue;
                AnimalState victim = WeakestDeer(state);
                if (victim == null) return; // no deer left; the pack goes hungry
                state.Animals.Remove(victim.Id);
                state.Events.Append(time, victim.Location, WorldEventType.Predation,
                    visibility: EventVisibility.Quiet, quantity: 1);
            }
        }

        private static AnimalState WeakestDeer(WorldState state)
        {
            AnimalState weakest = null;
            foreach (AnimalState deer in state.Animals.GetBySpecies(Deer))
            {
                if (weakest == null || deer.Health < weakest.Health)
                    weakest = deer;
                // Ties keep the earlier (lower-ID) deer: the list is in AnimalId order.
            }
            return weakest;
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PredationConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Predation configuration IDs must be unique.",
                        "configurations");
        }
    }
}
