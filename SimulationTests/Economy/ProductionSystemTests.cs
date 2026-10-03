using System;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;
using static LivingWorld.Simulation.Tests.Economy.ItemCatalogTests;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Proves one-time configured production with persistence-ready completion state.</summary>
    public sealed class ProductionSystemTests
    {
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly NpcId Farmer = new NpcId("npc_farmer");

        [Test]
        public void ProductionOccursOnceWhenEligibleAndSurvivesSystemRecreation()
        {
            var inventory = new Inventory(TinyCatalog());
            var configuration = new ProductionConfiguration("production_apples", Farm, Farmer, inventory,
                Apple, 12, new GameTime(60), EventVisibility.Normal);
            var system = new ProductionSystem(new[] { configuration }, new ProductionState());
            var world = new World(new WorldState(42, new GameTime(58)));
            world.RegisterSystem(system);
            world.Tick();
            Assert.That((inventory.Count(Apple), world.State.Events.Count), Is.EqualTo((0, 0)));
            world.Tick();
            system = new ProductionSystem(new[] { configuration }, system.State);
            system.Tick(world.State);

            Assert.That(inventory.Count(Apple), Is.EqualTo(12));
            Assert.That(system.State.CompletedIds, Is.EqualTo(new[] { "production_apples" }));
            WorldEvent produced = world.State.Events.Query().Single();
            Assert.That((produced.Type, produced.Time, produced.Location, produced.Actor,
                produced.ItemType, produced.Quantity),
                Is.EqualTo((WorldEventType.Produced, new GameTime(60), Farm,
                    (ActorId?)ActorId.ForNpc(Farmer), (ItemTypeId?)Apple, (int?)12)));
        }

        [Test]
        public void ConfigurationsAndSnapshotsUseStableOrdinalOrder()
        {
            var inventory = new Inventory(TinyCatalog());
            var ordinalLast = new ProductionConfiguration("production_z", Farm, Farmer, inventory,
                Apple, 2, new GameTime(10), EventVisibility.Normal);
            var ordinalFirst = new ProductionConfiguration("production_a", new LocationId("loc_a_field"),
                Farmer, inventory, Bread, 1, new GameTime(10), EventVisibility.Quiet);
            var system = new ProductionSystem(new[] { ordinalLast, ordinalFirst }, new ProductionState());
            var state = new WorldState(42, new GameTime(10));

            system.Tick(state);

            Assert.That(state.Events.Query().Select(entry => entry.Location),
                Is.EqualTo(new[] { ordinalFirst.Location, Farm }));
            Assert.That(system.State.CompletedIds,
                Is.EqualTo(new[] { "production_a", "production_z" }));
        }

        [Test]
        public void InputsAndRestoredStateAreValidated()
        {
            var inventory = new Inventory(TinyCatalog());
            Assert.Throws<ArgumentException>(() => new ProductionConfiguration("", Farm, Farmer,
                inventory, Apple, 1, new GameTime(1), EventVisibility.Normal));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProductionConfiguration("production", Farm,
                Farmer, inventory, Apple, 0, new GameTime(1), EventVisibility.Normal));
            Assert.Throws<ArgumentNullException>(() => new ProductionSystem(null, new ProductionState()));
            Assert.Throws<ArgumentException>(() => new ProductionState(new[] { "production", "production" }));
            Assert.Throws<ArgumentException>(() => new ProductionSystem(Array.Empty<ProductionConfiguration>(),
                new ProductionState(new[] { "unknown" })));
            Assert.That(new ProductionSystem(Array.Empty<ProductionConfiguration>(),
                new ProductionState()).Phase, Is.EqualTo(SimulationPhase.Economy));
        }
    }
}
