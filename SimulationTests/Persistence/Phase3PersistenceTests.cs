using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Persistence
{
    /// <summary>
    /// Proves every Phase 3 state section survives the version 3 save format:
    /// skill stores (player + NPCs), NPC happiness, tavern popularity and
    /// ingredient demand — plus version compatibility (v2 and v1 documents load
    /// with Phase 3 defaults) and corruption rejection.
    /// </summary>
    public sealed class Phase3PersistenceTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira_holt");
        private static readonly NpcId Bessa = new NpcId("npc_bessa_marlowe");
        private static readonly SkillId Cooking = new SkillId("skill_cooking");
        private static readonly SkillId Taming = new SkillId("skill_taming");
        private static readonly ItemTypeId Fish = new ItemTypeId("item_fish");
        private static readonly ItemTypeId Firewood = new ItemTypeId("item_firewood");

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }

        /// <summary>World with non-default values in every Phase 3 section.</summary>
        private static WorldState Phase3RichWorld(ContentBundle bundle)
        {
            var state = new WorldState(4242, new GameTime(9000));
            foreach (NpcId npc in new[] { Mira, Bessa })
            {
                state.Npcs.Register(NpcState.Restore(bundle.NpcDefinitions[npc],
                    NeedState.FromSixtieths(3000, 3000, 3000), false, null));
                state.Knowledge.Register(npc);
            }

            NpcState mira = state.Npcs[Mira];
            mira.RestoreHappiness(63);
            mira.Skills.Restore(new[] { SkillState.Restore(Cooking, 2, 120, 5, 4) });

            NpcState bessa = state.Npcs[Bessa];
            bessa.RestoreHappiness(71);
            bessa.Skills.Restore(new[]
            {
                SkillState.Restore(Cooking, 3, 310, 0, 5),
                SkillState.Restore(Taming, 1, 25, 0, 3),
            });

            state.PlayerSkills.Restore(new[] { SkillState.Restore(Cooking, 1, 14, 2, 6) });

            var popularity = new TavernPopularityState(64);
            popularity.LastSkilledCookDay = 6;
            popularity.LastDecayDay = 6;
            state.RestoreTavernPopularity(popularity);

            var demand = new IngredientDemandState();
            demand.LastDecayDay = 6;
            demand.AddDemand(Fish, 7);
            demand.AddDemand(Firewood, 3);
            state.RestoreIngredientDemand(demand);

            return state;
        }

        [Test]
        public void Phase3StateSaveLoadRoundTrip()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase3RichWorld(bundle);

            WorldState loaded = WorldLoader.Load(WorldSaver.Save(state), root);

            Assert.That(loaded.Npcs[Mira].Happiness, Is.EqualTo(63));
            Assert.That(loaded.Npcs[Bessa].Happiness, Is.EqualTo(71));

            SkillState miraCooking = loaded.Npcs[Mira].Skills.Get(Cooking);
            Assert.That(miraCooking.Level, Is.EqualTo(2));
            Assert.That(miraCooking.PracticePoints, Is.EqualTo(120));
            Assert.That(miraCooking.DailyPoints, Is.EqualTo(5));
            Assert.That(miraCooking.LastPracticeDay, Is.EqualTo(4));

            SkillState bessaCooking = loaded.Npcs[Bessa].Skills.Get(Cooking);
            Assert.That(bessaCooking.Level, Is.EqualTo(3));
            Assert.That(bessaCooking.PracticePoints, Is.EqualTo(310));
            Assert.That(loaded.Npcs[Bessa].Skills.Get(Taming).Level, Is.EqualTo(1));

            SkillState playerCooking = loaded.PlayerSkills.Get(Cooking);
            Assert.That(playerCooking.Level, Is.EqualTo(1));
            Assert.That(playerCooking.PracticePoints, Is.EqualTo(14));
            Assert.That(playerCooking.DailyPoints, Is.EqualTo(2));
            Assert.That(playerCooking.LastPracticeDay, Is.EqualTo(6));

            Assert.That(loaded.TavernPopularity.Popularity, Is.EqualTo(64));
            Assert.That(loaded.TavernPopularity.LastSkilledCookDay, Is.EqualTo(6));
            Assert.That(loaded.TavernPopularity.LastDecayDay, Is.EqualTo(6));

            Assert.That(loaded.IngredientDemand.Demand(Fish), Is.EqualTo(7));
            Assert.That(loaded.IngredientDemand.Demand(Firewood), Is.EqualTo(3));
            Assert.That(loaded.IngredientDemand.LastDecayDay, Is.EqualTo(6));
        }

        [Test]
        public void Phase3SaveLoadSaveIsByteIdentical()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase3RichWorld(bundle);
            string first = WorldSaver.Save(state);

            WorldState loaded = WorldLoader.Load(first, root);
            string second = WorldSaver.Save(loaded);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(WorldDigest.Compute(loaded), Is.EqualTo(WorldDigest.Compute(state)));
        }

        [Test]
        public void EmptyPhase3StateRoundTrips()
        {
            // A fresh world's Phase 3 sections are empty/default: the save stays
            // small and the load restores the same defaults.
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            var state = new WorldState(99, new GameTime(0));

            WorldState loaded = WorldLoader.Load(WorldSaver.Save(state), root);

            Assert.That(loaded.PlayerSkills.Capture().Count, Is.EqualTo(0));
            Assert.That(loaded.TavernPopularity.Popularity,
                Is.EqualTo(TavernPopularityState.Baseline));
            Assert.That(loaded.TavernPopularity.LastSkilledCookDay, Is.EqualTo(-1));
            Assert.That(loaded.IngredientDemand.Demand(Fish), Is.EqualTo(0));
        }

        [Test]
        public void Version2DocumentLoadsWithPhase3Defaults()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase3RichWorld(bundle);
            string v2 = StripToVersion2(WorldSaver.Save(state));

            WorldState loaded = WorldLoader.Load(v2, root);

            // Phase 3 sections default: neutral moods, empty skill stores, baseline
            // popularity, no demand. Pre-existing sections still load.
            foreach (NpcState npc in loaded.Npcs.Npcs)
            {
                Assert.That(npc.Happiness, Is.EqualTo(NpcState.NeutralHappiness),
                    "v2 has no happiness: " + npc.Definition.Id.Value + " is neutral.");
                Assert.That(npc.Skills.Capture().Count, Is.EqualTo(0));
            }
            Assert.That(loaded.PlayerSkills.Capture().Count, Is.EqualTo(0));
            Assert.That(loaded.TavernPopularity.Popularity,
                Is.EqualTo(TavernPopularityState.Baseline));
            Assert.That(loaded.TavernPopularity.LastSkilledCookDay, Is.EqualTo(-1));
            Assert.That(loaded.TavernPopularity.LastDecayDay, Is.EqualTo(-1));
            Assert.That(loaded.IngredientDemand.Demand(Fish), Is.EqualTo(0));
            Assert.That(loaded.IngredientDemand.LastDecayDay, Is.EqualTo(-1));
        }

        [Test]
        public void Version1DocumentLoadsWithPhase3Defaults()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase3RichWorld(bundle);
            string v3 = WorldSaver.Save(state);
            string v1 = StripToVersion1(StripToVersion2(v3));

            WorldState loaded = WorldLoader.Load(v1, root);

            Assert.That(loaded.Npcs[Mira].Happiness, Is.EqualTo(NpcState.NeutralHappiness));
            Assert.That(loaded.PlayerSkills.Capture().Count, Is.EqualTo(0));
            Assert.That(loaded.TavernPopularity.Popularity,
                Is.EqualTo(TavernPopularityState.Baseline));
            Assert.That(loaded.IngredientDemand.Demand(Fish), Is.EqualTo(0));
        }

        [Test]
        public void FutureVersionIsRejected()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase3RichWorld(bundle);
            string json = WorldSaver.Save(state);
            string v5 = json.Replace("\"formatVersion\": 4", "\"formatVersion\": 5");

            Assert.Throws<LoadException>(() => WorldLoader.Load(v5, root));
        }

        [Test]
        public void CorruptPhase3IsRejected()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase3RichWorld(bundle);
            string json = WorldSaver.Save(state);

            // Happiness above 100.
            string badHappiness = json.Replace("\"happiness\": 63", "\"happiness\": 101");
            Assert.Throws<LoadException>(() => WorldLoader.Load(badHappiness, root),
                "Happiness is 0-100.");

            // Unknown skill ID.
            string badSkill = json.Replace("\"skill\": \"skill_taming\"",
                "\"skill\": \"skill_flying\"");
            Assert.Throws<LoadException>(() => WorldLoader.Load(badSkill, root),
                "Unknown skills are rejected.");

            // Duplicate skill within one actor: Bessa's taming entry rewritten as
            // a second cooking entry.
            string dupSkill = json.Replace("\"skill_taming\"", "\"skill_cooking\"");
            Assert.Throws<LoadException>(() => WorldLoader.Load(dupSkill, root),
                "A duplicated skill is rejected.");

            // Points granting a higher level than stated: 800 points grant level 4,
            // but the entry claims level 3.
            string badPoints = json.Replace("\"practicePoints\": 310", "\"practicePoints\": 800");
            Assert.Throws<LoadException>(() => WorldLoader.Load(badPoints, root),
                "Level below what the points grant is rejected.");

            // Skills for an NPC that is not in the save's npcs array.
            string straySkills = json.Replace("\"npc:npc_bessa_marlowe\"",
                "\"npc:npc_tilda_bray\"");
            Assert.Throws<LoadException>(() => WorldLoader.Load(straySkills, root),
                "Skills for an unsaved NPC are rejected.");
        }

        /// <summary>
        /// Rewrites a version 3 document as version 2: drops the Phase 3 sections
        /// and the per-NPC happiness, mirroring StripToVersion1 in
        /// Phase2PersistenceTests.
        /// </summary>
        private static string StripToVersion2(string v3)
        {
            using (JsonDocument document = JsonDocument.Parse(v3))
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        foreach (JsonProperty property in document.RootElement.EnumerateObject())
                        {
                            switch (property.Name)
                            {
                                case "formatVersion":
                                    writer.WriteNumber("formatVersion", 2);
                                    break;
                                case "skills":
                                case "tavernPopularity":
                                case "ingredientDemand":
                                    break;
                                case "npcs":
                                    writer.WritePropertyName("npcs");
                                    writer.WriteStartArray();
                                    foreach (JsonElement npc in property.Value.EnumerateArray())
                                    {
                                        writer.WriteStartObject();
                                        foreach (JsonProperty npcProperty in npc.EnumerateObject())
                                        {
                                            if (npcProperty.Name == "happiness") continue;
                                            npcProperty.WriteTo(writer);
                                        }
                                        writer.WriteEndObject();
                                    }
                                    writer.WriteEndArray();
                                    break;
                                default:
                                    property.WriteTo(writer);
                                    break;
                            }
                        }
                        writer.WriteEndObject();
                    }
                    stream.Position = 0;
                    using (var reader = new StreamReader(stream))
                        return reader.ReadToEnd();
                }
            }
        }

        /// <summary>Rewrites a version 2 document as version 1 (drops Phase 2 sections).</summary>
        private static string StripToVersion1(string v2)
        {
            using (JsonDocument document = JsonDocument.Parse(v2))
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        foreach (JsonProperty property in document.RootElement.EnumerateObject())
                        {
                            switch (property.Name)
                            {
                                case "formatVersion":
                                    writer.WriteNumber("formatVersion", 1);
                                    break;
                                case "relationships":
                                case "attributedMemories":
                                case "smithy":
                                case "merchantSchedule":
                                case "travelerSpend":
                                case "wolfBounty":
                                case "villageFund":
                                case "harvest":
                                case "tax":
                                case "communityFund":
                                case "economyBaseline":
                                case "spoilage":
                                case "debtLedger":
                                    break;
                                case "shops":
                                case "belongings":
                                    // v1 has no per-lot ages: drop the "lots" arrays.
                                    writer.WritePropertyName(property.Name);
                                    writer.WriteStartArray();
                                    foreach (JsonElement entry in property.Value.EnumerateArray())
                                    {
                                        writer.WriteStartObject();
                                        foreach (JsonProperty entryProperty in entry.EnumerateObject())
                                        {
                                            if (entryProperty.Name == "lots") continue;
                                            entryProperty.WriteTo(writer);
                                        }
                                        writer.WriteEndObject();
                                    }
                                    writer.WriteEndArray();
                                    break;
                                default:
                                    property.WriteTo(writer);
                                    break;
                            }
                        }
                        writer.WriteEndObject();
                    }
                    stream.Position = 0;
                    using (var reader = new StreamReader(stream))
                        return reader.ReadToEnd();
                }
            }
        }
    }
}
