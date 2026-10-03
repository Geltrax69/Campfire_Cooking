using System;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;
using static LivingWorld.Simulation.Tests.Economy.ItemCatalogTests;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Proves atomic wholesale restocking with persistence-ready order state.</summary>
    public sealed class RestockSystemTests
    {
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly LocationId Stall = new LocationId("loc_stall");
        private static readonly NpcId Farmer = new NpcId("npc_farmer");
        private static readonly NpcId Shopkeeper = new NpcId("npc_shopkeeper");

        [Test]
        public void OnlyStockStrictlyBelowThresholdCreatesOnePendingOrder()
        {
            Scenario low = Create(shopStock: 9);
            var lowSystem = SystemFor(low, threshold: 10);
            lowSystem.Tick(low.State);
            lowSystem = new RestockSystem(new[] { Configuration(low) }, low.State);
            lowSystem.Tick(low.State);

            Assert.That(low.State.Restock.PendingOrders.Single().ConfigurationId, Is.EqualTo("restock_apples"));
            Assert.That(low.State.Restock.TriggeredIds, Is.EqualTo(new[] { "restock_apples" }));
            Assert.That(low.State.Events.Query(type: WorldEventType.RestockOrdered), Has.Count.EqualTo(1));

            Scenario adequate = Create(shopStock: 10);
            var adequateSystem = SystemFor(adequate, threshold: 10);
            adequateSystem.Tick(adequate.State);
            Assert.That(adequate.State.Restock.PendingOrders, Is.Empty);
            Assert.That(adequate.State.Events.Count, Is.Zero);
        }

        [Test]
        public void FullDeliveryMovesFortyFiveItemsAndCopperAndConservesTotals()
        {
            Scenario scenario = Create(shopStock: 2, shopCopper: 45, producerStock: 45, producerCopper: 7);
            int itemsBefore = scenario.Shop.Stock.Count(Apple) + scenario.ProducerStock.Count(Apple);
            int copperBefore = scenario.Shop.OwnerWallet.Balance + scenario.ProducerWallet.Balance;
            var configuration = Configuration(scenario);
            var system = new RestockSystem(new[] { configuration }, scenario.State);
            system.Tick(scenario.State);
            system = new RestockSystem(new[] { configuration }, scenario.State);
            system.Tick(scenario.State);

            Assert.That((scenario.Shop.Stock.Count(Apple), scenario.ProducerStock.Count(Apple)),
                Is.EqualTo((47, 0)));
            Assert.That((scenario.Shop.OwnerWallet.Balance, scenario.ProducerWallet.Balance),
                Is.EqualTo((0, 52)));
            Assert.That(scenario.Shop.Stock.Count(Apple) + scenario.ProducerStock.Count(Apple),
                Is.EqualTo(itemsBefore));
            Assert.That(scenario.Shop.OwnerWallet.Balance + scenario.ProducerWallet.Balance,
                Is.EqualTo(copperBefore));
            Assert.That(scenario.State.Restock.PendingOrders, Is.Empty);
            Assert.That(scenario.State.Restock.TriggeredIds, Is.Empty);
            WorldEvent restocked = scenario.State.Events.Query(type: WorldEventType.Restocked).Single();
            Assert.That((restocked.Actor, restocked.Targets.Single(), restocked.Quantity, restocked.Copper),
                Is.EqualTo(((ActorId?)ActorId.ForNpc(Farmer), ActorId.ForNpc(Shopkeeper), (int?)45, (int?)45)));
        }

        [TestCase(44, 45)]
        [TestCase(45, 44)]
        public void FullOnlyInsufficientStockOrCopperRemainsAtomicAcrossRecreation(
            int producerStock, int shopCopper)
        {
            Scenario scenario = Create(shopStock: 2, shopCopper: shopCopper,
                producerStock: producerStock, producerCopper: 3);
            var configuration = Configuration(scenario);
            var system = new RestockSystem(new[] { configuration }, scenario.State);
            system.Tick(scenario.State);
            system = new RestockSystem(new[] { configuration }, scenario.State);
            system.Tick(scenario.State);

            Assert.That((scenario.Shop.Stock.Count(Apple), scenario.ProducerStock.Count(Apple)),
                Is.EqualTo((2, producerStock)));
            Assert.That((scenario.Shop.OwnerWallet.Balance, scenario.ProducerWallet.Balance),
                Is.EqualTo((shopCopper, 3)));
            Assert.That(scenario.State.Restock.PendingOrders, Has.Count.EqualTo(1));
            Assert.That(scenario.State.Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitPartialPolicyTransfersOnlyAvailableAffordableQuantityOnce()
        {
            Scenario scenario = Create(shopStock: 2, shopCopper: 20, producerStock: 30, producerCopper: 3);
            var configuration = Configuration(scenario, policy: RestockFulfillmentPolicy.AllowPartial);
            var system = new RestockSystem(new[] { configuration }, scenario.State);
            system.Tick(scenario.State);
            system.Tick(scenario.State);

            Assert.That((scenario.Shop.Stock.Count(Apple), scenario.ProducerStock.Count(Apple)),
                Is.EqualTo((22, 10)));
            Assert.That((scenario.Shop.OwnerWallet.Balance, scenario.ProducerWallet.Balance),
                Is.EqualTo((0, 23)));
            Assert.That(scenario.State.Restock.PendingOrders, Is.Empty);
            WorldEvent restocked = scenario.State.Events.Query(type: WorldEventType.Restocked).Single();
            Assert.That((restocked.Quantity, restocked.Copper), Is.EqualTo(((int?)20, (int?)20)));
            Assert.That(scenario.State.Events.Count, Is.EqualTo(2));
        }

        [Test]
        public void RegisteredProductionRunsBeforeRestockingRegardlessOfRegistrationOrder()
        {
            Scenario scenario = Create(shopStock: 2, shopCopper: 45);
            var production = new ProductionConfiguration("production_apples", Farm, Farmer,
                scenario.ProducerStock, Apple, 45, new GameTime(100), EventVisibility.Normal);
            var world = new World(scenario.State);
            world.RegisterSystem(new RestockSystem(new[] { Configuration(scenario) }, scenario.State));
            world.RegisterSystem(new ProductionSystem(new[] { production }, scenario.State));

            world.Tick();

            Assert.That((scenario.Shop.Stock.Count(Apple), scenario.ProducerStock.Count(Apple)),
                Is.EqualTo((47, 0)));
            Assert.That(world.State.Events.Query().Select(entry => entry.Type), Is.EqualTo(new[]
            {
                WorldEventType.Produced, WorldEventType.RestockOrdered, WorldEventType.Restocked
            }));
        }

        [Test]
        public void InputsAndRestoredStateAreValidated()
        {
            Scenario scenario = Create(shopStock: 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => Configuration(scenario, threshold: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Configuration(scenario, wholesaleUnitPrice: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Configuration(scenario,
                policy: (RestockFulfillmentPolicy)99));
            Assert.Throws<ArgumentNullException>(() => new RestockSystem(null, scenario.State));
            Assert.Throws<ArgumentNullException>(() => new RestockSystem(
                new[] { Configuration(scenario) }, null));
            Assert.Throws<ArgumentException>(() => new RestockState(new[] { "route", "route" }));
            var unknownState = new WorldState(42);
            unknownState.RestoreRestock(new RestockState(new[] { "unknown" }));
            Assert.Throws<ArgumentException>(() => new RestockSystem(Array.Empty<RestockConfiguration>(),
                unknownState));
            Assert.That(new RestockSystem(Array.Empty<RestockConfiguration>(),
                scenario.State).Phase, Is.EqualTo(SimulationPhase.Economy));
        }

        private static RestockSystem SystemFor(Scenario scenario, int threshold = 10)
        {
            return new RestockSystem(new[] { Configuration(scenario, threshold) }, scenario.State);
        }

        private static RestockConfiguration Configuration(Scenario scenario, int threshold = 10,
            int orderQuantity = 45, int wholesaleUnitPrice = 1,
            RestockFulfillmentPolicy policy = RestockFulfillmentPolicy.FullOnly)
        {
            return new RestockConfiguration("restock_apples", scenario.Shop, Farm, Farmer,
                scenario.ProducerStock, scenario.ProducerWallet, Apple, threshold, orderQuantity,
                wholesaleUnitPrice, policy, EventVisibility.Normal);
        }

        private static Scenario Create(int shopStock, int shopCopper = 0,
            int producerStock = 0, int producerCopper = 0)
        {
            ItemCatalog catalog = TinyCatalog();
            var state = new WorldState(42, new GameTime(100));
            var shopInventory = new Inventory(catalog);
            if (shopStock > 0) shopInventory.Add(Apple, shopStock);
            var producerInventory = new Inventory(catalog);
            if (producerStock > 0) producerInventory.Add(Apple, producerStock);
            var producerWallet = new Wallet(producerCopper);
            var shop = new Shop(Stall, Shopkeeper, shopInventory, new Wallet(shopCopper),
                new[] { new System.Collections.Generic.KeyValuePair<ItemTypeId, int>(Apple, 3) });
            state.Shops.Register(shop);
            state.Belongings.Register(ActorId.ForNpc(Farmer), producerInventory, producerWallet);
            return new Scenario(shop, producerInventory, producerWallet, state);
        }

        private sealed class Scenario
        {
            public Scenario(Shop shop, Inventory producerStock, Wallet producerWallet, WorldState state)
            {
                Shop = shop;
                ProducerStock = producerStock;
                ProducerWallet = producerWallet;
                State = state;
            }

            public Shop Shop { get; }
            public Inventory ProducerStock { get; }
            public Wallet ProducerWallet { get; }
            public WorldState State { get; }
        }
    }
}
