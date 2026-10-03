using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>Exercises the production loader against approved content and invalid input boundaries.</summary>
    public sealed class NpcContentTests
    {
        private const string Row = "{\"id\":\"npc_test\",\"name\":\"Test\",\"age\":34,\"gender\":\"female\","
            + "\"occupation\":\"shopkeeper\",\"home\":\"loc_home\",\"workplace\":\"loc_home\",\"money\":850,"
            + "\"traits\":{\"honest\":85,\"friendly\":65},"
            + "\"needRates\":{\"hungerPerHour\":6,\"energyPerHour\":4,\"socialPerHour\":3}}";
        private static readonly LocationId Home = new LocationId("loc_home");
        private static LocationMap Map() => new LocationMap(
            new[] { new LocationDefinition(Home, "Home", "home", 0, 0) }, Array.Empty<TravelLink>());
        private static MemoryStream Stream(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));
        private static string Document(string rows) => "{\"version\":1,\"npcs\":[" + rows + "]}";
        private static IReadOnlyList<NpcDefinition> Load(string json)
        {
            using var stream = Stream(json);
            return NpcContentLoader.Load(stream, Map());
        }

        [Test]
        public void RuntimeLoaderReadsTwentyApprovedVillagersAndMirasFields()
        {
            var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/npcs/npcs.json"))) root = root.Parent;
            Assert.That(root, Is.Not.Null, "Find repository content above test output.");
            using var locations = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "Content/world/locations.json")));
            var map = new LocationMap(locations.RootElement.GetProperty("locations").EnumerateArray().Select(place =>
                new LocationDefinition(new LocationId(place.GetProperty("id").GetString()),
                    place.GetProperty("name").GetString(), place.GetProperty("type").GetString(), 0, 0)),
                Array.Empty<TravelLink>());
            using var stream = File.OpenRead(Path.Combine(root.FullName, "Content/npcs/npcs.json"));
            var npcs = NpcContentLoader.Load(stream, map);
            Assert.That(npcs.Count, Is.EqualTo(20));
            Assert.That(npcs.Select(n => n.Id), Is.Ordered);
            Assert.That(npcs.Select(n => n.Id).Distinct().Count(), Is.EqualTo(20));
            var mira = npcs.Single(n => n.Id == new NpcId("npc_mira_holt"));
            Assert.That((mira.Name, mira.Age, mira.Gender, mira.Occupation),
                Is.EqualTo(("Mira Holt", 34, "female", "apple-stall owner")));
            Assert.That((mira.Home, mira.Workplace, mira.StartingMoneyCopper),
                Is.EqualTo((new LocationId("loc_home_mira"), new LocationId("loc_apple_stall"), 850)));
            Assert.That(mira.Traits, Has.Count.EqualTo(10));
            Assert.That((mira.Traits["honest"], mira.Traits["friendly"], mira.Traits["cautious"]), Is.EqualTo((85, 65, 70)));
            Assert.That((mira.NeedRates.HungerPerHour, mira.NeedRates.EnergyPerHour, mira.NeedRates.SocialPerHour),
                Is.EqualTo((6, 4, 3)));
            stream.Position = 0;
            Assert.That(stream.ReadByte(), Is.EqualTo((int)'{'));
        }

        [Test]
        public void TinyDocumentsAcceptUnknownFieldsReturnReadOnlyOrdinalDefinitionsAndLeaveStreamOpen()
        {
            string row = Row.Replace("\"id\":", "\"future\":{\"schedule\":[1,2]},\"id\":");
            using var stream = Stream(Document(row.Replace("npc_test", "npc_z") + "," + row.Replace("npc_test", "npc_A")));
            var npcs = NpcContentLoader.Load(stream, Map());
            Assert.That(npcs.Select(n => n.Id.Value), Is.EqualTo(new[] { "npc_A", "npc_z" }));
            Assert.Throws<NotSupportedException>(() => ((IList<NpcDefinition>)npcs).Clear());
            Assert.That(Load(Document(Row)), Has.Count.EqualTo(1));
            Assert.That(Load(Document("")), Is.Empty);
            stream.Position = 0;
            Assert.That(stream.ReadByte(), Is.EqualTo((int)'{'));
        }

        [TestCase("id")]
        [TestCase("name")]
        [TestCase("age")]
        [TestCase("gender")]
        [TestCase("occupation")]
        [TestCase("home")]
        [TestCase("workplace")]
        [TestCase("money")]
        [TestCase("traits")]
        [TestCase("needRates")]
        [TestCase("needRates.hungerPerHour")]
        [TestCase("needRates.energyPerHour")]
        [TestCase("needRates.socialPerHour")]
        public void RequiredFieldsCannotSilentlyBecomeDefaults(string path)
        {
            var row = JsonNode.Parse(Row);
            var keys = path.Split('.');
            var parent = keys.Length == 1 ? row : row[keys[0]];
            parent.AsObject().Remove(keys[keys.Length - 1]);
            Assert.Catch(() => Load(Document(row.ToJsonString())));
        }

        [TestCase("id", "null")]
        [TestCase("name", "\" \"")]
        [TestCase("gender", "null")]
        [TestCase("occupation", "\"\"")]
        [TestCase("age", "-1")]
        [TestCase("age", "null")]
        [TestCase("money", "-1")]
        [TestCase("money", "2147483648")]
        [TestCase("traits", "null")]
        [TestCase("traits.honest", "-1")]
        [TestCase("traits.friendly", "101")]
        [TestCase("needRates", "null")]
        [TestCase("needRates.hungerPerHour", "-1")]
        [TestCase("needRates.energyPerHour", "-1")]
        [TestCase("needRates.socialPerHour", "-1")]
        [TestCase("home", "\"loc_missing\"")]
        [TestCase("workplace", "\"loc_missing\"")]
        public void InvalidFieldValuesAreRejected(string path, string value)
        {
            var row = JsonNode.Parse(Row);
            var keys = path.Split('.');
            var parent = keys.Length == 1 ? row : row[keys[0]];
            parent[keys[keys.Length - 1]] = JsonNode.Parse(value);
            Assert.Catch(() => Load(Document(row.ToJsonString())));
        }

        [TestCase("{")]
        [TestCase("null")]
        [TestCase("{\"npcs\":[]}")]
        [TestCase("{\"version\":1}")]
        [TestCase("{\"version\":2,\"npcs\":[]}")]
        [TestCase("{\"version\":1,\"npcs\":null}")]
        [TestCase("{\"version\":1,\"npcs\":[null]}")]
        public void InvalidDocumentsAreRejected(string json) => Assert.Catch(() => Load(json));

        [Test]
        public void DuplicateNpcAndTraitKeysAreRejectedWithoutClosingCallerStream()
        {
            using var stream = Stream(Document(Row + "," + Row));
            Assert.Catch(() => NpcContentLoader.Load(stream, Map()));
            Assert.That(stream.CanRead, Is.True);
            Assert.Catch(() => Load(Document(Row.Replace("\"friendly\":65", "\"honest\":65"))));
            Assert.Throws<ArgumentNullException>(() => NpcContentLoader.Load(null, Map()));
            Assert.Throws<ArgumentNullException>(() => NpcContentLoader.Load(stream, null));
        }

        [Test]
        public void DefinitionsDefensivelyCopyTraitsAndValidateCallerConstructedInputs()
        {
            var traits = new Dictionary<string, int> { ["z"] = 100, ["A"] = 0 };
            var npc = Define(traits);
            traits["z"] = 5;
            Assert.That(npc.Traits.Keys, Is.EqualTo(new[] { "A", "z" }));
            Assert.That(npc.Traits["z"], Is.EqualTo(100));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)npc.Traits).Clear());
            Assert.Throws<ArgumentNullException>(() => Define(null));
            Assert.Throws<ArgumentException>(() => Define(new Dictionary<string, int> { [" "] = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Define(new Dictionary<string, int> { ["honest"] = 101 }));
            Assert.Throws<ArgumentException>(() => Define(traits, default(NpcId)));
            Assert.Throws<ArgumentException>(() => Define(traits, home: default(LocationId)));
            Assert.Throws<ArgumentException>(() => Define(traits, workplace: default(LocationId)));
        }

        private static NpcDefinition Define(IEnumerable<KeyValuePair<string, int>> traits, NpcId? id = null,
            LocationId? home = null, LocationId? workplace = null) => new NpcDefinition(id ?? new NpcId("npc_test"),
                "Test", 34, "female", "shopkeeper", home ?? Home, workplace ?? Home, 850, traits, new NeedRates(6, 4, 3));
    }
}
