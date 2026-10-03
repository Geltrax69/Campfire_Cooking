namespace LivingWorld.Simulation.Core
{
    /// <summary>An immutable input description executed only by the simulation.</summary>
    /// <remarks>
    /// Implementations hold concrete command data and apply it to the supplied state.
    /// Use readonly inputs, not captured delegates or outside state; future persistence
    /// will serialize that data. Test helpers may record results externally.
    /// </remarks>
    public interface IWorldCommand
    {
        void Execute(WorldState state);
    }
}
