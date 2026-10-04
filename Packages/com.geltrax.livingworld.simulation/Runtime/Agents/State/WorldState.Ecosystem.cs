using System;
using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds ecosystem-dynamics state ownership (P4-02) to the shared world state.</summary>
    public sealed partial class WorldState
    {
        public PredationState Predation { get; private set; } = new PredationState();
        public BreedingState Breeding { get; private set; } = new BreedingState();
        public WinterPressureState WinterPressure { get; private set; } = new WinterPressureState();

        internal void RestorePredation(PredationState state) =>
            Predation = state ?? throw new ArgumentNullException(nameof(state));

        internal void RestoreBreeding(BreedingState state) =>
            Breeding = state ?? throw new ArgumentNullException(nameof(state));

        internal void RestoreWinterPressure(WinterPressureState state) =>
            WinterPressure = state ?? throw new ArgumentNullException(nameof(state));
    }
}
