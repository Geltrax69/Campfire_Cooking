using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds the player's skill store to the shared world state.</summary>
    public sealed partial class WorldState
    {
        /// <summary>
        /// The player's skills. NPC skills live on their <see cref="NpcState"/>; the
        /// player has no NpcState, so their store lives here. Save/load wiring for
        /// both is a Persistence follow-up (see SkillStore.Capture/Restore).
        /// </summary>
        public SkillStore PlayerSkills { get; } = new SkillStore();
    }
}
