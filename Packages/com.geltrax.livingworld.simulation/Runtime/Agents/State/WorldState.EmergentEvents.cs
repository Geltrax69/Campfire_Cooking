using System;
using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds the emergent events section to the shared world state (P5-03).</summary>
    public sealed partial class WorldState
    {
        public EmergentEventState EmergentEvents { get; private set; } = new EmergentEventState();

        /// <summary>Installs validated emergent event state for Persistence.</summary>
        internal void RestoreEmergentEvents(EmergentEventState state)
        {
            EmergentEvents = state ?? throw new ArgumentNullException(nameof(state));
        }
    }
}
