namespace LivingWorld.Simulation.Core
{
    /// <summary>Defines the fixed system order after the central clock advances.</summary>
    public enum SimulationPhase
    {
        Commands,
        Needs,
        Decisions,
        Actions,
        Perception,
        Social,
        Economy,
        Memory
    }
}
