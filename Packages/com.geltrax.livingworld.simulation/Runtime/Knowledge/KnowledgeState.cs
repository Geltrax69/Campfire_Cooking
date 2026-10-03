using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Registers independent NPC belief stores in deterministic NPC order.</summary>
    public sealed class KnowledgeState
    {
        private readonly SortedDictionary<NpcId, BeliefStore> _stores = new SortedDictionary<NpcId, BeliefStore>();

        public IReadOnlyList<BeliefStore> Stores => new List<BeliefStore>(_stores.Values).AsReadOnly();

        public BeliefStore Register(NpcId npc)
        {
            if (!npc.IsValid) throw new ArgumentException("Knowledge requires a valid NPC ID.", nameof(npc));
            if (_stores.ContainsKey(npc)) throw new InvalidOperationException("The NPC already has a belief store.");
            var store = new BeliefStore(npc);
            _stores.Add(npc, store);
            return store;
        }

        public BeliefStore Get(NpcId npc)
        {
            if (!npc.IsValid) throw new ArgumentException("Knowledge requires a valid NPC ID.", nameof(npc));
            return _stores[npc];
        }

        public bool TryGet(NpcId npc, out BeliefStore store)
        {
            if (!npc.IsValid) throw new ArgumentException("Knowledge requires a valid NPC ID.", nameof(npc));
            return _stores.TryGetValue(npc, out store);
        }

        public IReadOnlyList<WorldEventId> RetainedEventIds()
        {
            var retained = new SortedSet<WorldEventId>();
            foreach (var store in _stores.Values)
                foreach (var belief in store.Query())
                    if (belief.Source.OriginEventId.HasValue)
                        retained.Add(belief.Source.OriginEventId.Value);
            return new List<WorldEventId>(retained).AsReadOnly();
        }
    }
}
