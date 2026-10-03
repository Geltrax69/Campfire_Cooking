using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Advances awake NPC needs once during the Needs phase. Sleeping NPCs are
    /// exempt: the approved need rates assume a 16-hour waking day (ITEMS.md: an
    /// adult needs ~96 hunger/day), so accruing hunger around the clock runs hot.
    /// IsSleeping follows the NPC's own schedule until the intention driver exists.
    /// </summary>
    public sealed class NeedsSystem : IWorldSystem
    {
        public string Id => "agents.needs";
        public SimulationPhase Phase => SimulationPhase.Needs;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (var npc in state.Npcs.Npcs)
            {
                ScheduleEntry entry = npc.Definition.Schedule.At(state.Clock);
                npc.IsSleeping = entry != null && entry.Kind == ActivityKind.Sleep;
                npc.AdvanceNeedsOneMinute();
            }
        }
    }
}
