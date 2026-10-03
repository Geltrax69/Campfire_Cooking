using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable view of one actor's personal inventory and wallet.</summary>
    public sealed class NpcBelongingsEntry
    {
        public NpcBelongingsEntry(ActorId owner, Inventory inventory, Wallet wallet)
        {
            if (!owner.IsValid) throw new ArgumentException("Belongings need a valid owner.", nameof(owner));
            Owner = owner;
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public ActorId Owner { get; }
        public Inventory Inventory { get; }
        public Wallet Wallet { get; }
    }

    /// <summary>
    /// Owns every actor's personal inventory and wallet, keyed by actor in deterministic
    /// order (the player first, then NPCs by ordinal ID). A dedicated registry keeps the
    /// Agents-owned NpcState free of Economy types and covers the player, who is an actor
    /// but not an NPC.
    /// </summary>
    public sealed class NpcBelongings
    {
        private readonly SortedDictionary<ActorId, NpcBelongingsEntry> _entries;

        public NpcBelongings()
        {
            _entries = new SortedDictionary<ActorId, NpcBelongingsEntry>(new ActorOrder());
        }

        /// <summary>All registered belongings in deterministic actor order.</summary>
        public IReadOnlyList<NpcBelongingsEntry> Entries =>
            new List<NpcBelongingsEntry>(_entries.Values).AsReadOnly();

        /// <summary>Registers one actor's inventory and wallet exactly once.</summary>
        public void Register(ActorId owner, Inventory inventory, Wallet wallet)
        {
            var entry = new NpcBelongingsEntry(owner, inventory, wallet);
            if (_entries.ContainsKey(owner))
                throw new ArgumentException("Belongings are already registered for this owner.", nameof(owner));
            _entries.Add(owner, entry);
        }

        public NpcBelongingsEntry this[ActorId owner]
        {
            get
            {
                if (!owner.IsValid)
                    throw new ArgumentException("A belongings lookup needs a valid owner.", nameof(owner));
                if (!_entries.TryGetValue(owner, out NpcBelongingsEntry entry))
                    throw new ArgumentException("No belongings are registered for this owner.", nameof(owner));
                return entry;
            }
        }

        public bool TryGet(ActorId owner, out NpcBelongingsEntry entry)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A belongings lookup needs a valid owner.", nameof(owner));
            return _entries.TryGetValue(owner, out entry);
        }

        /// <summary>
        /// Replaces every entry with a validated batch for Persistence. Everything is validated
        /// before anything changes; a failed restore leaves the registry untouched.
        /// </summary>
        internal void Restore(IEnumerable<NpcBelongingsEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var validated = new SortedDictionary<ActorId, NpcBelongingsEntry>(new ActorOrder());
            foreach (NpcBelongingsEntry entry in entries)
            {
                if (entry == null)
                    throw new ArgumentException("Restored belongings cannot contain null.", nameof(entries));
                if (validated.ContainsKey(entry.Owner))
                    throw new ArgumentException("Restored belongings owners must be unique.", nameof(entries));
                validated.Add(entry.Owner, entry);
            }
            _entries.Clear();
            foreach (var pair in validated) _entries.Add(pair.Key, pair.Value);
        }

        private sealed class ActorOrder : IComparer<ActorId>
        {
            public int Compare(ActorId left, ActorId right)
            {
                if (left.IsPlayer != right.IsPlayer) return left.IsPlayer ? -1 : 1;
                if (left.IsPlayer) return 0;
                return left.Npc.Value.CompareTo(right.Npc.Value);
            }
        }
    }
}
