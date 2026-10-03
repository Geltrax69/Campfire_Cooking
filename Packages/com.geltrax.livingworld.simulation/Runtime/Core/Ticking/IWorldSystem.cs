namespace LivingWorld.Simulation.Core
{
    /// <summary>Updates shared world state once per minute using captured registration keys.</summary>
    public interface IWorldSystem
    {
        string Id { get; }
        SimulationPhase Phase { get; }
        void Tick(WorldState state);
    }
}
