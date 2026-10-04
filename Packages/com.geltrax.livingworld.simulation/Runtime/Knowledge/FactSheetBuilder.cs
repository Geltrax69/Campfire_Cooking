using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Builds dialogue fact sheets (P8-01): everything dialogue may phrase about
    /// one NPC, drawn only from knowledge stores (beliefs, memories, arrived
    /// news, mood) and never from world truth the NPC has no access to. A pure
    /// function: same state and NPC always yield the same sheet, and building
    /// never mutates the world. Missing data degrades gracefully (empty lists,
    /// neutral mood, placeholder identity).
    /// </summary>
    public static class FactSheetBuilder
    {
        /// <summary>How many player-related memories the sheet keeps, most recent first.</summary>
        public const int MaxMemoriesAboutPlayer = 5;

        /// <summary>How many arrived news items the sheet keeps, most recent first.</summary>
        public const int MaxKnownNews = 3;

        public static FactSheet Build(WorldState state, NpcId npc)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!npc.IsValid) throw new ArgumentException("A fact sheet needs a valid NPC ID.", nameof(npc));

            string name = npc.Value;
            string occupation = "unknown";
            int age = 0;
            LifeStage lifeStage = LifeStage.Child;
            int mood = NpcState.NeutralHappiness;
            NpcState npcState = null;

            if (state.Npcs.Contains(npc))
            {
                npcState = state.Npcs[npc];
                name = npcState.Definition.Name;
                occupation = npcState.Definition.Occupation;
                age = npcState.Age;
                lifeStage = npcState.LifeStage;
                mood = npcState.Happiness;
            }

            IReadOnlyList<FactSheetBelief> beliefs = ReadBeliefsAboutPlayer(state, npc);
            IReadOnlyList<FactSheetMemory> memories = ReadMemoriesAboutPlayer(state, npc);
            IReadOnlyList<FactSheetNews> news = ReadKnownNews(state);
            IReadOnlyList<FactSheetHouseholdMember> household = ReadHousehold(state, npcState, npc);

            return new FactSheet(npc, name, occupation, age, lifeStage, mood,
                beliefs, memories, news, household);
        }

        private static IReadOnlyList<FactSheetBelief> ReadBeliefsAboutPlayer(WorldState state, NpcId npc)
        {
            var result = new List<FactSheetBelief>();
            if (!state.Knowledge.TryGet(npc, out BeliefStore store))
                return result.AsReadOnly();

            // Query returns beliefs in deterministic claim order.
            foreach (Belief belief in store.Query(subject: ActorId.Player))
            {
                result.Add(new FactSheetBelief(
                    belief.Claim.Kind,
                    belief.Claim.Location,
                    belief.Claim.ItemType,
                    belief.Claim.Quantity,
                    belief.Confidence,
                    belief.Source.Kind));
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<FactSheetMemory> ReadMemoriesAboutPlayer(WorldState state, NpcId npc)
        {
            var result = new List<FactSheetMemory>();
            if (!state.Knowledge.TryGet(npc, out _))
                return result.AsReadOnly();

            var aboutPlayer = new List<Memory>();
            foreach (Memory memory in state.Knowledge.GetMemories(npc).Query())
                if (memory.Claim.Subject.HasValue && memory.Claim.Subject.Value == ActorId.Player)
                    aboutPlayer.Add(memory);

            // Most recent first; claim order breaks ties deterministically.
            aboutPlayer.Sort((a, b) =>
            {
                int byTime = b.LastReinforcedAt.CompareTo(a.LastReinforcedAt);
                return byTime != 0 ? byTime : a.Claim.CompareTo(b.Claim);
            });

            int take = Math.Min(aboutPlayer.Count, MaxMemoriesAboutPlayer);
            for (int i = 0; i < take; i++)
            {
                Memory memory = aboutPlayer[i];
                result.Add(new FactSheetMemory(
                    memory.Claim.Kind,
                    memory.Claim.Location,
                    memory.Claim.ItemType,
                    memory.Claim.Quantity,
                    memory.Importance));
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<FactSheetNews> ReadKnownNews(WorldState state)
        {
            var result = new List<FactSheetNews>();
            // Individual NPCs live in the full-LOD village; abstract villages have
            // no individual residents. What arrived there is what goes around town.
            IReadOnlyList<AbstractVillageState> full = state.Villages.GetByLod(VillageLod.Full);
            if (full.Count == 0)
                return result.AsReadOnly();

            IReadOnlyList<ArrivedNews> arrived = state.News.GetArrivedFor(full[0].Id);
            int start = Math.Max(0, arrived.Count - MaxKnownNews);
            for (int i = arrived.Count - 1; i >= start; i--)
            {
                VillageNews news = arrived[i].News;
                result.Add(new FactSheetNews(
                    news.Kind,
                    VillageName(state, news.Origin),
                    VillageName(state, news.About),
                    news.Severity,
                    NewsKindInfo.Valence(news.Kind) == NewsValence.Good));
            }
            return result.AsReadOnly();
        }

        private static string VillageName(WorldState state, VillageId village)
        {
            try
            {
                return state.Villages[village].Name;
            }
            catch (ArgumentException)
            {
                return village.Value;
            }
        }

        private static IReadOnlyList<FactSheetHouseholdMember> ReadHousehold(
            WorldState state, NpcState npcState, NpcId npc)
        {
            var result = new List<FactSheetHouseholdMember>();
            if (npcState == null || !npcState.HouseholdId.HasValue)
                return result.AsReadOnly();
            HouseholdId householdId = npcState.HouseholdId.Value;
            if (!state.Households.Contains(householdId))
                return result.AsReadOnly();

            var members = new List<NpcId>();
            foreach (NpcId member in state.Households[householdId].Members)
                if (member != npc)
                    members.Add(member);
            members.Sort();

            foreach (NpcId member in members)
            {
                string memberName = state.Npcs.Contains(member)
                    ? state.Npcs[member].Definition.Name
                    : member.Value;
                result.Add(new FactSheetHouseholdMember(member, memberName));
            }
            return result.AsReadOnly();
        }
    }
}
