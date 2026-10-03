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
    /// <summary>Proves approved schedule loading, calendar selection, gaps and validation.</summary>
    public sealed class NpcScheduleTests
    {
        private const string BaseRow = "{\"id\":\"npc_test\",\"name\":\"Test\",\"age\":34,\"gender\":\"female\","
            + "\"occupation\":\"shopkeeper\",\"home\":\"loc_home\",\"workplace\":\"loc_work\",\"money\":10,"
            + "\"traits\":{},\"needRates\":{\"hungerPerHour\":1,\"energyPerHour\":1,\"socialPerHour\":1},"
            + "\"schedule\":{\"workday\":[{\"activity\":\"work\",\"from\":\"08:00\",\"location\":\"loc_work\",\"to\":\"12:00\"},"
            + "{\"activity\":\"sleep\",\"from\":\"21:00\",\"location\":\"loc_home\",\"to\":\"06:00\"}],"
            + "\"restday\":[{\"activity\":\"pray\",\"from\":\"09:00\",\"location\":\"loc_shrine\",\"to\":\"10:00\"},"
            + "{\"activity\":\"sleep\",\"from\":\"22:00\",\"location\":\"loc_home\",\"to\":\"07:00\"}]}}";

        [Test]
        public void LoadsApprovedSchedulesAndUsesThirddayRestdayOvernightAndGaps()
        {
            var definitions = LoadApproved();
            Assert.That(definitions, Has.Count.EqualTo(20));
            var mira = definitions.Single(n => n.Id == new NpcId("npc_mira_holt"));
            Assert.That(mira.Schedule.Workday, Has.Count.EqualTo(9));
            Assert.That(mira.Schedule.Restday, Has.Count.EqualTo(7));

            AssertEntry(mira.Schedule.At(Time(day: 1, hour: 7)), ActivityKind.Work, "loc_apple_stall");
            AssertEntry(mira.Schedule.At(Time(day: 1, hour: 22)), ActivityKind.Sleep, "loc_home_mira");
            AssertEntry(mira.Schedule.At(Time(day: 2, hour: 2)), ActivityKind.Sleep, "loc_home_mira");
            AssertEntry(mira.Schedule.At(Time(day: 5, hour: 9)), ActivityKind.Shop, "loc_square");
            AssertEntry(mira.Schedule.At(Time(day: 5, hour: 22)), ActivityKind.Sleep, "loc_home_mira");
            AssertEntry(mira.Schedule.At(Time(day: 6, hour: 2)), ActivityKind.Sleep, "loc_home_mira");

            var doran = definitions.Single(n => n.Id == new NpcId("npc_doran_kettle"));
            Assert.That(doran.Schedule.At(Time(day: 1, hour: 6, minute: 45)), Is.Null);
        }

        [Test]
        public void PreservesEveryApprovedActivityKindAndReadOnlyOrder()
        {
            var definitions = LoadApproved();
            var kinds = definitions.SelectMany(n => n.Schedule.Workday.Concat(n.Schedule.Restday))
                .Select(entry => entry.Kind).Distinct().OrderBy(kind => kind).ToArray();
            Assert.That(kinds, Is.EqualTo(new[] { ActivityKind.Eat, ActivityKind.Sleep, ActivityKind.Work,
                ActivityKind.Socialize, ActivityKind.Shop, ActivityKind.Rest, ActivityKind.Chores,
                ActivityKind.Pray, ActivityKind.Learn, ActivityKind.Play }.OrderBy(kind => kind)));
            var schedule = definitions[0].Schedule;
            Assert.Throws<NotSupportedException>(() => ((IList<ScheduleEntry>)schedule.Workday).Clear());
        }

        [TestCase("schedule")]
        [TestCase("schedule.workday")]
        [TestCase("schedule.restday")]
        public void MissingScheduleSectionsAreRejected(string path)
        {
            var row = JsonNode.Parse(BaseRow);
            var keys = path.Split('.');
            var parent = keys.Length == 1 ? row : row[keys[0]];
            parent.AsObject().Remove(keys[keys.Length - 1]);
            Assert.Catch(() => Load(row.ToJsonString()));
        }

        [TestCase("from", "\"6:00\"")]
        [TestCase("from", "\"24:00\"")]
        [TestCase("to", "\"08:00\"")]
        [TestCase("activity", "\"unknown\"")]
        [TestCase("activity", "\" \"")]
        [TestCase("location", "\"loc_missing\"")]
        [TestCase("location", "null")]
        public void MalformedScheduleEntriesAreRejected(string field, string value)
        {
            var row = JsonNode.Parse(BaseRow);
            row["schedule"]["workday"][0][field] = JsonNode.Parse(value);
            Assert.Catch(() => Load(row.ToJsonString()));
        }

        [Test]
        public void OverlappingAndDuplicateScheduleSectionsAreRejected()
        {
            var row = JsonNode.Parse(BaseRow);
            row["schedule"]["workday"].AsArray().Add(JsonNode.Parse(
                "{\"activity\":\"eat\",\"from\":\"11:00\",\"location\":\"loc_home\",\"to\":\"13:00\"}"));
            Assert.Catch(() => Load(row.ToJsonString()));

            string duplicate = BaseRow.Replace("\"workday\":", "\"workday\":[],\"workday\":");
            Assert.Catch(() => Load(duplicate));
        }

        private static IReadOnlyList<NpcDefinition> LoadApproved()
        {
            var root = RepositoryRoot();
            using var locations = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Content/world/locations.json")));
            var definitions = locations.RootElement.GetProperty("locations").EnumerateArray().Select(place =>
                new LocationDefinition(new LocationId(place.GetProperty("id").GetString()),
                    place.GetProperty("name").GetString(), place.GetProperty("type").GetString(), 0, 0));
            var map = new LocationMap(definitions, Array.Empty<TravelLink>());
            using var stream = File.OpenRead(Path.Combine(root, "Content/npcs/npcs.json"));
            return NpcContentLoader.Load(stream, map);
        }

        private static IReadOnlyList<NpcDefinition> Load(string row)
        {
            var locations = new[] { "loc_home", "loc_work", "loc_shrine" }.Select(id =>
                new LocationDefinition(new LocationId(id), id, "test", 0, 0));
            var map = new LocationMap(locations, Array.Empty<TravelLink>());
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"version\":1,\"npcs\":[" + row + "]}"));
            return NpcContentLoader.Load(stream, map);
        }

        private static string RepositoryRoot()
        {
            var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/npcs/npcs.json"))) root = root.Parent;
            Assert.That(root, Is.Not.Null);
            return root.FullName;
        }

        private static GameTime Time(int day, int hour, int minute = 0) =>
            new GameTime((day - 1L) * 1440 + hour * 60 + minute);

        private static void AssertEntry(ScheduleEntry entry, ActivityKind kind, string destination)
        {
            Assert.That(entry, Is.Not.Null);
            Assert.That((entry.Kind, entry.Destination), Is.EqualTo((kind, new LocationId(destination))));
        }
    }
}
