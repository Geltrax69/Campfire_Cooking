using System;
using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds the village registry (level-of-detail villages) to the shared world state (P6-01).</summary>
    public sealed partial class WorldState
    {
        public VillageRegistry Villages { get; } = new VillageRegistry();

        /// <summary>Installs a validated village snapshot for Persistence.</summary>
        internal void RestoreVillages(VillageRegistrySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Villages.Restore(snapshot);
        }
    }
}
