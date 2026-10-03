using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Immutable snapshot of what one NPC felt toward another before dynamics moved it:
    /// the Content-loaded (or first-seen) trust, affection and reason. Relationship decay
    /// drifts pairs back toward these values; P2-02 dynamics capture them lazily on the
    /// first shift so the Content values are never lost.
    /// </summary>
    public sealed class RelationshipBaseline
    {
        public RelationshipBaseline(NpcId from, NpcId to, int trust, int affection, string reason)
        {
            if (!from.IsValid) throw new ArgumentException("A baseline needs a valid source NPC.", nameof(from));
            if (!to.IsValid) throw new ArgumentException("A baseline needs a valid target NPC.", nameof(to));
            if (from == to) throw new ArgumentException("An NPC cannot have a baseline with itself.", nameof(to));
            if (trust < 0 || trust > 100)
                throw new ArgumentOutOfRangeException(nameof(trust), "Trust is 0–100.");
            if (affection < 0 || affection > 100)
                throw new ArgumentOutOfRangeException(nameof(affection), "Affection is 0–100.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A baseline needs a reason.", nameof(reason));

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
}
