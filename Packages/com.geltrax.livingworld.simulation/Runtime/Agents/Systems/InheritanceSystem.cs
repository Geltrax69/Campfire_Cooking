using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Caller-owned, restorable record of inheritance: the deceased NPCs whose
    /// estates have already been distributed. Estates are distributed once;
    /// without this set a second tick would find the (now empty) wallet and
    /// record spurious zero-transfer events.
    /// </summary>
    public sealed class InheritanceState
    {
        private readonly HashSet<NpcId> _distributed = new HashSet<NpcId>();

        public InheritanceState(bool initialized = false,
            IEnumerable<NpcId> distributed = null)
        {
            IsInitialized = initialized;
            if (distributed == null) return;
            foreach (NpcId id in distributed)
            {
                if (!id.IsValid) throw new ArgumentException("Distributed IDs must be valid.", nameof(distributed));
                _distributed.Add(id);
            }
        }

        public bool IsInitialized { get; }

        public IReadOnlyCollection<NpcId> Distributed => _distributed;

        internal bool IsDistributed(NpcId id) => _distributed.Contains(id);

        internal void MarkDistributed(NpcId id)
        {
            if (!id.IsValid) throw new ArgumentException("A distributed ID must be valid.", nameof(id));
            _distributed.Add(id);
        }

        /// <summary>Captures the distributed set for save/load.</summary>
        public InheritanceState Capture() =>
            new InheritanceState(IsInitialized, _distributed);
    }

    /// <summary>
    /// Estate distribution (agents.inheritance, Actions phase): once per tick,
    /// every deceased NPC whose estate has not been distributed yet gets
    /// <see cref="Inheritance.Distribute"/> called. Runs after the aging
    /// system in registration order so same-tick deaths are settled promptly.
    /// Deterministic: heir priority is fully ordered, no RNG.
    /// </summary>
    public sealed class InheritanceSystem : IWorldSystem
    {
        public string Id => "agents.inheritance";
        public SimulationPhase Phase => SimulationPhase.Actions;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Inheritance.IsInitialized) return;
            // Snapshot the deceased list: Distribute appends events but never
            // mutates the registry, so iterating the live list is safe.
            var deceased = new List<NpcState>();
            foreach (NpcState npc in state.Npcs.Npcs)
                if (npc.IsDeceased && !state.Inheritance.IsDistributed(npc.Definition.Id))
                    deceased.Add(npc);
            // Deterministic ordinal order.
            deceased.Sort((left, right) =>
                string.CompareOrdinal(left.Definition.Id.Value, right.Definition.Id.Value));
            foreach (NpcState npc in deceased)
            {
                Inheritance.Distribute(state, npc.Definition.Id);
                state.Inheritance.MarkDistributed(npc.Definition.Id);
            }
        }
    }
}
