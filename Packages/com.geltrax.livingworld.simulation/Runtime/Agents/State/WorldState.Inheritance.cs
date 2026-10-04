using System;
using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds the inheritance section to the shared world state (P7-03).</summary>
    public sealed partial class WorldState
    {
        public InheritanceState Inheritance { get; private set; } = new InheritanceState();

        internal void RestoreInheritance(InheritanceState state) =>
            Inheritance = state ?? throw new ArgumentNullException(nameof(state));
    }
}
