using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;
using static LivingWorld.Simulation.Tests.Economy.ItemCatalogTests;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Checks inter-village trade routes: route/registry validation, per-village
    /// prices, merchant journeys (buy at origin, travel, sell at destination),
    /// profit arithmetic and travel-time behavior. All deterministic on a fixed seed.
    /// </summary>
    public sealed class TradeRouteTests
    {
        private static readonly VillageId Millbrook = VillageFactory.Millbrook;
        private static readonly VillageId KingsRest = new VillageId("village_kings_rest");
        private static readonly TradeRouteId AppleRouteId = new TradeRouteId("route_millbrook_kings_rest");
        private static readonly LocationId Stall = new LocationId("loc_apple_stall");
        private static readonly NpcId Mira = new NpcId("npc_mira_holt");

        private const int MillbrookBuyPrice = 2;
        private const int KingsRestSellPrice = 5;
        private const int TravelCost = 5;

        [Test]
        public void GoodsFlowAlongRoutes()
        {
            Scenario scenario = Create();
            scenario.System.Tick(scenario.World);

            // One merchant departed on day 1 with a non-empty apple cargo.
            MerchantJourney journey = scenario.Ledger.Journeys.Single();
            Assert.That(journey.RouteId, Is.EqualTo(AppleRouteId));
            Assert.That((journey.DepartureDay, journey.ArrivalDay), Is.EqualTo((1L, 3L)));
            int units = journey.Cargo.Single().Value;
            Assert.That(units, Is.GreaterThan(0));

            // Bought from Mira's shop at the offer price; till credited from outside.
            Assert.That(scenario.Stock.Count(Apple), Is.EqualTo(20 - units));
            Assert.That(scenario.Till.Balance, Is.EqualTo(units * MillbrookBuyPrice));
            Assert.That(journey.BoughtCopper, Is.EqualTo(units * MillbrookBuyPrice));

            WorldEvent departed = scenario.World.Events.Query(type: WorldEventType.MerchantDeparted).Single();
            Assert.That((departed.Quantity, departed.Copper), Is.EqualTo(((int?)units, (int?)(units * MillbrookBuyPrice))));

            // King's Rest wealth is untouched until the merchant arrives.
            Assert.That(scenario.World.Villages[KingsRest].WealthCopper, Is.EqualTo(200000));

            // Day 3: the merchant arrives and sells at King's Rest prices.
            scenario.World.Clock = scenario.World.Clock.Advance(2 * 1440);
            scenario.System.Tick(scenario.World);

            Assert.That(journey.IsComplete, Is.True);
            Assert.That(journey.SoldCopper, Is.EqualTo(units * KingsRestSellPrice));
            Assert.That(scenario.World.Villages[KingsRest].WealthCopper,
                Is.EqualTo(200000 - units * KingsRestSellPrice));
            WorldEvent arrived = scenario.World.Events.Query(type: WorldEventType.MerchantArrived).Single();
            Assert.That((arrived.Quantity, arrived.Copper), Is.EqualTo(((int?)units, (int?)(units * KingsRestSellPrice))));
        }

        [Test]
        public void PricesDifferByVillage()
        {
            var prices = new VillageTradePrices();
            prices.SetPrice(KingsRest, Apple, KingsRestSellPrice);
            prices.SetPrice(Millbrook, Apple, 3);
            Assert.That(prices.GetPrice(KingsRest, Apple), Is.EqualTo(5));
            Assert.That(prices.GetPrice(Millbrook, Apple), Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => prices.GetPrice(KingsRest, Bread));
            Assert.Throws<ArgumentException>(() => prices.GetPrice(new VillageId("village_nowhere"), Apple));
        }

        [Test]
        public void MerchantProfit()
        {
            var cargo = new[] { new KeyValuePair<ItemTypeId, int>(Apple, 10) };
            var profitable = new MerchantJourney(AppleRouteId, cargo, departureDay: 1, arrivalDay: 3, boughtCopper: 20);
            profitable.MarkArrived(50);
            Assert.That(profitable.ProfitCopper(TravelCost), Is.EqualTo(25));

            var loss = new MerchantJourney(AppleRouteId, cargo, departureDay: 1, arrivalDay: 3, boughtCopper: 60);
            loss.MarkArrived(50);
            Assert.That(loss.ProfitCopper(TravelCost), Is.EqualTo(-15));
        }

        [Test]
        public void TradeTakesTime()
        {
            Scenario scenario = Create(departureIntervalDays: 1000);
            scenario.System.Tick(scenario.World);

            MerchantJourney journey = scenario.Ledger.Journeys.Single();
            Assert.That(journey.ArrivalDay, Is.EqualTo(3L));
            Assert.That(scenario.World.Events.Query(type: WorldEventType.MerchantArrived), Is.Empty);

            // Day 2: still on the road, no arrival.
            scenario.World.Clock = scenario.World.Clock.Advance(1440);
            scenario.System.Tick(scenario.World);
            Assert.That(journey.IsComplete, Is.False);
            Assert.That(scenario.World.Events.Query(type: WorldEventType.MerchantArrived), Is.Empty);

            // Day 3: arrives; the huge interval means no second departure.
            scenario.World.Clock = scenario.World.Clock.Advance(1440);
            scenario.System.Tick(scenario.World);
            Assert.That(journey.IsComplete, Is.True);
            Assert.That(scenario.World.Events.Query(type: WorldEventType.MerchantArrived).Count, Is.EqualTo(1));
            Assert.That(scenario.Ledger.Journeys.Count, Is.EqualTo(1));
        }

        [Test]
        public void InboundCargoSellsToMillbrookShops()
        {
            var world = new WorldState(7);
            RegisterVillages(world);
            var oakhollow = new VillageId("village_oakhollow");
            var routeId = new TradeRouteId("route_oakhollow_millbrook");
            var routes = new TradeRouteRegistry();
            routes.Register(new TradeRoute(routeId, oakhollow, Millbrook, travelDays: 1, new[] { Bread }));
            var prices = new VillageTradePrices();
            prices.SetPrice(oakhollow, Bread, 2);
            var policy = new TradeRoutePolicy(departureIntervalDays: 1000, maxUnitsPerGood: 10, travelCostCopper: 1);
            var ledger = new TradeRouteLedger();

            var catalog = TinyCatalog();
            var buyerStock = new Inventory(catalog);
            var buyerTill = new Wallet(100);
            var importOffer = new MerchantImportOffer(Mira, buyerStock, buyerTill, Bread,
                targetStock: 10, sellPriceCopper: 4, location: Stall);
            var system = new TradeRouteSystem(world.Villages, routes, prices, policy, ledger,
                millbrookImportOffers: new[] { importOffer });

            system.Tick(world);
            MerchantJourney journey = ledger.Journeys.Single();
            int units = journey.Cargo.Single().Value;
            // Oakhollow sold bread to the merchant: its wealth rose at the origin price.
            Assert.That(world.Villages[oakhollow].WealthCopper, Is.EqualTo(8000 + units * 2));

            world.Clock = world.Clock.Advance(1440);
            system.Tick(world);
            int sold = Math.Min(units, Math.Min(10, 100 / 4));
            Assert.That(buyerStock.Count(Bread), Is.EqualTo(sold));
            Assert.That(buyerTill.Balance, Is.EqualTo(100 - sold * 4));
            Assert.That(journey.IsComplete, Is.True);
        }

        [Test]
        public void RouteValidationRejectsBadDefinitions()
        {
            var goods = new[] { Apple };
            Assert.Throws<ArgumentException>(() => new TradeRoute(default, Millbrook, KingsRest, 2, goods));
            Assert.Throws<ArgumentException>(() => new TradeRoute(AppleRouteId, Millbrook, Millbrook, 2, goods));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TradeRoute(AppleRouteId, Millbrook, KingsRest, 0, goods));
            Assert.Throws<ArgumentException>(() => new TradeRoute(AppleRouteId, Millbrook, KingsRest, 2, new ItemTypeId[0]));
            Assert.Throws<ArgumentException>(() => new TradeRoute(AppleRouteId, Millbrook, KingsRest, 2, null));
            Assert.Throws<ArgumentException>(() => new TradeRoute(AppleRouteId, Millbrook, KingsRest, 2, new[] { Apple, Apple }));
            Assert.Throws<ArgumentException>(() => new TradeRoute(AppleRouteId, Millbrook, KingsRest, 2, new[] { default(ItemTypeId) }));
        }

        [Test]
        public void RegistryValidationAndQueries()
        {
            var registry = new TradeRouteRegistry();
            var route = new TradeRoute(AppleRouteId, Millbrook, KingsRest, 2, new[] { Apple });
            registry.Register(route);
            Assert.Throws<ArgumentException>(() => registry.Register(route));
            Assert.That(registry.Get(AppleRouteId), Is.SameAs(route));
            Assert.Throws<ArgumentException>(() => registry.Get(new TradeRouteId("route_missing")));
            Assert.That(registry.GetFrom(Millbrook), Is.EqualTo(new[] { route }));
            Assert.That(registry.GetTo(KingsRest), Is.EqualTo(new[] { route }));
            Assert.That(registry.GetFrom(KingsRest), Is.Empty);
            Assert.That(registry.GetTo(Millbrook), Is.Empty);
        }

        [Test]
        public void PolicyAndPriceValidation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TradeRoutePolicy(0, 10, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TradeRoutePolicy(7, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TradeRoutePolicy(7, 10, -1));
            var prices = new VillageTradePrices();
            Assert.Throws<ArgumentOutOfRangeException>(() => prices.SetPrice(KingsRest, Apple, 0));
            Assert.Throws<ArgumentException>(() => prices.SetPrice(default, Apple, 5));
        }

        [Test]
        public void JourneyValidation()
        {
            var cargo = new[] { new KeyValuePair<ItemTypeId, int>(Apple, 3) };
            Assert.Throws<ArgumentException>(() => new MerchantJourney(default, cargo, 1, 3, 6));
            Assert.Throws<ArgumentException>(() => new MerchantJourney(AppleRouteId, new KeyValuePair<ItemTypeId, int>[0], 1, 3, 0));
            Assert.Throws<ArgumentException>(() => new MerchantJourney(AppleRouteId, cargo, 3, 3, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MerchantJourney(AppleRouteId, cargo, 1, 3, -1));
            var journey = new MerchantJourney(AppleRouteId, cargo, 1, 3, 6);
            Assert.Throws<InvalidOperationException>(() => journey.ProfitCopper(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => journey.MarkArrived(-1));
            journey.MarkArrived(15);
            Assert.Throws<InvalidOperationException>(() => journey.MarkArrived(15));
        }

        [Test]
        public void SystemRequiresPricesForAbstractEndpoints()
        {
            var world = new WorldState(42);
            RegisterVillages(world);
            var routes = new TradeRouteRegistry();
            routes.Register(new TradeRoute(AppleRouteId, Millbrook, KingsRest, 2, new[] { Apple }));
            var policy = new TradeRoutePolicy(7, 10, 1);
            // No price for King's Rest: the destination is abstract, so this must fail.
            Assert.Throws<ArgumentException>(() => new TradeRouteSystem(world.Villages, routes,
                new VillageTradePrices(), policy, new TradeRouteLedger()));
            // Unknown village in a route must fail too.
            var badRoutes = new TradeRouteRegistry();
            badRoutes.Register(new TradeRoute(new TradeRouteId("route_bad"),
                Millbrook, new VillageId("village_nowhere"), 2, new[] { Apple }));
            var prices = new VillageTradePrices();
            prices.SetPrice(KingsRest, Apple, 5);
            Assert.Throws<ArgumentException>(() => new TradeRouteSystem(world.Villages, badRoutes,
                prices, policy, new TradeRouteLedger()));
        }

        [Test]
        public void LedgerCaptureRestoreRoundTrip()
        {
            Scenario scenario = Create();
            scenario.System.Tick(scenario.World);
            TradeRouteLedgerSnapshot snapshot = scenario.Ledger.Capture();
            var restored = new TradeRouteLedger();
            restored.Restore(snapshot);
            Assert.That(restored.Journeys.Count, Is.EqualTo(1));
            MerchantJourney journey = restored.Journeys.Single();
            Assert.That((journey.RouteId, journey.DepartureDay, journey.ArrivalDay, journey.BoughtCopper),
                Is.EqualTo((AppleRouteId, 1L, 3L, scenario.Ledger.Journeys.Single().BoughtCopper)));
            // A corrupt snapshot must not mutate the ledger.
            Assert.Throws<ArgumentNullException>(() => restored.Restore(null));
            Assert.That(restored.Journeys.Count, Is.EqualTo(1));
        }

        private static void RegisterVillages(WorldState state)
        {
            foreach (AbstractVillageState village in VillageFactory.CreateInitialVillages().GetAll())
                state.Villages.Register(village);
        }

        private static Scenario Create(int departureIntervalDays = 100)
        {
            var catalog = TinyCatalog();
            var world = new WorldState(42);
            RegisterVillages(world);

            var routes = new TradeRouteRegistry();
            routes.Register(new TradeRoute(AppleRouteId, Millbrook, KingsRest, travelDays: 2, new[] { Apple }));
            var prices = new VillageTradePrices();
            prices.SetPrice(KingsRest, Apple, KingsRestSellPrice);
            prices.SetPrice(Millbrook, Apple, 3);
            var policy = new TradeRoutePolicy(departureIntervalDays, maxUnitsPerGood: 10, travelCostCopper: TravelCost);
            var ledger = new TradeRouteLedger();

            var stock = new Inventory(catalog);
            stock.Add(Apple, 20);
            var till = new Wallet(0);
            var offer = new MerchantPurchaseOffer(Mira, stock, till, Apple,
                buyPriceCopper: MillbrookBuyPrice, maxUnitsPerVisit: 10, location: Stall);
            var system = new TradeRouteSystem(world.Villages, routes, prices, policy, ledger,
                millbrookPurchaseOffers: new[] { offer });
            return new Scenario(world, system, ledger, stock, till);
        }

        private sealed class Scenario
        {
            public Scenario(WorldState world, TradeRouteSystem system, TradeRouteLedger ledger,
                Inventory stock, Wallet till)
            {
                World = world;
                System = system;
                Ledger = ledger;
                Stock = stock;
                Till = till;
            }

            public WorldState World { get; }
            public TradeRouteSystem System { get; }
            public TradeRouteLedger Ledger { get; }
            public Inventory Stock { get; }
            public Wallet Till { get; }
        }
    }
}
