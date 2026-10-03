using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Advances awake NPC needs once during the Needs phase.</summary>
    public sealed class NeedsSystem : IWorldSystem
    {
        public string Id => "agents.needs";
        public SimulationPhase Phase => SimulationPhase.Needs;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (var npc in state.Npcs.Npcs) npc.AdvanceNeedsOneMinute();
        }
    }
}
