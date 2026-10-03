using System;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Holds one NPC's mutable simulation state separately from its definition.</summary>
    public sealed class NpcState
    {
        public NpcState(NpcDefinition definition, int initialHunger, int initialEnergy, int initialSocial)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var needs = new NeedState(initialHunger, initialEnergy, initialSocial);
            Definition = definition;
            Needs = needs;
        }

        public NpcDefinition Definition { get; }
        public NeedState Needs { get; }
        public bool IsSleeping { get; set; }

        public void AdvanceNeedsOneMinute()
        {
            if (!IsSleeping) Needs.AdvanceOneAwakeMinute(Definition.NeedRates);
        }
    }
}
