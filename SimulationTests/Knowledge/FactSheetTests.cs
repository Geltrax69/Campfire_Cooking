using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>
    /// Proves the dialogue fact sheet (P8-01) contains only what the NPC knows:
    /// identity, mood, beliefs and memories about the player, known news and
    /// household — never world truth the NPC has no access to.
    /// </summary>
    public sealed class FactSheetTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Lida = new NpcId("npc_lida");
        private static readonly LocationId Shop = new LocationId("loc_shop");
        private static readonly LocationId Home = new LocationId("loc_home");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly VillageId Millbrook = new VillageId("village_millbrook");
        private static readonly VillageId KingsRest = new VillageId("village_kings_rest");

        private static NpcState MakeNpc(string id, string name, string occupation, int age)
        {
            var definition = new NpcDefinition(new NpcId(id), name, age, "woman", occupation,
                Home, Home, 0,
                new Dictionary<string, int>(),
                new NeedRates(6, 4, 4),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            return new NpcState(definition, 30, 80, 50);
        }

        private static WorldState WorldWith(params NpcState[] npcs)
        {
            var state = new WorldState(1, new GameTime(0));
            foreach (NpcState npc in npcs)
            {
                state.Npcs.Register(npc);
                state.Knowledge.Register(npc.Definition.Id);
            }
            return state;
        }

        private static void AddBelief(WorldState state, NpcId npc, BeliefClaim claim,
            int confidence, BeliefSourceKind sourceKind)
        {
            BeliefSource source = sourceKind == BeliefSourceKind.ToldBy
                ? new BeliefSource(sourceKind, Lida)
                : new BeliefSource(sourceKind);
            state.Knowledge.Get(npc).Set(new Belief(claim, source, confidence, state.Clock));
        }

        private static void DeliverNews(WorldState state, NewsKind kind, int severity)
        {
            state.RestoreVillages(VillageFactory.CreateInitialVillages().Capture());
            NewsInTransit transit = state.News.Publish(
                KingsRest, Millbrook, kind, dayCreated: 1, severity: severity,
                from: KingsRest, to: Millbrook, arrivalDay: 3);
            state.News.RecordDelivery(transit,
                new VillageNews(transit.News.Id, KingsRest, Millbrook, kind, 1, severity));
        }

        [Test]
        public void FactSheetContainsIdentityAndMood()
        {
            WorldState state = WorldWith(MakeNpc("npc_mira", "Mira", "shopkeeper", 34));

            FactSheet sheet = FactSheetBuilder.Build(state, Mira);

            Assert.That(sheet.NpcId, Is.EqualTo(Mira));
            Assert.That(sheet.Name, Is.EqualTo("Mira"));
            Assert.That(sheet.Occupation, Is.EqualTo("shopkeeper"));
            Assert.That(sheet.Age, Is.EqualTo(34));
            Assert.That(sheet.LifeStage, Is.EqualTo(LifeStage.Adult));
            Assert.That(sheet.Mood, Is.EqualTo(NpcState.NeutralHappiness));
            Assert.That(sheet.MoodBand, Is.EqualTo("neutral"));
        }

        [Test]
        public void MoodBandsCoverTheWholeScale()
        {
            Assert.That(FactSheet.MoodBandFor(0), Is.EqualTo("miserable"));
            Assert.That(FactSheet.MoodBandFor(19), Is.EqualTo("miserable"));
            Assert.That(FactSheet.MoodBandFor(20), Is.EqualTo("low"));
            Assert.That(FactSheet.MoodBandFor(39), Is.EqualTo("low"));
            Assert.That(FactSheet.MoodBandFor(50), Is.EqualTo("neutral"));
            Assert.That(FactSheet.MoodBandFor(60), Is.EqualTo("content"));
            Assert.That(FactSheet.MoodBandFor(79), Is.EqualTo("content"));
            Assert.That(FactSheet.MoodBandFor(80), Is.EqualTo("happy"));
            Assert.That(FactSheet.MoodBandFor(100), Is.EqualTo("happy"));
        }

        [Test]
        public void FactSheetIncludesBeliefsAboutPlayer()
        {
            WorldState state = WorldWith(MakeNpc("npc_mira", "Mira", "shopkeeper", 34));
            var claim = new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple,
                ActorId.Player, quantity: 6);
            AddBelief(state, Mira, claim, 80, BeliefSourceKind.Seen);
            // A belief about someone else must not leak into the player section.
            AddBelief(state, Mira,
                new BeliefClaim(BeliefClaimKind.Presence, Shop, subject: ActorId.ForNpc(Lida)),
                60, BeliefSourceKind.Seen);

            FactSheet sheet = FactSheetBuilder.Build(state, Mira);

            Assert.That(sheet.BeliefsAboutPlayer.Count, Is.EqualTo(1));
            FactSheetBelief belief = sheet.BeliefsAboutPlayer[0];
            Assert.That(belief.Kind, Is.EqualTo(BeliefClaimKind.TheftObserved));
            Assert.That(belief.Location, Is.EqualTo(Shop));
            Assert.That(belief.ItemType, Is.EqualTo(Apple));
            Assert.That(belief.Quantity, Is.EqualTo(6));
            Assert.That(belief.Confidence, Is.EqualTo(80));
            Assert.That(belief.SourceKind, Is.EqualTo(BeliefSourceKind.Seen));
        }

        [Test]
        public void FactSheetIncludesMemoriesAboutPlayer()
        {
            WorldState state = WorldWith(MakeNpc("npc_mira", "Mira", "shopkeeper", 34));
            state.Knowledge.GetMemories(Mira).Remember(
                new BeliefClaim(BeliefClaimKind.WrongedBy, Shop, subject: ActorId.Player),
                70, state.Clock);
            state.Knowledge.GetMemories(Mira).Remember(
                new BeliefClaim(BeliefClaimKind.GiftFrom, Home, subject: ActorId.ForNpc(Lida)),
                40, state.Clock);

            FactSheet sheet = FactSheetBuilder.Build(state, Mira);

            Assert.That(sheet.MemoriesAboutPlayer.Count, Is.EqualTo(1));
            FactSheetMemory memory = sheet.MemoriesAboutPlayer[0];
            Assert.That(memory.Kind, Is.EqualTo(BeliefClaimKind.WrongedBy));
            Assert.That(memory.Location, Is.EqualTo(Shop));
            Assert.That(memory.Importance, Is.EqualTo(70));
        }

        [Test]
        public void FactSheetIncludesKnownNews()
        {
            WorldState state = WorldWith(MakeNpc("npc_mira", "Mira", "shopkeeper", 34));
            DeliverNews(state, NewsKind.Festival, 40);

            FactSheet sheet = FactSheetBuilder.Build(state, Mira);

            Assert.That(sheet.KnownNews.Count, Is.EqualTo(1));
            FactSheetNews news = sheet.KnownNews[0];
            Assert.That(news.Kind, Is.EqualTo(NewsKind.Festival));
            Assert.That(news.Severity, Is.EqualTo(40));
            Assert.That(news.IsGoodNews, Is.True);
        }

        [Test]
        public void FactSheetExcludesUnknownTruth()
        {
            WorldState state = WorldWith(MakeNpc("npc_mira", "Mira", "shopkeeper", 34));
            // World truth: a theft happened at the shop. Mira never perceived it,
            // was never told, and holds no belief or memory about it.
            state.Events.Append(state.Clock, Shop, WorldEventType.Theft, quantity: 6);

            FactSheet sheet = FactSheetBuilder.Build(state, Mira);

            Assert.That(sheet.BeliefsAboutPlayer, Is.Empty);
            Assert.That(sheet.MemoriesAboutPlayer, Is.Empty);
            Assert.That(sheet.KnownNews, Is.Empty);
        }

        [Test]
        public void FactSheetIncludesFalseBelief()
        {
            WorldState state = WorldWith(MakeNpc("npc_mira", "Mira", "shopkeeper", 34));
            // No theft ever happened (no world event), but Mira believes the
            // player stole 6 apples. Dialogue phrases beliefs, not truth.
            AddBelief(state, Mira,
                new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple,
                    ActorId.Player, quantity: 6),
                90, BeliefSourceKind.Inferred);

            FactSheet sheet = FactSheetBuilder.Build(state, Mira);

            Assert.That(sheet.BeliefsAboutPlayer.Count, Is.EqualTo(1));
            Assert.That(sheet.BeliefsAboutPlayer[0].Quantity, Is.EqualTo(6));
            Assert.That(sheet.BeliefsAboutPlayer[0].Confidence, Is.EqualTo(90));
        }

        [Test]
        public void FactSheetBuildIsDeterministic()
        {
            WorldState state = WorldWith(
                MakeNpc("npc_mira", "Mira", "shopkeeper", 34),
                MakeNpc("npc_lida", "Lida", "baker", 41));
            AddBelief(state, Mira,
                new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple,
                    ActorId.Player, quantity: 6),
                80, BeliefSourceKind.Seen);
            state.Knowledge.GetMemories(Mira).Remember(
                new BeliefClaim(BeliefClaimKind.WrongedBy, Shop, subject: ActorId.Player),
                70, state.Clock);
            DeliverNews(state, NewsKind.Festival, 40);

            FactSheet first = FactSheetBuilder.Build(state, Mira);
            FactSheet second = FactSheetBuilder.Build(state, Mira);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void FactSheetBuildDoesNotMutateState()
        {
            WorldState state = WorldWith(
                MakeNpc("npc_mira", "Mira", "shopkeeper", 34),
                MakeNpc("npc_lida", "Lida", "baker", 41));
            AddBelief(state, Mira,
                new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple,
                    ActorId.Player, quantity: 6),
                80, BeliefSourceKind.Seen);
            DeliverNews(state, NewsKind.Festival, 40);

            string before = WorldDigest.Compute(state);
            foreach (NpcState npc in state.Npcs.Npcs)
                FactSheetBuilder.Build(state, npc.Definition.Id);
            string after = WorldDigest.Compute(state);

            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void FactSheetDegradesGracefullyWithoutKnowledgeStores()
        {
            var state = new WorldState(1, new GameTime(0));
            NpcState mira = MakeNpc("npc_mira", "Mira", "shopkeeper", 34);
            state.Npcs.Register(mira);
            // No Knowledge.Register call: the builder must not throw.

            FactSheet sheet = FactSheetBuilder.Build(state, mira.Definition.Id);

            Assert.That(sheet.Name, Is.EqualTo("Mira"));
            Assert.That(sheet.BeliefsAboutPlayer, Is.Empty);
            Assert.That(sheet.MemoriesAboutPlayer, Is.Empty);
            Assert.That(sheet.KnownNews, Is.Empty);
        }

        [Test]
        public void FactSheetIncludesHouseholdMembers()
        {
            WorldState state = WorldWith(
                MakeNpc("npc_mira", "Mira", "shopkeeper", 34),
                MakeNpc("npc_lida", "Lida", "baker", 41));
            var householdId = new HouseholdId("household_home");
            var household = new Household(householdId, Home);
            household.AddMember(Mira);
            household.AddMember(Lida);
            state.Households.Register(household);
            state.Npcs[Mira].SetHousehold(householdId);
            state.Npcs[Lida].SetHousehold(householdId);

            FactSheet sheet = FactSheetBuilder.Build(state, Mira);

            Assert.That(sheet.HouseholdMembers.Count, Is.EqualTo(1));
            Assert.That(sheet.HouseholdMembers[0].NpcId, Is.EqualTo(Lida));
            Assert.That(sheet.HouseholdMembers[0].Name, Is.EqualTo("Lida"));
        }
    }
}
