using System;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds the computed town-stats section to the shared world state (P5-01).</summary>
    public sealed partial class WorldState
    {
        public TownStatsState TownStats { get; private set; } = new TownStatsState();

        /// <summary>Installs validated town stats for Persistence.</summary>
        internal void RestoreTownStats(TownStatsState state)
        {
            TownStats = state ?? throw new ArgumentNullException(nameof(state));
        }
    }
}
