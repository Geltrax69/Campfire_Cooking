using System;
using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds the aging section to the shared world state (P7-01).</summary>
    public sealed partial class WorldState
    {
        public AgingState Aging { get; private set; } = new AgingState();

        internal void RestoreAging(AgingState state) =>
            Aging = state ?? throw new ArgumentNullException(nameof(state));
    }
}
