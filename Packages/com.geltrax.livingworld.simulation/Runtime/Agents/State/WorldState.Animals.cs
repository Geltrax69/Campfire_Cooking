using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds animal state ownership to the shared world state.</summary>
    public sealed partial class WorldState
    {
        public AnimalStore Animals { get; } = new AnimalStore();
    }
}
