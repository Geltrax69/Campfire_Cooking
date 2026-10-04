using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Owns NPC state with unique IDs and deterministic ordinal iteration.</summary>
    public sealed class NpcRegistry
    {
        private readonly SortedList<NpcId, NpcState> _npcs = new SortedList<NpcId, NpcState>();
        private readonly IReadOnlyList<NpcState> _readOnlyNpcs;

        public NpcRegistry()
        {
            _readOnlyNpcs = new ReadOnlyCollection<NpcState>(_npcs.Values);
        }

        public int Count => _npcs.Count;
        public IReadOnlyList<NpcState> Npcs => _readOnlyNpcs;

        public NpcState this[NpcId id]
        {
            get
            {
                ValidateId(id);
                if (!_npcs.TryGetValue(id, out var npc)) throw new ArgumentException("Unknown NPC ID.", nameof(id));
                return npc;
            }
        }

        public void Register(NpcState npc)
        {
            if (npc == null) throw new ArgumentNullException(nameof(npc));
            var id = npc.Definition.Id;
            if (_npcs.ContainsKey(id)) throw new ArgumentException("NPC ID is already registered.", nameof(npc));
            _npcs.Add(id, npc);
        }

        /// <summary>
        /// Removes an NPC from the village (P5-02 out-migration). The population stat
        /// counts registered NPCs, so departure shrinks the village. Callers own the
        /// consequences for beliefs, memories and relationships that referenced the NPC.
        /// </summary>
        public void Unregister(NpcId id)
        {
            ValidateId(id);
            if (!_npcs.Remove(id)) throw new ArgumentException("Unknown NPC ID.", nameof(id));
        }

        private static void ValidateId(NpcId id)
        {
            if (!id.IsValid) throw new ArgumentException("NPC ID must be valid.", nameof(id));
        }
    }
}
