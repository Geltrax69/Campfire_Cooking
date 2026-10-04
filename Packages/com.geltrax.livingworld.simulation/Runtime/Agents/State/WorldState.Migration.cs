using System;
using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds the migration section to the shared world state (P5-02).</summary>
    public sealed partial class WorldState
    {
        public MigrationState Migration { get; } = new MigrationState();

        /// <summary>Installs validated migration state for Persistence.</summary>
        internal void RestoreMigration(MigrationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Migration.Restore(state.Capture());
        }
    }
}
