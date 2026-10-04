using System;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Owns world-level town-development state for Phase 5: granary, housing, shared infrastructure, night watch and outward reputation.</summary>
    public sealed partial class WorldState
    {
        public GranaryState Granary { get; private set; } = new GranaryState();
        public HousingState Housing { get; private set; } = new HousingState();
        public TownInfrastructureState Infrastructure { get; private set; } = new TownInfrastructureState();
        public NightWatchState NightWatch { get; private set; } = new NightWatchState();
        public OutwardReputationState Reputation { get; private set; } = new OutwardReputationState();

        /// <summary>Installs validated granary state for Persistence.</summary>
        internal void RestoreGranary(GranaryState state)
        {
            Granary = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Installs validated housing state for Persistence.</summary>
        internal void RestoreHousing(HousingState state)
        {
            Housing = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Installs validated town-infrastructure state for Persistence.</summary>
        internal void RestoreInfrastructure(TownInfrastructureState state)
        {
            Infrastructure = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Installs validated night-watch state for Persistence.</summary>
        internal void RestoreNightWatch(NightWatchState state)
        {
            NightWatch = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Installs validated outward-reputation state for Persistence.</summary>
        internal void RestoreReputation(OutwardReputationState state)
        {
            Reputation = state ?? throw new ArgumentNullException(nameof(state));
        }
    }
}
