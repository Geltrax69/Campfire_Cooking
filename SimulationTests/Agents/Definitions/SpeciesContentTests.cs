using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Exercises the species loader against the approved animals content and
    /// malformed documents.
    /// </summary>
    public sealed class SpeciesContentTests
    {
        private static readonly string ContentRoot = FindContentRoot();

        private static string FindContentRoot()
        {
            var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/animals/species.json")))
                root = root.Parent;
            Assert.That(root, Is.Not.Null, "Find repository content above test output.");
            return root.FullName;
        }

        private static LocationMap LoadLocationMap()
        {
            var map = new LocationMap(
                new[]
                {
                    new LocationDefinition(new LocationId("loc_farm"), "Alder Farm", "farm", 0, 0),
                    new LocationDefinition(new LocationId("loc_forest_edge"), "Forest Edge", "wild", 0, 0),
                    new LocationDefinition(new LocationId("loc_home_fenn"), "Fenn Home", "home", 0, 0),
                },
                Array.Empty<TravelLink>());
            return map;
        }

        private static IReadOnlyList<SpeciesDefinition> LoadApproved()
        {
            using var stream = File.OpenRead(Path.Combine(ContentRoot, "Content/animals/species.json"));
            return SpeciesContentLoader.Load(stream, LoadLocationMap());
        }

        private static MemoryStream Stream(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

        [Test]
        public void SpeciesLoadFromContent()
        {
            var species = LoadApproved();

            Assert.That(species.Select(s => s.Id.Value),
                Is.EqualTo(new[] { "species_brambleback", "species_chicken", "species_deer", "species_pig", "species_wolf" }));
            Assert.That(species.Select(s => s.Name),
                Is.EqualTo(new[] { "Brambleback", "Chicken", "Deer", "Pig (domestic) / Boar (wild)", "Wolf" }));

            var chicken = species.Single(s => s.Id == new SpeciesId("species_chicken"));
            Assert.That((chicken.Wild, chicken.Tameable, chicken.BondThreshold, chicken.DaysToBond),
                Is.EqualTo((false, true, 60, 7)));
            Assert.That(chicken.Habitats.Select(h => h.Value), Is.EqualTo(new[] { "loc_farm", "loc_home_fenn" }));
            Assert.That(chicken.Diet, Is.EqualTo(new[] { "item_grain", "insects", "kitchen scraps" }));
            Assert.That(chicken.PopulationCount, Is.EqualTo(20));

            var pig = species.Single(s => s.Id == new SpeciesId("species_pig"));
            Assert.That((pig.Wild, pig.Tameable, pig.BondThreshold, pig.DaysToBond),
                Is.EqualTo((false, true, 70, 12)));
            Assert.That(pig.Habitats.Select(h => h.Value), Is.EqualTo(new[] { "loc_farm", "loc_forest_edge" }));
            Assert.That(pig.PopulationCount, Is.EqualTo(12));

            var deer = species.Single(s => s.Id == new SpeciesId("species_deer"));
            Assert.That((deer.Wild, deer.Tameable, deer.BondThreshold, deer.DaysToBond),
                Is.EqualTo((true, true, 80, 25)));
            Assert.That(deer.Habitats.Select(h => h.Value), Is.EqualTo(new[] { "loc_forest_edge" }));
            Assert.That(deer.PopulationCount, Is.EqualTo(40));

            var wolf = species.Single(s => s.Id == new SpeciesId("species_wolf"));
            Assert.That((wolf.Wild, wolf.Tameable, wolf.BondThreshold, wolf.DaysToBond),
                Is.EqualTo((true, true, 85, 60)));
            Assert.That(wolf.Habitats.Select(h => h.Value), Is.EqualTo(new[] { "loc_forest_edge" }));
            Assert.That(wolf.PopulationCount, Is.EqualTo(6));

            var brambleback = species.Single(s => s.Id == new SpeciesId("species_brambleback"));
            Assert.That((brambleback.Wild, brambleback.Tameable, brambleback.BondThreshold, brambleback.DaysToBond),
                Is.EqualTo((true, true, 50, 5)));
            Assert.That(brambleback.Habitats.Select(h => h.Value), Is.EqualTo(new[] { "loc_forest_edge", "loc_farm" }));
            Assert.That(brambleback.PopulationCount, Is.EqualTo(25));
        }

        [Test]
        public void LoaderAcceptsUnknownFieldsAndReportsMalformedDocuments()
        {
            const string row = "{\"id\":\"species_chicken\",\"future\":\"ignored\"}";
            const string doc = "{\"version\":1,\"species\":[" + row + "],\"ecosystemChains\":[]}";
            using var stream = Stream(doc);
            // Fails: a real species row is required, unknown future fields are ignored.
            Assert.Throws<SerializationException>(() => SpeciesContentLoader.Load(stream, LoadLocationMap()));

            // Missing version.
            using var noVersion = Stream("{\"species\":[]}");
            Assert.Throws<SerializationException>(() => SpeciesContentLoader.Load(noVersion, LoadLocationMap()));

            // Wrong version.
            using var wrongVersion = Stream("{\"version\":2,\"species\":[]}");
            Assert.Throws<SerializationException>(() => SpeciesContentLoader.Load(wrongVersion, LoadLocationMap()));

            // Null stream.
            Assert.Throws<ArgumentNullException>(() => SpeciesContentLoader.Load(null, LoadLocationMap()));
            Assert.Throws<ArgumentNullException>(() => SpeciesContentLoader.Load(Stream(doc), null));
        }
    }
}
