using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// One approved family link from Content/npcs/npcs.json ("father", "mother",
    /// "son", "daughter", "husband", "wife", "brother", "sister"). Sibling links
    /// carry no parent/child state and are ignored by FamilySetup.
    /// </summary>
    public sealed class FamilyRelation
    {
        public FamilyRelation(string relation, NpcId target)
        {
            if (string.IsNullOrWhiteSpace(relation))
                throw new ArgumentException("A relation kind is required.", nameof(relation));
            if (!target.IsValid) throw new ArgumentException("A target NPC ID is required.", nameof(target));
            Relation = relation;
            Target = target;
        }

        public string Relation { get; }
        public NpcId Target { get; }
    }
}
