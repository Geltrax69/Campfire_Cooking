using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Builds the village's initial family structure from approved Content (P7-02):
    /// one household per home location, parent/child links from the "family"
    /// arrays, and two-way partner links from husband/wife entries. Sibling
    /// entries carry no parent/child state and are ignored. NPCs whose Content
    /// names no family simply share their home's household.
    /// </summary>
    public static class FamilySetup
    {
        /// <summary>
        /// Assigns every registered NPC to a household and links the approved
        /// family relations. NPCs are visited in ordinal ID order so the result
        /// is deterministic; children lists therefore stay in ordinal order.
        /// </summary>
        public static void AssignInitialFamilies(WorldState state,
            IReadOnlyDictionary<NpcId, IReadOnlyList<FamilyRelation>> relations)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (relations == null) throw new ArgumentNullException(nameof(relations));

            foreach (NpcState npc in state.Npcs.Npcs)
            {
                var householdId = new HouseholdId("household_" + npc.Definition.Home.Value);
                if (!state.Households.Contains(householdId))
                    state.Households.Register(new Household(householdId, npc.Definition.Home));
                state.Households[householdId].AddMember(npc.Definition.Id);
                npc.SetHousehold(householdId);
            }

            foreach (NpcState npc in state.Npcs.Npcs)
            {
                if (!relations.TryGetValue(npc.Definition.Id, out var links)) continue;
                foreach (FamilyRelation link in links)
                {
                    switch (link.Relation)
                    {
                        case "father":
                            npc.SetParents(null, link.Target);
                            break;
                        case "mother":
                            npc.SetParents(link.Target, null);
                            break;
                        case "son":
                        case "daughter":
                            npc.AddChild(link.Target);
                            break;
                        case "husband":
                        case "wife":
                            npc.SetPartner(link.Target);
                            // Marriage is two-way: repair the reverse link when the
                            // partner is registered but their own entry missed it.
                            if (state.Npcs.Contains(link.Target))
                                state.Npcs[link.Target].SetPartner(npc.Definition.Id);
                            break;
                        // "brother"/"sister": no parent/child state; ignored.
                    }
                }
            }
        }
    }
}
