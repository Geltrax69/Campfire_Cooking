using System;
using System.IO;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Proves grain-to-flour milling with the toll kept in kind.</summary>
    public sealed class MillingSystemTests
    {
        private static readonly LocationId Mill = new LocationId("loc_mill");
        private static readonly NpcId Miller = new NpcId("npc_garrick_alder");

        [Test]
        public void MillingKeepsTollInKindAndProducesFlour()
        {
            ItemCatalog catalog = TestCatalog();
            var stock = new Inventory(catalog);
            stock.Add(BakeryChainSetup.Grain, 24);
            var configuration = new MillingConfiguration("mill", Mill, Miller, stock,
                BakeryChainSetup.Grain, BakeryChainSetup.Flour, 12, 1, EventVisibility.Normal);
            var state = new WorldState(7, new GameTime(300));
            var world = new World(state);
            world.RegisterSystem(new MillingSystem(new[] { configuration }));

            world.Tick();

            // 12 sacks in: the 1-sack toll stays as grain, 11 are ground into 11 flour.
            Assert.That((stock.Count(BakeryChainSetup.Grain), stock.Count(BakeryChainSetup.Flour)),
                Is.EqualTo((13, 11)));
            WorldEvent produced = state.Events.Query().Single();
            Assert.That((produced.Type, produced.Location, produced.ItemType, produced.Quantity),
                Is.EqualTo((WorldEventType.Produced, Mill,
                    (ItemTypeId?)BakeryChainSetup.Flour, (int?)11)));

            world.Tick();

            Assert.That((stock.Count(BakeryChainSetup.Grain), stock.Count(BakeryChainSetup.Flour)),
                Is.EqualTo((2, 22)));
        }

        [Test]
        public void MillingWaitsForAFullBatch()
        {
            ItemCatalog catalog = TestCatalog();
            var stock = new Inventory(catalog);
            stock.Add(BakeryChainSetup.Grain, 11);
            var configuration = new MillingConfiguration("mill", Mill, Miller, stock,
                BakeryChainSetup.Grain, BakeryChainSetup.Flour, 12, 1, EventVisibility.Normal);
            var state = new WorldState(7, new GameTime(300));
            var system = new MillingSystem(new[] { configuration });

            system.Tick(state);

            Assert.That((stock.Count(BakeryChainSetup.Grain), stock.Count(BakeryChainSetup.Flour)),
                Is.EqualTo((11, 0)));
            Assert.That(state.Events.Query(), Is.Empty);
        }

        [Test]
        public void MillingMovesNoCopper()
        {
            // The toll is in kind by construction: the system never touches a wallet.
            // This test pins the money-conservation property at the unit level.
            ItemCatalog catalog = TestCatalog();
            var stock = new Inventory(catalog);
            stock.Add(BakeryChainSetup.Grain, 12);
            var wallet = new Wallet(150);
            var configuration = new MillingConfiguration("mill", Mill, Miller, stock,
                BakeryChainSetup.Grain, BakeryChainSetup.Flour, 12, 1, EventVisibility.Normal);
            var state = new WorldState(7, new GameTime(300));
            var system = new MillingSystem(new[] { configuration });

            system.Tick(state);

            Assert.That(wallet.Balance, Is.EqualTo(150));
        }

        [Test]
        public void InputsAreValidated()
        {
            ItemCatalog catalog = TestCatalog();
            var stock = new Inventory(catalog);
            var state = new WorldState(7);
            Assert.Throws<ArgumentException>(() => new MillingConfiguration("", Mill, Miller, stock,
                BakeryChainSetup.Grain, BakeryChainSetup.Flour, 12, 1, EventVisibility.Normal));
            Assert.Throws<ArgumentNullException>(() => new MillingConfiguration("mill", Mill, Miller, null,
                BakeryChainSetup.Grain, BakeryChainSetup.Flour, 12, 1, EventVisibility.Normal));
            Assert.Throws<ArgumentException>(() => new MillingConfiguration("mill", Mill, Miller, stock,
                BakeryChainSetup.Grain, BakeryChainSetup.Grain, 12, 1, EventVisibility.Normal));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MillingConfiguration("mill", Mill, Miller, stock,
                BakeryChainSetup.Grain, BakeryChainSetup.Flour, 0, 0, EventVisibility.Normal));
            Assert.Throws<ArgumentException>(() => new MillingConfiguration("mill", Mill, Miller, stock,
                BakeryChainSetup.Grain, BakeryChainSetup.Flour, 12, 12, EventVisibility.Normal));
            Assert.Throws<ArgumentException>(() => new MillingSystem(new[]
            {
                new MillingConfiguration("mill", Mill, Miller, stock,
                    BakeryChainSetup.Grain, BakeryChainSetup.Flour, 12, 1, EventVisibility.Normal),
                new MillingConfiguration("mill", Mill, Miller, stock,
                    BakeryChainSetup.Grain, BakeryChainSetup.Flour, 12, 1, EventVisibility.Normal),
            }));
            Assert.Throws<ArgumentNullException>(() => new MillingSystem(null));
            Assert.That(new MillingSystem(Array.Empty<MillingConfiguration>()).Phase,
                Is.EqualTo(SimulationPhase.Economy));
        }

        internal static ItemCatalog TestCatalog() => ContentBundle.Load(RepositoryRoot()).Catalog;

        internal static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }
    }
}
