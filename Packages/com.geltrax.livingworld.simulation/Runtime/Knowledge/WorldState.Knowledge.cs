using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Exposes the world-owned collection of NPC knowledge.</summary>
    public sealed partial class WorldState
    {
        public KnowledgeState Knowledge { get; } = new KnowledgeState();
    }
}
