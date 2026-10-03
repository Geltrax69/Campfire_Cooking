using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Registers independent NPC belief and memory stores in deterministic NPC order.</summary>
    public sealed class KnowledgeState
    {
        private readonly SortedDictionary<NpcId, BeliefStore> _stores = new SortedDictionary<NpcId, BeliefStore>();
        private readonly SortedDictionary<NpcId, MemoryStore> _memoryStores = new SortedDictionary<NpcId, MemoryStore>();

        private long _perceptionCursor;

        /// <summary>Last world event ID perception has processed; zero before any event.</summary>
        internal long PerceptionCursor
        {
            get => _perceptionCursor;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _perceptionCursor = value;
            }
        }

        /// <summary>Captures the perception event cursor for Persistence.</summary>
        internal long CapturePerceptionCursor() => PerceptionCursor;

        /// <summary>
        /// Restores the perception event cursor. A negative value is rejected and the
        /// current cursor is left unchanged.
        /// </summary>
        internal void RestorePerceptionCursor(long cursor)
        {
            if (cursor < 0) throw new ArgumentOutOfRangeException(nameof(cursor));
            PerceptionCursor = cursor;
        }

        public IReadOnlyList<BeliefStore> Stores => new List<BeliefStore>(_stores.Values).AsReadOnly();
        public IReadOnlyList<MemoryStore> MemoryStores => new List<MemoryStore>(_memoryStores.Values).AsReadOnly();

        public BeliefStore Register(NpcId npc)
        {
            if (!npc.IsValid) throw new ArgumentException("Knowledge requires a valid NPC ID.", nameof(npc));
            if (_stores.ContainsKey(npc)) throw new InvalidOperationException("The NPC already has a belief store.");
            var store = new BeliefStore(npc);
            _stores.Add(npc, store);
            _memoryStores.Add(npc, new MemoryStore(npc));
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

        public MemoryStore GetMemories(NpcId npc)
        {
            if (!npc.IsValid) throw new ArgumentException("Knowledge requires a valid NPC ID.", nameof(npc));
            return _memoryStores[npc];
        }

        public IReadOnlyList<WorldEventId> RetainedEventIds()
        {
            var retained = new SortedSet<WorldEventId>();
            foreach (var store in _stores.Values)
                foreach (var belief in store.Query())
                    if (belief.Source.OriginEventId.HasValue)
                        retained.Add(belief.Source.OriginEventId.Value);
            foreach (var store in _memoryStores.Values)
                foreach (var memory in store.Query())
                    if (memory.OriginEventId.HasValue)
                        retained.Add(memory.OriginEventId.Value);
            return new List<WorldEventId>(retained).AsReadOnly();
        }
    }
}
