using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Verifies the starting animal populations match docs/design/ANIMALS.md and
    /// Content/animals/species.json: counts, habitats, and trust conventions.
    /// </summary>
    public sealed class AnimalPopulationTests
    {
        private static readonly SpeciesId Chicken = new SpeciesId("species_chicken");
        private static readonly SpeciesId Pig = new SpeciesId("species_pig");
        private static readonly SpeciesId Deer = new SpeciesId("species_deer");
        private static readonly SpeciesId Wolf = new SpeciesId("species_wolf");
        private static readonly SpeciesId Brambleback = new SpeciesId("species_brambleback");
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly LocationId ForestEdge = new LocationId("loc_forest_edge");
        private static readonly LocationId FennHome = new LocationId("loc_home_fenn");

        private static IReadOnlyList<SpeciesDefinition> LoadSpecies()
        {
            var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/animals/species.json")))
                root = root.Parent;
            Assert.That(root, Is.Not.Null, "Find repository content above test output.");
            var map = new LocationMap(
                new[] { Farm, ForestEdge, FennHome }.Select(id =>
                    new LocationDefinition(id, id.Value, "test", 0, 0)),
                Array.Empty<TravelLink>());
            using var stream = File.OpenRead(Path.Combine(root.FullName, "Content/animals/species.json"));
            return SpeciesContentLoader.Load(stream, map);
        }

        private static IReadOnlyList<AnimalState> Create() =>
            AnimalPopulationFactory.CreateInitialPopulations(LoadSpecies());

        [Test]
        public void InitialPopulationsMatchDesign()
        {
            var animals = Create();

            Assert.That(animals.Count(a => a.Species == Chicken), Is.EqualTo(20));
            Assert.That(animals.Count(a => a.Species == Pig), Is.EqualTo(12));
            Assert.That(animals.Count(a => a.Species == Deer), Is.EqualTo(40));
            Assert.That(animals.Count(a => a.Species == Wolf), Is.EqualTo(6));
            Assert.That(animals.Count(a => a.Species == Brambleback), Is.EqualTo(25));

            // Habitat placement.
            Assert.That(animals.Where(a => a.Species == Deer).All(a => a.Location == ForestEdge), Is.True);
            Assert.That(animals.Where(a => a.Species == Wolf).All(a => a.Location == ForestEdge), Is.True);
            var farmChickens = animals.Count(a => a.Species == Chicken && a.Location == Farm);
            var fennChickens = animals.Count(a => a.Species == Chicken && a.Location == FennHome);
            Assert.That((farmChickens, fennChickens), Is.EqualTo((14, 6)));
            var farmPigs = animals.Count(a => a.Species == Pig && a.Location == Farm);
            var boars = animals.Count(a => a.Species == Pig && a.Location == ForestEdge);
            Assert.That((farmPigs, boars), Is.EqualTo((6, 6)));
            Assert.That(animals.Count(a => a.Species == Pig && a.Age == AnimalAge.Young), Is.EqualTo(4),
                "Piglets are the young domestic pigs.");

            // Trust conventions: wild animals start at 0, domestic at 20-30.
            Assert.That(animals.Where(a => a.Species == Deer).All(a => a.Trust == 0), Is.True);
            Assert.That(animals.Where(a => a.Species == Wolf).All(a => a.Trust == 0), Is.True);
            Assert.That(animals.Where(a => a.Species == Brambleback).All(a => a.Trust == 0), Is.True);
            Assert.That(animals.Where(a => a.Species == Pig && a.Location == ForestEdge).All(a => a.Trust == 0),
                Is.True, "Wild boars start at trust 0.");
            Assert.That(animals.Where(a => a.Species == Chicken).All(a => a.Trust >= 20 && a.Trust <= 30),
                Is.True);
            Assert.That(animals.Where(a => a.Species == Pig && a.Location == Farm).All(a => a.Trust >= 20 && a.Trust <= 30),
                Is.True);

            // Fresh state: nobody is bonded, all animals are healthy.
            Assert.That(animals.All(a => a.Owner == null), Is.True);
            Assert.That(animals.All(a => a.Health == 100), Is.True);

            // IDs are unique, ordinal, and deterministic across runs.
            Assert.That(animals.Select(a => a.Id.Value).Distinct().Count(), Is.EqualTo(animals.Count));
            var again = Create();
            Assert.That(again.Select(a => a.Id.Value), Is.EqualTo(animals.Select(a => a.Id.Value)));
            Assert.That(again.Select(a => a.Trust), Is.EqualTo(animals.Select(a => a.Trust)));
        }

        [Test]
        public void FactoryRequiresTheFiveDesignSpecies()
        {
            var species = LoadSpecies().Where(s => s.Id != Deer).ToList();
            Assert.Throws<ArgumentException>(() => AnimalPopulationFactory.CreateInitialPopulations(species));
            Assert.Throws<ArgumentNullException>(() => AnimalPopulationFactory.CreateInitialPopulations(null));
        }
    }
}
