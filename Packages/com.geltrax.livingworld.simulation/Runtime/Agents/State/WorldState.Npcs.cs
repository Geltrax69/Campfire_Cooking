using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds deterministic NPC state ownership to the shared world state.</summary>
    public sealed partial class WorldState
    {
        public NpcRegistry Npcs { get; } = new NpcRegistry();
    }
}
