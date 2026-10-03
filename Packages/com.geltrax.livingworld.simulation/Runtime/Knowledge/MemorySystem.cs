using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Applies approved memory decay during the fixed Memory phase.</summary>
    public sealed class MemorySystem : IWorldSystem
    {
        private readonly MemoryRules _rules;

        public MemorySystem(MemoryRules rules) => _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        public string Id => "knowledge.memory-decay";
        public SimulationPhase Phase => SimulationPhase.Memory;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (var store in state.Knowledge.MemoryStores) store.Decay(state.Clock, _rules);
        }
    }
}
