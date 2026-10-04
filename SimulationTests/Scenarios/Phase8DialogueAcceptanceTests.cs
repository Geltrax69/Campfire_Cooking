using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Scenarios
{
    /// <summary>
    /// Phase 8 acceptance (P8-04): exercises the full dialogue stack —
    /// FactSheetBuilder, TemplatePhrasingEngine, DialogueSession — against a
    /// hand-built village of real Content NPCs with distinct moods, beliefs,
    /// memories and known news. Proves dialogue phrases only NPC knowledge,
    /// never changes state, is deterministic, and survives save/load.
    /// Writes a readable log to Phase8_Acceptance_Log.md.
    /// </summary>
    public sealed class Phase8DialogueAcceptanceTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira_holt");
        private static readonly NpcId Bram = new NpcId("npc_bram_stone");
        private static readonly NpcId Sella = new NpcId("npc_sella_wren");
        private static readonly NpcId Elswith = new NpcId("npc_elswith_alder");

        private static readonly VillageId Millbrook = new VillageId("village_millbrook");
        private static readonly VillageId KingsRest = new VillageId("village_kings_rest");
        private static readonly VillageId Oakhollow = new VillageId("village_oakhollow");

        private static readonly LocationId AppleStall = new LocationId("loc_apple_stall");
        private static readonly LocationId GeneralStore = new LocationId("loc_general_store");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        private static readonly DialogueIntent[] AllIntents =
            (DialogueIntent[])Enum.GetValues(typeof(DialogueIntent));

        private static readonly NpcId[] Villagers = { Mira, Bram, Sella, Elswith };

        private static string ContentRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }

        private static string LogPath(string contentRoot) => Path.Combine(contentRoot,
            "SimulationTests", "Scenarios", "Phase8_Acceptance_Log.md");

        /// <summary>
        /// Builds the village: four Content NPCs with distinct moods, distinct
        /// beliefs about the player, one memory, and three arrived news items.
        /// </summary>
        private static WorldState SetUp(string contentRoot, ulong seed)
        {
            ContentBundle bundle = ContentBundle.Load(contentRoot);
            var state = new WorldState(seed, new GameTime(0));

            Register(state, bundle, Mira, happiness: 85);    // happy
            Register(state, bundle, Bram, happiness: 25);    // low
            Register(state, bundle, Sella, happiness: 50);   // neutral
            Register(state, bundle, Elswith, happiness: 10); // miserable

            state.RestoreVillages(VillageFactory.CreateInitialVillages().Capture());

            // World truth (for the log, not the sheets): the player really did
            // steal 6 apples from Mira's stall. Mira saw it. Bram was told by
            // Sella. Sella was never there and knows nothing about it.
            AddBelief(state, Mira,
                new BeliefClaim(BeliefClaimKind.TheftObserved, AppleStall, Apple,
                    ActorId.Player, quantity: 6),
                confidence: 80, sourceKind: BeliefSourceKind.Seen);

            AddBelief(state, Bram,
                new BeliefClaim(BeliefClaimKind.TheftObserved, AppleStall, Apple,
                    ActorId.Player, quantity: 6),
                confidence: 50, sourceKind: BeliefSourceKind.ToldBy, speaker: Sella);

            // Elswith never met the player but inferred a theft that never
            // happened: dialogue must phrase the belief, not the truth.
            AddBelief(state, Elswith,
                new BeliefClaim(BeliefClaimKind.TheftObserved, GeneralStore, Apple,
                    ActorId.Player, quantity: 3),
                confidence: 40, sourceKind: BeliefSourceKind.Inferred);

            // One genuine memory: the player once gave Mira a gift.
            state.Knowledge.GetMemories(Mira).Remember(
                new BeliefClaim(BeliefClaimKind.GiftFrom, AppleStall,
                    subject: ActorId.Player),
                importance: 60, reinforcedAt: state.Clock);

            // Three news items circulating in Millbrook.
            DeliverNews(state, NewsKind.Festival, KingsRest, Millbrook, severity: 40);
            DeliverNews(state, NewsKind.WolfAttack, Oakhollow, Oakhollow, severity: 70);
            DeliverNews(state, NewsKind.GoodHarvest, KingsRest, KingsRest, severity: 60);

            return state;
        }

        private static void Register(WorldState state, ContentBundle bundle, NpcId id, int happiness)
        {
            var npc = new NpcState(bundle.NpcDefinitions[id], 30, 80, 50);
            npc.RestoreHappiness(happiness);
            state.Npcs.Register(npc);
            state.Knowledge.Register(id);
        }

        private static void AddBelief(WorldState state, NpcId npc, BeliefClaim claim,
            int confidence, BeliefSourceKind sourceKind, NpcId? speaker = null)
        {
            BeliefSource source = sourceKind == BeliefSourceKind.ToldBy && speaker.HasValue
                ? new BeliefSource(sourceKind, speaker.Value)
                : new BeliefSource(sourceKind);
            state.Knowledge.Get(npc).Set(new Belief(claim, source, confidence, state.Clock));
        }

        private static void DeliverNews(WorldState state, NewsKind kind,
            VillageId origin, VillageId about, int severity)
        {
            NewsInTransit transit = state.News.Publish(
                origin, about, kind, dayCreated: 1, severity: severity,
                from: origin, to: Millbrook, arrivalDay: 3);
            state.News.RecordDelivery(transit,
                new VillageNews(transit.News.Id, origin, about, kind, 1, severity));
        }

        /// <summary>
        /// Runs every intent for every villager and returns all utterances,
        /// keyed by NPC then intent.
        /// </summary>
        private static Dictionary<NpcId, Dictionary<DialogueIntent, string>> TalkToEveryone(
            WorldState state, ulong seed)
        {
            var result = new Dictionary<NpcId, Dictionary<DialogueIntent, string>>();
            foreach (NpcId id in Villagers)
            {
                FactSheet sheet = FactSheetBuilder.Build(state, id);
                var session = new DialogueSession(sheet, new TemplatePhrasingEngine(seed));
                var utterances = new Dictionary<DialogueIntent, string>();
                foreach (DialogueIntent intent in AllIntents)
                    utterances[intent] = session.Say(intent);
                result[id] = utterances;
            }
            return result;
        }

        [Test]
        public void VillageDialogueAcceptance()
        {
            string root = ContentRoot();
            WorldState state = SetUp(root, seed: 42);
            const ulong seed = 42;

            string digestBefore = WorldDigest.Compute(state);
            var utterances = TalkToEveryone(state, seed);

            // Every utterance is non-empty.
            foreach (NpcId id in Villagers)
                foreach (DialogueIntent intent in AllIntents)
                    Assert.That(utterances[id][intent], Is.Not.Null.And.Not.Empty,
                        id.Value + " / " + intent + " produced no utterance.");

            // Dialogue reflects knowledge: different moods/beliefs, different speech.
            Assert.That(utterances[Mira][DialogueIntent.Greeting],
                Is.Not.EqualTo(utterances[Elswith][DialogueIntent.Greeting]),
                "Happy Mira and miserable Elswith should not greet identically.");
            Assert.That(utterances[Mira][DialogueIntent.AskAboutPlayer],
                Is.Not.EqualTo(utterances[Sella][DialogueIntent.AskAboutPlayer]),
                "Mira (saw the theft) and Sella (knows nothing) should answer differently.");

            // Knowledge/truth split: Mira phrases what she saw; Sella, who was
            // never there, says nothing about apples at all.
            StringAssert.Contains("apple", utterances[Mira][DialogueIntent.AskAboutPlayer].ToLowerInvariant(),
                "Mira saw the theft; her answer should mention apples.");
            StringAssert.DoesNotContain("apple", utterances[Sella][DialogueIntent.AskAboutPlayer].ToLowerInvariant(),
                "Sella knows nothing about the player; her answer must not mention apples.");

            // Beliefs, not truth: Elswith's inferred gift never happened, but
            // she believes it — dialogue phrases the belief.
            StringAssert.Contains("apple", utterances[Elswith][DialogueIntent.AskAboutPlayer].ToLowerInvariant(),
                "Elswith believes the player gave her apples; dialogue phrases beliefs, not truth.");

            // Hedging follows the source: seen vs told-by phrase differently.
            Assert.That(utterances[Mira][DialogueIntent.AskAboutPlayer],
                Is.Not.EqualTo(utterances[Bram][DialogueIntent.AskAboutPlayer]),
                "Seen and told-by beliefs about the same theft should hedge differently.");

            // Dialogue changed nothing.
            Assert.That(WorldDigest.Compute(state), Is.EqualTo(digestBefore),
                "Talking to the whole village must not change world state.");

            File.WriteAllText(LogPath(root), BuildLog(state, utterances, digestBefore));
        }

        [Test]
        public void DialogueIsDeterministicAcrossSessions()
        {
            string root = ContentRoot();
            WorldState state = SetUp(root, seed: 42);

            var first = TalkToEveryone(state, seed: 42);
            var second = TalkToEveryone(state, seed: 42);

            foreach (NpcId id in Villagers)
                foreach (DialogueIntent intent in AllIntents)
                    Assert.That(second[id][intent], Is.EqualTo(first[id][intent]),
                        "Same seed must phrase identically: " + id.Value + " / " + intent);
        }

        [Test]
        public void DialogueSurvivesSaveLoad()
        {
            string root = ContentRoot();
            WorldState state = SetUp(root, seed: 42);
            const ulong seed = 42;

            var before = TalkToEveryone(state, seed);

            string saved = WorldSaver.Save(state);
            WorldState loaded = WorldLoader.Load(saved, root);

            var after = TalkToEveryone(loaded, seed);

            foreach (NpcId id in Villagers)
                foreach (DialogueIntent intent in AllIntents)
                    Assert.That(after[id][intent], Is.EqualTo(before[id][intent]),
                        "Utterance changed across save/load: " + id.Value + " / " + intent);
        }

        private static string BuildLog(WorldState state,
            Dictionary<NpcId, Dictionary<DialogueIntent, string>> utterances,
            string digestBefore)
        {
            var log = new StringBuilder();
            log.AppendLine("# Phase 8 Acceptance Log — Dialogue across the village");
            log.AppendLine();
            log.AppendLine("Seed 42. Four Content NPCs with distinct moods, beliefs, memories");
            log.AppendLine("and known news. The template phrasing engine (P8-02) stands in for");
            log.AppendLine("the future AI adapter behind `IPhrasingEngine`; no AI services, no Unity.");
            log.AppendLine();
            log.AppendLine("## The setup (world truth vs NPC knowledge)");
            log.AppendLine();
            log.AppendLine("- **World truth:** the player really did steal 6 apples from Mira's stall.");
            log.AppendLine("- **Mira Holt** (apple-stall owner, happy) *saw* the theft.");
            log.AppendLine("- **Bram Stone** (guard, low) was *told* about it by Sella (hedges differently).");
            log.AppendLine("- **Sella Wren** (healer, neutral) was never there and knows nothing about the player.");
            log.AppendLine("- **Elswith Alder** (village elder, miserable) *inferred* the player stole");
            log.AppendLine("  apples from the general store — this never happened. Dialogue phrases beliefs, not truth.");
            log.AppendLine("- Three news items reach Millbrook from elsewhere: a festival at King's Rest,");
            log.AppendLine("  a wolf attack near Oakhollow, a good harvest at King's Rest.");
            log.AppendLine();

            foreach (NpcId id in Villagers)
            {
                FactSheet sheet = FactSheetBuilder.Build(state, id);
                log.AppendLine("## " + sheet.Name + " — " + sheet.Occupation + " (mood: " + sheet.MoodBand + ")");
                log.AppendLine();
                log.AppendLine("*Greeting:* \"" + utterances[id][DialogueIntent.Greeting] + "\"");
                log.AppendLine();
                log.AppendLine("*Asked about the player:* \"" + utterances[id][DialogueIntent.AskAboutPlayer] + "\"");
                log.AppendLine();
                log.AppendLine("*Shares news:* \"" + utterances[id][DialogueIntent.ShareNews] + "\"");
                log.AppendLine();
            }

            log.AppendLine("## Knowledge vs truth, demonstrated");
            log.AppendLine();
            log.AppendLine("- Sella was never at the stall: asked about the player she says");
            log.AppendLine("  \"" + utterances[Sella][DialogueIntent.AskAboutPlayer] + "\" —");
            log.AppendLine("  no mention of apples, though the theft really happened.");
            log.AppendLine("- Elswith's theft never happened, yet asked about the player she says");
            log.AppendLine("  \"" + utterances[Elswith][DialogueIntent.AskAboutPlayer] + "\" —");
            log.AppendLine("  dialogue phrases what she believes, not what is true.");
            log.AppendLine("- Mira saw it, Bram only heard it: their answers hedge differently");
            log.AppendLine("  (\"saw\" vs \"heard\"), though both name the same theft.");
            log.AppendLine();
            log.AppendLine("## Guarantees verified by the tests");
            log.AppendLine();
            log.AppendLine("- `VillageDialogueAcceptance`: every intent non-empty for every NPC;");
            log.AppendLine("  utterances differ with mood/belief; world digest byte-identical");
            log.AppendLine("  before and after all 24 utterances (" + digestBefore.Substring(0, Math.Min(16, digestBefore.Length)) + "…).");
            log.AppendLine("- `DialogueIsDeterministicAcrossSessions`: same seed → byte-identical utterances.");
            log.AppendLine("- `DialogueSurvivesSaveLoad`: save → load → rebuild sheets → identical utterances.");
            log.AppendLine();
            return log.ToString();
        }
    }
}
