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
        private bool _relationshipsInitialized;

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

        /// <summary>
        /// The player's standing with each group, or null until <see cref="InitializeReputation"/>
        /// installs it. Reputation lives here because it is knowledge-adjacent: the groups'
        /// opinion of the player, kept with the rest of what the world knows.
        /// </summary>
        public ReputationState Reputation { get; private set; }

        /// <summary>
        /// Installs player reputation standings exactly once; a second call is rejected.
        /// </summary>
        public void InitializeReputation(IEnumerable<ReputationStanding> standings)
        {
            if (Reputation != null) throw new InvalidOperationException("Reputation is already initialized.");
            Reputation = new ReputationState(standings);
        }

        /// <summary>
        /// Directed trust/affection between NPC pairs (world truth). It lives here, not in
        /// Core, because relationships are social knowledge: the substrate the rumor and
        /// reputation systems read, installed alongside the rest of what the world knows.
        /// Installed once from Content at world build; P2-02 dynamics mutate it via Set.
        /// </summary>
        public RelationshipRegistry Relationships { get; } = new RelationshipRegistry();

        /// <summary>
        /// Installs the content relationships exactly once; every endpoint must already
        /// be registered. A rejected batch installs nothing; a second call is rejected.
        /// </summary>
        public void InitializeRelationships(IEnumerable<Relationship> relationships)
        {
            if (relationships == null) throw new ArgumentNullException(nameof(relationships));
            if (_relationshipsInitialized)
                throw new InvalidOperationException("Relationships are already initialized.");
            var staged = new List<Relationship>();
            foreach (Relationship relationship in relationships)
            {
                if (relationship == null)
                    throw new ArgumentException("Relationships cannot contain null.", nameof(relationships));
                if (!_stores.ContainsKey(relationship.From))
                    throw new InvalidOperationException("Unknown NPC '" + relationship.From.Value +
                        "' in relationships.");
                if (!_stores.ContainsKey(relationship.To))
                    throw new InvalidOperationException("Unknown NPC '" + relationship.To.Value +
                        "' in relationships.");
                staged.Add(relationship);
            }
            Relationships.Restore(staged);
            _relationshipsInitialized = true;
        }

        /// <summary>Captures the relationship pairs for Persistence, in deterministic pair order.</summary>
        internal IReadOnlyList<Relationship> CaptureRelationships() => Relationships.Capture();

        /// <summary>
        /// Replaces relationship pairs with a validated snapshot for Persistence; a rejected
        /// snapshot leaves the current pairs unchanged. This is the restore path, not
        /// initialization: the world build still installs content relationships first.
        /// </summary>
        internal void RestoreRelationships(IEnumerable<Relationship> snapshot) =>
            Relationships.Restore(snapshot);

        /// <summary>Replaces reputation standings with a validated snapshot for Persistence.</summary>
        internal void RestoreReputation(ReputationState state)
        {
            Reputation = state ?? throw new ArgumentNullException(nameof(state));
        }

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
