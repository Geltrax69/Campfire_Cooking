using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Core.Locations
{
    /// <summary>Checks approved content, shortest durations and immutable validated map inputs.</summary>
    public sealed class LocationMapTests
    {
        internal static readonly LocationId A = new LocationId("loc_A");
        internal static readonly LocationId B = new LocationId("loc_B");
        internal static readonly LocationId C = new LocationId("loc_C");
        internal static LocationDefinition Place(LocationId id) => new LocationDefinition(id, id.Value, "home", 0, 0);
        internal static LocationMap TinyMap() => new LocationMap(new[] { Place(C), Place(B), Place(A) },
            new[] { new TravelLink(A, B, 2) });

        [Test]
        public void ApprovedContentLoadsAllDefinitionsAndUndirectedLinks()
        {
            DirectoryInfo root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/world/locations.json"))) root = root.Parent;
            Assert.That(root, Is.Not.Null, "Find repository content above test output.");
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "Content/world/locations.json")));
            var definitions = json.RootElement.GetProperty("locations").EnumerateArray().Select(location =>
            {
                var owner = location.GetProperty("owner").GetString();
                var position = location.GetProperty("position");
                return new LocationDefinition(new LocationId(location.GetProperty("id").GetString()),
                    location.GetProperty("name").GetString(), location.GetProperty("type").GetString(),
                    position.GetProperty("x").GetInt32(), position.GetProperty("y").GetInt32(),
                    owner == "village" ? (NpcId?)null : new NpcId(owner));
            }).ToArray();
            var links = json.RootElement.GetProperty("travelMinutes").EnumerateArray().Select(link =>
                new TravelLink(new LocationId(link.GetProperty("from").GetString()),
                    new LocationId(link.GetProperty("to").GetString()), link.GetProperty("minutes").GetInt32())).ToArray();
            var map = new LocationMap(definitions, links);
            Assert.That(map.Locations.Count, Is.EqualTo(23));
            Assert.That(links.Length, Is.EqualTo(36));
            foreach (var link in links)
            {
                Assert.That(map.GetTravelMinutes(link.From, link.To), Is.EqualTo(link.Minutes));
                Assert.That(map.GetTravelMinutes(link.To, link.From), Is.EqualTo(link.Minutes));
            }
            var stall = map[new LocationId("loc_apple_stall")];
            Assert.That((stall.Name, stall.Type, stall.X, stall.Y, stall.Owner),
                Is.EqualTo(("Holt's Apple Stall", "shop", 14, -10, (NpcId?)new NpcId("npc_mira_holt"))));
            Assert.That(map[new LocationId("loc_square")].Owner, Is.Null);
            Assert.That(map.GetTravelMinutes(stall.Id, stall.Id), Is.Zero);
            Assert.That(map.GetTravelMinutes(stall.Id, new LocationId("loc_forest_edge")), Is.EqualTo(2));
            Assert.That(map.GetTravelMinutes(new LocationId("loc_bakery"), new LocationId("loc_tavern")), Is.EqualTo(2));
        }

        [Test]
        public void MapCopiesInputsOrdersIdsAndChoosesShortestInsteadOfDirectRoute()
        {
            var places = new[] { Place(C), Place(B), Place(A) };
            var links = new[] { new TravelLink(A, C, 9), new TravelLink(A, B, 2), new TravelLink(B, C, 3) };
            var map = new LocationMap(places, links);
            places[0] = Place(new LocationId("replacement"));
            links[0] = new TravelLink(A, C, 1);
            Assert.That(map.Locations.Select(location => location.Id), Is.EqualTo(new[] { A, B, C }));
            Assert.That(map.GetTravelMinutes(A, C), Is.EqualTo(5));
            Assert.Throws<NotSupportedException>(() => ((IList<LocationDefinition>)map.Locations).Clear());
            Assert.That(TinyMap().GetTravelMinutes(A, C), Is.Null);
            Assert.That(TinyMap().GetTravelMinutes(C, C), Is.Zero);
        }

        [Test]
        public void LongRoutesDoNotOverflowIntegerLinkDurations()
        {
            var map = new LocationMap(new[] { Place(A), Place(B), Place(C) },
                new[] { new TravelLink(A, B, int.MaxValue), new TravelLink(B, C, int.MaxValue) });
            Assert.That(map.GetTravelMinutes(A, C), Is.EqualTo(2L * int.MaxValue));
            Assert.That(map.GetTravelMinutes(C, A), Is.EqualTo(2L * int.MaxValue));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void DefinitionsRejectBlankNameOrType(string value)
        {
            Assert.Throws<ArgumentException>(() => new LocationDefinition(A, value, "home", 0, 0));
            Assert.Throws<ArgumentException>(() => new LocationDefinition(A, "Home", value, 0, 0));
        }

        [Test]
        public void InvalidMapInputsAndDefaultIdsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => Place(default));
            Assert.Throws<ArgumentException>(() => new LocationDefinition(A, "Home", "home", 0, 0, default(NpcId)));
            Assert.Throws<ArgumentNullException>(() => new LocationMap(null, Array.Empty<TravelLink>()));
            Assert.Throws<ArgumentNullException>(() => new LocationMap(new[] { Place(A) }, null));
            Assert.Throws<ArgumentException>(() => new LocationMap(new[] { Place(A), Place(A) }, Array.Empty<TravelLink>()));
            Assert.Throws<ArgumentException>(() => new LocationMap(new LocationDefinition[] { null }, Array.Empty<TravelLink>()));
            Assert.Throws<ArgumentException>(() => new LocationMap(new[] { Place(A), Place(B) }, new TravelLink[] { null }));
            Assert.Throws<ArgumentException>(() => new LocationMap(new[] { Place(A) }, new[] { new TravelLink(A, B, 1) }));
            foreach (bool reverse in new[] { false, true })
                Assert.Throws<ArgumentException>(() => new LocationMap(new[] { Place(A), Place(B) },
                    new[] { new TravelLink(A, B, 1), new TravelLink(reverse ? B : A, reverse ? A : B, 2) }));
            Assert.Throws<ArgumentException>(() => new TravelLink(default, B, 1));
            Assert.Throws<ArgumentException>(() => new TravelLink(A, default, 1));
            Assert.Throws<ArgumentException>(() => new TravelLink(A, A, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TravelLink(A, B, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TravelLink(A, B, -1));
            Assert.Throws<ArgumentException>(() => TinyMap().GetTravelMinutes(default, A));
            Assert.Throws<ArgumentException>(() => TinyMap().GetTravelMinutes(A, new LocationId("unknown")));
        }
    }
}
