using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Chooses conversation company by affection: when an NPC picks whom to talk to
    /// (tavern evenings, rest breaks), each candidate's selection weight is 1 + the
    /// decider's directed affection for them, rolled on the world's seeded RNG. Friends
    /// seek each other out, but it is never a hard script — even a stranger keeps weight
    /// 1, so a lonely NPC still talks to whoever is there. The runtime meeting driver
    /// that calls this is future work; the scenario harness drives meetings today.
    /// </summary>
    public static class ConversationChoice
    {
        /// <summary>
        /// Selection weight for one candidate: 1 + affection (0–100), so every candidate
        /// always has a non-zero chance.
        /// </summary>
        public static int WeightFor(int affection) =>
            1 + Math.Max(0, Math.Min(100, affection));

        /// <summary>
        /// Picks one conversation partner from the candidates by affection-weighted roll.
        /// Candidates are walked in deterministic id order, so the same seed always picks
        /// the same partner. The decider is never chosen, even if listed.
        /// </summary>
        public static NpcId ChoosePartner(WorldState state, NpcId decider, IReadOnlyList<NpcId> candidates)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!decider.IsValid) throw new ArgumentException("A decider needs a valid NPC id.", nameof(decider));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (candidates.Count == 0)
                throw new ArgumentException("At least one conversation candidate is needed.", nameof(candidates));

            var ordered = new List<NpcId>();
            foreach (NpcId candidate in candidates)
            {
                if (!candidate.IsValid) throw new ArgumentException("Candidates need valid NPC ids.", nameof(candidates));
                if (candidate == decider) continue;
                ordered.Add(candidate);
            }
            if (ordered.Count == 0)
                throw new InvalidOperationException("There is no one for the NPC to talk to.");
            ordered.Sort((left, right) => string.Compare(left.Value, right.Value, StringComparison.Ordinal));

            RelationshipRegistry registry = state.Knowledge.Relationships;
            long total = 0;
            foreach (NpcId candidate in ordered)
                total += WeightFor(registry.Affection(decider, candidate));
            long roll = state.Rng.NextInt((int)total);
            foreach (NpcId candidate in ordered)
            {
                roll -= WeightFor(registry.Affection(decider, candidate));
                if (roll < 0) return candidate;
            }
            throw new InvalidOperationException("The affection-weighted roll unexpectedly missed every candidate.");
        }
    }
}
