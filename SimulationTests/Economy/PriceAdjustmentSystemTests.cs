using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;
using static LivingWorld.Simulation.Tests.Economy.ItemCatalogTests;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Proves bounded event-driven price changes with restorable progress.</summary>
    public sealed class PriceAdjustmentSystemTests
    {
        private static readonly LocationId Stall = new LocationId("loc_stall");
        private static readonly LocationId OtherShop = new LocationId("loc_other_shop");
        private static readonly NpcId Shopkeeper = new NpcId("npc_shopkeeper");

        [TestCase(WorldEventType.FailedPurchase)]
        [TestCase(WorldEventType.PartialPurchase)]
        public void MissedSaleRaisesThreeToFourButNeverPastConfiguredMaximum(WorldEventType eventType)
        {
            Scenario scenario = Create(stock: 20, price: 3);
            var configuration = Configuration(scenario.Shop, maximumPrice: 4);
            var system = new PriceAdjustmentSystem(new[] { configuration }, scenario.State);
            MissedSale(scenario.State, Stall, Apple, eventType);

            system.Tick(scenario.State);
            scenario.State.Clock = new GameTime(160);
            MissedSale(scenario.State, Stall, Apple, eventType);
            system.Tick(scenario.State);

            Assert.That(scenario.Shop.UnitPrice(Apple), Is.EqualTo(4));
            Assert.That(scenario.Shop.Prices.Single(pair => pair.Key == Apple).Value, Is.EqualTo(4));
            WorldEvent changed = scenario.State.Events.Query(type: WorldEventType.PriceChanged).Single();
            Assert.That((changed.Location, changed.Actor, changed.ItemType, changed.Copper),
                Is.EqualTo((Stall, (ActorId?)ActorId.ForNpc(Shopkeeper), (ItemTypeId?)Apple, (int?)4)));
            Assert.That(scenario.State.Prices.Progress.Single().CompletedInterval, Is.EqualTo(2));
        }

        [Test]
        public void LowStockRaisesAndSurplusLowersWithinConfiguredBounds()
        {
            Scenario low = Create(stock: 2, price: 3);
            var lowSystem = new PriceAdjustmentSystem(new[] { Configuration(low.Shop, maximumPrice: 4) },
                low.State);
            lowSystem.Tick(low.State);
            low.State.Clock = new GameTime(160);
            lowSystem.Tick(low.State);
            Assert.That(low.Shop.UnitPrice(Apple), Is.EqualTo(4));
            Assert.That(low.State.Events.Query(type: WorldEventType.PriceChanged), Has.Count.EqualTo(1));

            Scenario high = Create(stock: 40, price: 3);
            var highSystem = new PriceAdjustmentSystem(new[] { Configuration(high.Shop, minimumPrice: 2) },
                high.State);
            highSystem.Tick(high.State);
            high.State.Clock = new GameTime(160);
            highSystem.Tick(high.State);
            Assert.That(high.Shop.UnitPrice(Apple), Is.EqualTo(2));
            Assert.That(high.State.Events.Query(type: WorldEventType.PriceChanged), Has.Count.EqualTo(1));
        }

        [Test]
        public void NormalStockWithoutMissedDemandLeavesPriceAndEventsUnchanged()
        {
            Scenario scenario = Create(stock: 20, price: 3);
            var system = new PriceAdjustmentSystem(new[] { Configuration(scenario.Shop) },
                scenario.State);

            system.Tick(scenario.State);

            Assert.That(scenario.Shop.UnitPrice(Apple), Is.EqualTo(3));
            Assert.That(scenario.State.Events.Count, Is.Zero);
            Assert.That(scenario.State.Prices.Progress.Single().CompletedInterval, Is.EqualTo(1));
        }

        [Test]
        public void OtherLocationsAndItemsDoNotCountAsMissedDemand()
        {
            Scenario scenario = Create(stock: 20, price: 3);
            MissedSale(scenario.State, OtherShop, Apple);
            MissedSale(scenario.State, Stall, Bread);
            var system = new PriceAdjustmentSystem(new[] { Configuration(scenario.Shop) },
                scenario.State);

            system.Tick(scenario.State);

            Assert.That(scenario.Shop.UnitPrice(Apple), Is.EqualTo(3));
            Assert.That(scenario.State.Events.Query(type: WorldEventType.PriceChanged), Is.Empty);
            Assert.That(scenario.State.Prices.Progress.Single().LastProcessedEventId, Is.EqualTo(2));
        }

        [Test]
        public void RestoredProgressCarriesDemandForwardAndCannotReplayAnInterval()
        {
            Scenario scenario = Create(stock: 20, price: 3, time: new GameTime(100));
            var configuration = Configuration(scenario.Shop, firstAdjustmentAt: new GameTime(200));
            var system = new PriceAdjustmentSystem(new[] { configuration }, scenario.State);
            MissedSale(scenario.State, Stall, Apple);
            system.Tick(scenario.State);
            Assert.That(scenario.State.Prices.Progress.Single().MissedSalePending, Is.True);

            scenario.State.RestorePrices(new PriceAdjustmentState(scenario.State.Prices.Progress));
            system = new PriceAdjustmentSystem(new[] { configuration }, scenario.State);
            scenario.State.Clock = new GameTime(200);
            system.Tick(scenario.State);
            system = new PriceAdjustmentSystem(new[] { configuration }, scenario.State);
            system.Tick(scenario.State);

            Assert.That(scenario.Shop.UnitPrice(Apple), Is.EqualTo(4));
            Assert.That(scenario.State.Events.Query(type: WorldEventType.PriceChanged), Has.Count.EqualTo(1));
            PriceAdjustmentProgress progress = scenario.State.Prices.Progress.Single();
            Assert.That((progress.CompletedInterval, progress.MissedSalePending), Is.EqualTo((1L, false)));
        }

        [Test]
        public void EventAppendFailureLeavesShopPriceAndProgressUnchanged()
        {
            Scenario scenario = Create(stock: 2, price: 3);
            scenario.State.Events.Append(new GameTime(101), OtherShop, WorldEventType.Conversation);
            var system = new PriceAdjustmentSystem(new[] { Configuration(scenario.Shop) },
                scenario.State);

            Assert.Throws<ArgumentException>(() => system.Tick(scenario.State));

            Assert.That(scenario.Shop.UnitPrice(Apple), Is.EqualTo(3));
            Assert.That(scenario.State.Prices.Progress, Is.Empty);
            Assert.That(scenario.State.Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConfigurationStateAndSystemInputsAreValidated()
        {
            Scenario scenario = Create(stock: 20, price: 3);
            Assert.Throws<ArgumentException>(() => Configuration(scenario.Shop, id: ""));
            Assert.Throws<ArgumentOutOfRangeException>(() => Configuration(scenario.Shop, intervalMinutes: 0));
            Assert.Throws<ArgumentException>(() => Configuration(scenario.Shop,
                lowStockThreshold: 30, highStockThreshold: 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => Configuration(scenario.Shop, priceStep: 0));
            Assert.Throws<ArgumentException>(() => Configuration(scenario.Shop, minimumPrice: 4, maximumPrice: 3));
            Assert.Throws<ArgumentNullException>(() => new PriceAdjustmentSystem(null,
                scenario.State));
            Assert.Throws<ArgumentNullException>(() => new PriceAdjustmentSystem(
                new[] { Configuration(scenario.Shop) }, null));
            Assert.Throws<ArgumentException>(() => new PriceAdjustmentState(new[]
            {
                new PriceAdjustmentProgress("price_apples", 0, 0, false),
                new PriceAdjustmentProgress("price_apples", 1, 1, true)
            }));
        }

        private static PriceAdjustmentConfiguration Configuration(Shop shop, string id = "price_apples",
            GameTime firstAdjustmentAt = default, int intervalMinutes = 60, int lowStockThreshold = 10,
            int highStockThreshold = 30, int priceStep = 1, int minimumPrice = 1, int maximumPrice = 5)
        {
            if (firstAdjustmentAt == default) firstAdjustmentAt = new GameTime(100);
            return new PriceAdjustmentConfiguration(id, shop, Apple, firstAdjustmentAt, intervalMinutes,
                lowStockThreshold, highStockThreshold, priceStep, minimumPrice, maximumPrice,
                EventVisibility.Normal);
        }

        private static Scenario Create(int stock, int price, GameTime time = default)
        {
            ItemCatalog catalog = TinyCatalog();
            var state = new WorldState(42, time == default ? new GameTime(100) : time);
            var inventory = new Inventory(catalog);
            if (stock > 0) inventory.Add(Apple, stock);
            var shop = new Shop(Stall, Shopkeeper, inventory, new Wallet(), new[]
            {
                new KeyValuePair<ItemTypeId, int>(Apple, price),
                new KeyValuePair<ItemTypeId, int>(Bread, 2)
            });
            state.Shops.Register(shop);
            return new Scenario(shop, state);
        }

        private static void MissedSale(WorldState state, LocationId location, ItemTypeId item,
            WorldEventType type = WorldEventType.FailedPurchase)
        {
            state.Events.Append(state.Clock, location, type,
                ActorId.Player, itemType: item, quantity: 0, copper: 0);
        }

        private sealed class Scenario
        {
            public Scenario(Shop shop, WorldState state) { Shop = shop; State = state; }
            public Shop Shop { get; }
            public WorldState State { get; }
        }
    }
}
