using System;
using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds the family section to the shared world state (P7-02).</summary>
    public sealed partial class WorldState
    {
        public HouseholdRegistry Households { get; } = new HouseholdRegistry();
        public FamilyState Family { get; private set; } = new FamilyState();

        internal void RestoreFamily(FamilyState state) =>
            Family = state ?? throw new ArgumentNullException(nameof(state));
    }
}
