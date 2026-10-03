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
        private long _dynamicsCursor;
        private long _recallCursor;
        private long _lastDynamicsDay;
        private readonly SortedDictionary<RelationshipPairKey, RelationshipBaseline> _relationshipBaselines =
            new SortedDictionary<RelationshipPairKey, RelationshipBaseline>();
        private readonly SortedDictionary<AttributedMemoryKey, AttributedMemory> _attributedMemories =
            new SortedDictionary<AttributedMemoryKey, AttributedMemory>();

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

        /// <summary>Last world event ID relationship dynamics has processed; zero before any event.</summary>
        internal long DynamicsCursor
        {
            get => _dynamicsCursor;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _dynamicsCursor = value;
            }
        }

        /// <summary>Captures the relationship-dynamics event cursor for Persistence.</summary>
        internal long CaptureDynamicsCursor() => DynamicsCursor;

        /// <summary>
        /// Restores the relationship-dynamics event cursor. A negative value is rejected and
        /// the current cursor is left unchanged.
        /// </summary>
        internal void RestoreDynamicsCursor(long cursor)
        {
            if (cursor < 0) throw new ArgumentOutOfRangeException(nameof(cursor));
            DynamicsCursor = cursor;
        }

        /// <summary>Last world event ID memory recall has processed; zero before any event.</summary>
        internal long RecallCursor
        {
            get => _recallCursor;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _recallCursor = value;
            }
        }

        /// <summary>Captures the memory-recall event cursor for Persistence.</summary>
        internal long CaptureRecallCursor() => RecallCursor;

        /// <summary>
        /// Restores the memory-recall event cursor. A negative value is rejected and the
        /// current cursor is left unchanged.
        /// </summary>
        internal void RestoreRecallCursor(long cursor)
        {
            if (cursor < 0) throw new ArgumentOutOfRangeException(nameof(cursor));
            RecallCursor = cursor;
        }

        /// <summary>
        /// Records the recall accounting for an attributed interaction memory. First capture
        /// wins: reinforcing the same memory later refreshes its decay clock but never moves
        /// its own bound, mirroring how relationship baselines work.
        /// </summary>
        internal void RecordAttributedMemory(NpcId owner, BeliefClaim claim, int trustDelta,
            int affectionDelta)
        {
            var key = new AttributedMemoryKey(owner, claim);
            if (_attributedMemories.ContainsKey(key)) return;
            _attributedMemories.Add(key, new AttributedMemory(owner, claim, trustDelta, affectionDelta));
        }

        /// <summary>Reads the recall accounting for one attributed memory, if recorded.</summary>
        internal bool TryGetAttributedMemory(NpcId owner, BeliefClaim claim,
            out AttributedMemory record) =>
            _attributedMemories.TryGetValue(new AttributedMemoryKey(owner, claim), out record);

        /// <summary>
        /// Adds actual recall-driven movement to a memory's accounting. Only the recall
        /// system calls this, after checking the per-axis bound; unknown memories are ignored.
        /// </summary>
        internal void AddRecallMovement(NpcId owner, BeliefClaim claim, int trustMoved,
            int affectionMoved)
        {
            if (_attributedMemories.TryGetValue(new AttributedMemoryKey(owner, claim),
                out AttributedMemory record))
                record.AddRecall(trustMoved, affectionMoved);
        }

        /// <summary>Captures every attributed memory record for Persistence, in deterministic order.</summary>
        internal IReadOnlyList<AttributedMemory> CaptureAttributedMemories() =>
            new List<AttributedMemory>(_attributedMemories.Values).AsReadOnly();

        /// <summary>
        /// Atomically replaces all attributed memory records with a validated snapshot for
        /// Persistence. A rejected snapshot (null, null record, duplicate memory) leaves the
        /// current records unchanged.
        /// </summary>
        internal void RestoreAttributedMemories(IEnumerable<AttributedMemory> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var staged = new SortedDictionary<AttributedMemoryKey, AttributedMemory>();
            foreach (AttributedMemory record in snapshot)
            {
                if (record == null)
                    throw new ArgumentException("Attributed memories cannot contain null.", nameof(snapshot));
                var key = new AttributedMemoryKey(record.Owner, record.Claim);
                if (staged.ContainsKey(key))
                    throw new ArgumentException("Duplicate attributed memory in snapshot.", nameof(snapshot));
                staged.Add(key, record);
            }
            _attributedMemories.Clear();
            foreach (KeyValuePair<AttributedMemoryKey, AttributedMemory> pair in staged)
                _attributedMemories.Add(pair.Key, pair.Value);
        }

        /// <summary>
        /// The in-game day (GameTime.Day) relationship decay last ran; zero before the first run.
        /// </summary>
        internal long LastDynamicsDay
        {
            get => _lastDynamicsDay;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _lastDynamicsDay = value;
            }
        }

        /// <summary>Captures the last relationship-decay day for Persistence.</summary>
        internal long CaptureDynamicsDay() => LastDynamicsDay;

        /// <summary>
        /// Restores the last relationship-decay day. A negative value is rejected and the
        /// current day is left unchanged.
        /// </summary>
        internal void RestoreDynamicsDay(long day)
        {
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            LastDynamicsDay = day;
        }

        /// <summary>
        /// Records the Content (or first-seen) trust/affection/reason for a pair as the target
        /// relationship decay drifts back toward. First capture wins: later calls for the same
        /// pair are ignored so shifts can never move their own baseline.
        /// </summary>
        internal void CaptureRelationshipBaseline(NpcId from, NpcId to, int trust, int affection, string reason)
        {
            var key = new RelationshipPairKey(from, to);
            if (_relationshipBaselines.ContainsKey(key)) return;
            _relationshipBaselines.Add(key, new RelationshipBaseline(from, to, trust, affection, reason));
        }

        /// <summary>Reads the recorded baseline for a pair, if dynamics has captured one.</summary>
        internal bool TryGetRelationshipBaseline(NpcId from, NpcId to, out RelationshipBaseline baseline) =>
            _relationshipBaselines.TryGetValue(new RelationshipPairKey(from, to), out baseline);

        /// <summary>Captures every relationship baseline for Persistence, in deterministic pair order.</summary>
        internal IReadOnlyList<RelationshipBaseline> CaptureRelationshipBaselines() =>
            new List<RelationshipBaseline>(_relationshipBaselines.Values).AsReadOnly();

        /// <summary>
        /// Atomically replaces all relationship baselines with a validated snapshot for
        /// Persistence. A rejected snapshot (null, null record, duplicate pair) leaves the
        /// current baselines unchanged.
        /// </summary>
        internal void RestoreRelationshipBaselines(IEnumerable<RelationshipBaseline> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var staged = new SortedDictionary<RelationshipPairKey, RelationshipBaseline>();
            foreach (RelationshipBaseline baseline in snapshot)
            {
                if (baseline == null)
                    throw new ArgumentException("Relationship baselines cannot contain null.", nameof(snapshot));
                var key = new RelationshipPairKey(baseline.From, baseline.To);
                if (staged.ContainsKey(key))
                    throw new ArgumentException("Duplicate relationship baseline in snapshot.", nameof(snapshot));
                staged.Add(key, baseline);
            }
            _relationshipBaselines.Clear();
            foreach (KeyValuePair<RelationshipPairKey, RelationshipBaseline> pair in staged)
                _relationshipBaselines.Add(pair.Key, pair.Value);
        }

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

        private readonly struct RelationshipPairKey : IComparable<RelationshipPairKey>
        {
            public readonly NpcId From;
            public readonly NpcId To;

            public RelationshipPairKey(NpcId from, NpcId to)
            {
                // RelationshipBaseline's constructor already validated these.
                From = from;
                To = to;
            }

            public int CompareTo(RelationshipPairKey other)
            {
                int byFrom = From.CompareTo(other.From);
                return byFrom != 0 ? byFrom : To.CompareTo(other.To);
            }
        }

        private readonly struct AttributedMemoryKey : IComparable<AttributedMemoryKey>
        {
            public readonly NpcId Owner;
            public readonly BeliefClaim Claim;

            public AttributedMemoryKey(NpcId owner, BeliefClaim claim)
            {
                // AttributedMemory's constructor already validated these.
                Owner = owner;
                Claim = claim;
            }

            public int CompareTo(AttributedMemoryKey other)
            {
                int byOwner = Owner.CompareTo(other.Owner);
                return byOwner != 0 ? byOwner : Claim.CompareTo(other.Claim);
            }
        }
    }
}
