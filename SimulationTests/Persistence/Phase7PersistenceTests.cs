using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Persistence
{
    /// <summary>
    /// Proves every Phase 7 state section survives the version 7 save format:
    /// NPC age/death/family fields (including born-NPC definitions), the aging,
    /// family and inheritance cursors, and the household registry — plus version
    /// compatibility (v6 and older documents load with generation defaults) and
    /// corruption rejection.
    /// </summary>
    public sealed class Phase7PersistenceTests
    {
        private static readonly NpcId Elswith = new NpcId("npc_elswith_alder");
        private static readonly NpcId Corvin = new NpcId("npc_corvin_alder");
        private static readonly NpcId Maren = new NpcId("npc_maren_alder");
        private static readonly NpcId Piotr = new NpcId("npc_piotr_alder");
        private static readonly NpcId Bessa = new NpcId("npc_bessa_marlowe");
        private static readonly NpcId Born = new NpcId("npc_born_0");
        private static readonly HouseholdId FarmHousehold = new HouseholdId("household_loc_farm");
        private static readonly HouseholdId TavernHousehold = new HouseholdId("household_loc_tavern");
        private static readonly HouseholdId ElderHousehold = new HouseholdId("household_loc_home_elder");

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }

        /// <summary>World with non-default values in every Phase 7 section.</summary>
        private static WorldState Phase7RichWorld(ContentBundle bundle)
        {
            var state = new WorldState(777, new GameTime(700 * 1440));

            // Elswith: 71, deceased, named Bessa her heir.
            NpcState elswith = RegisterContentNpc(state, bundle, Elswith);
            elswith.MarkDeceased();
            elswith.SetDesignatedHeir(Bessa);
            elswith.SetHousehold(ElderHousehold);

            // Corvin + Maren: partners at the farm; Piotr and a born baby are their children.
            NpcState corvin = RegisterContentNpc(state, bundle, Corvin);
            NpcState maren = RegisterContentNpc(state, bundle, Maren);
            NpcState piotr = RegisterContentNpc(state, bundle, Piotr);
            corvin.SetPartner(Maren);
            maren.SetPartner(Corvin);
            corvin.AddChild(Piotr);
            maren.AddChild(Piotr);
            piotr.SetParents(Maren, Corvin);
            corvin.SetHousehold(FarmHousehold);
            maren.SetHousehold(FarmHousehold);
            piotr.SetHousehold(FarmHousehold);

            // A baby born during the simulation (no Content entry).
            NpcState baby = RegisterBornNpc(state, bundle);
            baby.SetParents(Maren, Corvin);
            baby.SetHousehold(FarmHousehold);
            corvin.AddChild(Born);
            maren.AddChild(Born);

            // Bessa: alive at the tavern.
            NpcState bessa = RegisterContentNpc(state, bundle, Bessa);
            bessa.SetHousehold(TavernHousehold);

            // Households.
            var farm = new Household(FarmHousehold, new LocationId("loc_farm"));
            farm.AddMember(Corvin);
            farm.AddMember(Maren);
            farm.AddMember(Piotr);
            farm.AddMember(Born);
            state.Households.Register(farm);
            var tavern = new Household(TavernHousehold, new LocationId("loc_tavern"));
            tavern.AddMember(Bessa);
            state.Households.Register(tavern);
            var elder = new Household(ElderHousehold, new LocationId("loc_home_elder"));
            elder.AddMember(Elswith);
            state.Households.Register(elder);

            // Generation cursors.
            state.RestoreAging(new AgingState(initialized: true, lastAgingDay: 700));
            state.RestoreFamily(new FamilyState(initialized: true, lastFamilyDay: 700, birthsSoFar: 1));
            state.RestoreInheritance(new InheritanceState(initialized: true,
                distributed: new[] { Elswith }));

            return state;
        }

        private static NpcState RegisterContentNpc(WorldState state, ContentBundle bundle, NpcId id)
        {
            NpcDefinition definition = bundle.NpcDefinitions[id];
            var npc = new NpcState(definition, 30, 80, 50);
            state.Npcs.Register(npc);
            return npc;
        }

        private static NpcState RegisterBornNpc(WorldState state, ContentBundle bundle)
        {
            var definition = new NpcDefinition(Born, "Child of Maren Alder", 0,
                "female", "child", new LocationId("loc_farm"), new LocationId("loc_farm"), 0,
                new Dictionary<string, int>(), new NeedRates(6, 4, 4),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            var baby = new NpcState(definition, 30, 80, 50);
            baby.BornInSimulation = true;
            state.Npcs.Register(baby);
            return baby;
        }

        [Test]
        public void GenerationStateSaveLoadRoundTrip()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase7RichWorld(bundle);

            string json = WorldSaver.Save(state);
            WorldState loaded = WorldLoader.Load(json, root);

            // NPC age and death.
            Assert.That(loaded.Npcs[Elswith].Age, Is.EqualTo(71));
            Assert.That(loaded.Npcs[Elswith].IsDeceased, Is.True);
            Assert.That(loaded.Npcs[Corvin].Age, Is.EqualTo(45));
            Assert.That(loaded.Npcs[Corvin].IsDeceased, Is.False);

            // Family links.
            Assert.That(loaded.Npcs[Piotr].MotherId, Is.EqualTo(Maren));
            Assert.That(loaded.Npcs[Piotr].FatherId, Is.EqualTo(Corvin));
            Assert.That(loaded.Npcs[Corvin].PartnerId, Is.EqualTo(Maren));
            Assert.That(loaded.Npcs[Maren].PartnerId, Is.EqualTo(Corvin));
            Assert.That(loaded.Npcs[Corvin].ChildrenIds, Is.EqualTo(new[] { Piotr, Born }));
            Assert.That(loaded.Npcs[Elswith].DesignatedHeirId, Is.EqualTo(Bessa));
            Assert.That(loaded.Npcs[Bessa].DesignatedHeirId.HasValue, Is.False);

            // Household links.
            Assert.That(loaded.Npcs[Corvin].HouseholdId, Is.EqualTo(FarmHousehold));
            Assert.That(loaded.Npcs[Elswith].HouseholdId, Is.EqualTo(ElderHousehold));

            // Households.
            Assert.That(loaded.Households.Count, Is.EqualTo(3));
            Household farm = loaded.Households[FarmHousehold];
            Assert.That(farm.Home, Is.EqualTo(new LocationId("loc_farm")));
            Assert.That(new HashSet<NpcId>(farm.Members).SetEquals(new[] { Corvin, Maren, Piotr, Born }),
                Is.True, "Farm household members.");

            // The born baby's definition was rebuilt from the inline detail.
            NpcState baby = loaded.Npcs[Born];
            Assert.That(baby.Definition.Name, Is.EqualTo("Child of Maren Alder"));
            Assert.That(baby.Definition.Gender, Is.EqualTo("female"));
            Assert.That(baby.Definition.Home, Is.EqualTo(new LocationId("loc_farm")));
            Assert.That(baby.MotherId, Is.EqualTo(Maren));
            Assert.That(baby.HouseholdId, Is.EqualTo(FarmHousehold));

            // Generation cursors.
            Assert.That(loaded.Aging.IsInitialized, Is.True);
            Assert.That(loaded.Aging.LastAgingDay, Is.EqualTo(700));
            Assert.That(loaded.Family.IsInitialized, Is.True);
            Assert.That(loaded.Family.LastFamilyDay, Is.EqualTo(700));
            Assert.That(loaded.Family.BirthsSoFar, Is.EqualTo(1));
            Assert.That(loaded.Inheritance.IsInitialized, Is.True);
            Assert.That(loaded.Inheritance.Distributed, Is.EquivalentTo(new[] { Elswith }));
        }

        [Test]
        public void GenerationSaveLoadSaveIsByteIdentical()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase7RichWorld(bundle);

            string first = WorldSaver.Save(state);
            WorldState loaded = WorldLoader.Load(first, root);
            string second = WorldSaver.Save(loaded);

            Assert.That(second, Is.EqualTo(first), "Save → load → save must be byte-identical.");
        }

        [Test]
        public void EmptyGenerationStateRoundTrips()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            var state = new WorldState(777, new GameTime(1400));
            RegisterContentNpc(state, bundle, Corvin);

            string json = WorldSaver.Save(state);
            WorldState loaded = WorldLoader.Load(json, root);

            Assert.That(loaded.Aging.IsInitialized, Is.False);
            Assert.That(loaded.Aging.LastAgingDay, Is.EqualTo(0));
            Assert.That(loaded.Family.IsInitialized, Is.False);
            Assert.That(loaded.Family.BirthsSoFar, Is.EqualTo(0));
            Assert.That(loaded.Households.Count, Is.EqualTo(0));
            Assert.That(loaded.Inheritance.IsInitialized, Is.False);
            Assert.That(loaded.Inheritance.Distributed, Is.Empty);
            Assert.That(loaded.Npcs[Corvin].Age, Is.EqualTo(45));
            Assert.That(loaded.Npcs[Corvin].IsDeceased, Is.False);
            Assert.That(loaded.Npcs[Corvin].MotherId.HasValue, Is.False);
            Assert.That(loaded.Npcs[Corvin].ChildrenIds, Is.Empty);
            Assert.That(loaded.Npcs[Corvin].HouseholdId.HasValue, Is.False);
        }

        [Test]
        public void GenerationSaveFormatVersion()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase7RichWorld(bundle);

            string v7 = WorldSaver.Save(state);
            Assert.That(v7, Does.Contain("\"formatVersion\": 7"));

            // A v6 document loads with generation defaults.
            WorldState loadedV6 = WorldLoader.Load(StripToVersion6(v7), root);
            Assert.That(loadedV6.Aging.IsInitialized, Is.False,
                "A v6 document has no aging cursor: uninitialized.");
            Assert.That(loadedV6.Family.IsInitialized, Is.False,
                "A v6 document has no family cursor: uninitialized.");
            Assert.That(loadedV6.Households.Count, Is.EqualTo(0),
                "A v6 document has no households.");
            Assert.That(loadedV6.Inheritance.IsInitialized, Is.False,
                "A v6 document has no inheritance cursor: uninitialized.");
            Assert.That(loadedV6.Inheritance.Distributed, Is.Empty);
            Assert.That(loadedV6.Npcs[Corvin].Age, Is.EqualTo(45),
                "A v6 document restores the Content age.");
            Assert.That(loadedV6.Npcs[Elswith].IsDeceased, Is.False,
                "A v6 document restores everyone alive.");
            Assert.That(loadedV6.Npcs[Piotr].MotherId.HasValue, Is.False,
                "A v6 document has no family links.");
        }

        [Test]
        public void FutureVersionIsRejected()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase7RichWorld(bundle);
            string json = WorldSaver.Save(state);
            string v8 = json.Replace("\"formatVersion\": 7", "\"formatVersion\": 8");

            Assert.Throws<LoadException>(() => WorldLoader.Load(v8, root));
        }

        [Test]
        public void CorruptGenerationIsRejected()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase7RichWorld(bundle);
            string json = WorldSaver.Save(state);

            // A family link to an NPC that is not in the save.
            string stranger = json.Replace("\"mother\": \"npc_maren_alder\"",
                "\"mother\": \"npc_nonexistent\"");
            Assert.Throws<LoadException>(() => WorldLoader.Load(stranger, root),
                "A family link to an unsaved NPC must be rejected.");

            // A duplicate household.
            string duplicated;
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement households = document.RootElement.GetProperty("households");
                string first = households[0].GetRawText();
                duplicated = json.Replace("\"households\": [",
                    "\"households\": [" + first + ",");
            }
            Assert.Throws<LoadException>(() => WorldLoader.Load(duplicated, root),
                "A duplicate household must be rejected.");

            // A negative age.
            Assert.That(json, Does.Contain("\"age\": 45"));
            string badAge = new System.Text.RegularExpressions.Regex("\"age\": 45")
                .Replace(json, "\"age\": -1", 1);
            Assert.Throws<LoadException>(() => WorldLoader.Load(badAge, root),
                "A negative age must be rejected.");
        }

        /// <summary>Rewrites a version 7 document as version 6 (drops Phase 7 sections).</summary>
        private static string StripToVersion6(string v7)
        {
            using (JsonDocument document = JsonDocument.Parse(v7))
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
                                    writer.WriteNumber("formatVersion", 6);
                                    break;
                                case "aging":
                                case "family":
                                case "households":
                                case "inheritance":
                                    break;
                                default:
                                    property.WriteTo(writer);
                                    break;
                            }
                        }
                        writer.WriteEndObject();
                    }
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }
    }
}
