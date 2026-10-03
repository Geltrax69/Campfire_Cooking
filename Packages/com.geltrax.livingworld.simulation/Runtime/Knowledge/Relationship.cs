using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Immutable directed feeling of one NPC toward another: trust and affection on
    /// a 0–100 scale plus the reason why. This is world truth, not a belief — what
    /// the NPC actually feels, which P2-02 dynamics may change over time.
    /// </summary>
    public sealed class Relationship
    {
        public Relationship(NpcId from, NpcId to, int trust, int affection, string reason)
        {
            if (!from.IsValid) throw new ArgumentException("A relationship needs a valid source NPC.", nameof(from));
            if (!to.IsValid) throw new ArgumentException("A relationship needs a valid target NPC.", nameof(to));
            if (from == to) throw new ArgumentException("An NPC cannot have a relationship with itself.", nameof(to));
            if (trust < 0 || trust > 100)
                throw new ArgumentOutOfRangeException(nameof(trust), "Trust is 0–100.");
            if (affection < 0 || affection > 100)
                throw new ArgumentOutOfRangeException(nameof(affection), "Affection is 0–100.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A relationship needs a reason.", nameof(reason));

            From = from;
            To = to;
            Trust = trust;
            Affection = affection;
            Reason = reason;
        }

        public NpcId From { get; }
        public NpcId To { get; }
        public int Trust { get; }
        public int Affection { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// Directed trust/affection pairs keyed by (from, to): Mira→Ralf need not equal
    /// Ralf→Mira. Iteration is deterministic in ordinal (from, to) NPC ID order.
    /// NPCs with no recorded pair are strangers: trust 50, affection 50 — the neutral
    /// midpoint of the scale, since a villager neither trusts nor distrusts a stranger.
    /// </summary>
    public sealed class RelationshipRegistry
    {
        public const int StrangerTrust = 50;
        public const int StrangerAffection = 50;

        private readonly SortedDictionary<Pair, Relationship> _pairs =
            new SortedDictionary<Pair, Relationship>();

        public int Count => _pairs.Count;

        /// <summary>Adds a pair or overwrites the existing one in the same direction.</summary>
        public void Set(Relationship relationship)
        {
            if (relationship == null) throw new ArgumentNullException(nameof(relationship));
            _pairs[new Pair(relationship.From, relationship.To)] = relationship;
        }

        public bool TryGet(NpcId from, NpcId to, out Relationship relationship)
        {
            ValidateEndpoints(from, to);
            return _pairs.TryGetValue(new Pair(from, to), out relationship);
        }

        /// <summary>Trust from one NPC toward another; 50 for pairs never recorded (strangers).</summary>
        public int Trust(NpcId from, NpcId to) =>
            TryGet(from, to, out Relationship relationship) ? relationship.Trust : StrangerTrust;

        /// <summary>Affection from one NPC toward another; 50 for pairs never recorded (strangers).</summary>
        public int Affection(NpcId from, NpcId to) =>
            TryGet(from, to, out Relationship relationship) ? relationship.Affection : StrangerAffection;

        /// <summary>All pairs in deterministic ordinal (from, to) order.</summary>
        public IReadOnlyList<Relationship> Query() =>
            new List<Relationship>(_pairs.Values).AsReadOnly();

        /// <summary>Captures every pair for Persistence, in deterministic pair order.</summary>
        internal IReadOnlyList<Relationship> Capture() => Query();

        /// <summary>
        /// Atomically replaces all pairs with a validated snapshot for Persistence.
        /// A rejected snapshot (null, null record, duplicate pair) leaves the
        /// registry unchanged.
        /// </summary>
        internal void Restore(IEnumerable<Relationship> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var staged = new SortedDictionary<Pair, Relationship>();
            foreach (Relationship relationship in snapshot)
            {
                if (relationship == null)
                    throw new ArgumentException("Relationship snapshots cannot contain null.", nameof(snapshot));
                var key = new Pair(relationship.From, relationship.To);
                if (staged.ContainsKey(key))
                    throw new ArgumentException("Duplicate relationship pair in snapshot.", nameof(snapshot));
                staged.Add(key, relationship);
            }
            _pairs.Clear();
            foreach (KeyValuePair<Pair, Relationship> pair in staged) _pairs.Add(pair.Key, pair.Value);
        }

        private static void ValidateEndpoints(NpcId from, NpcId to)
        {
            if (!from.IsValid) throw new ArgumentException("A relationship query needs a valid source NPC.", nameof(from));
            if (!to.IsValid) throw new ArgumentException("A relationship query needs a valid target NPC.", nameof(to));
        }

        private readonly struct Pair : IComparable<Pair>
        {
            public readonly NpcId From;
            public readonly NpcId To;

            public Pair(NpcId from, NpcId to)
            {
                From = from;
                To = to;
            }

            public int CompareTo(Pair other)
            {
                int byFrom = From.CompareTo(other.From);
                return byFrom != 0 ? byFrom : To.CompareTo(other.To);
            }
        }
    }
}
