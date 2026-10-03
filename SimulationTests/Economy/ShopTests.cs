using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;
using static LivingWorld.Simulation.Tests.Economy.ItemCatalogTests;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Checks atomic full, partial and failed shop purchases and their truth events.</summary>
    public sealed class ShopTests
    {
        private static readonly LocationId Stall = new LocationId("loc_apple_stall");
        private static readonly NpcId Mira = new NpcId("npc_mira_holt");
        private static readonly NpcId Tom = new NpcId("npc_tom_fenn");

        [Test]
        public void FullPurchaseMovesExactStockAndCopperAndLogsTruth()
        {
            Scenario scenario = Create(stock: 20, buyerCopper: 100, sellerCopper: 10);
            PurchaseResult result = scenario.Shop.Purchase(scenario.World,
                new PurchaseRequest(ActorId.ForNpc(Tom), scenario.BuyerStock, scenario.BuyerWallet, Apple, 5, false));

            Assert.That((result.Outcome, result.RequestedQuantity, result.ActualQuantity, result.PaidCopper),
                Is.EqualTo((PurchaseOutcome.Full, 5, 5, 15)));
            Assert.That((scenario.Shop.Stock.Count(Apple), scenario.BuyerStock.Count(Apple)), Is.EqualTo((15, 5)));
            Assert.That((scenario.BuyerWallet.Balance, scenario.Shop.OwnerWallet.Balance), Is.EqualTo((85, 25)));
            AssertEvent(scenario.World.Events.Query().Single(), WorldEventType.Purchase, 5, 15);
        }

        [TestCase(true, PurchaseOutcome.Partial, 2, 6, WorldEventType.PartialPurchase)]
        [TestCase(false, PurchaseOutcome.Failed, 0, 0, WorldEventType.FailedPurchase)]
        public void WantsSevenWithTwoAvailableHonorsPartialChoice(bool partial, PurchaseOutcome outcome,
            int actual, int copper, WorldEventType eventType)
        {
            Scenario scenario = Create(stock: 2, buyerCopper: 100);
            PurchaseResult result = scenario.Shop.Purchase(scenario.World,
                new PurchaseRequest(ActorId.Player, scenario.BuyerStock, scenario.BuyerWallet, Apple, 7, partial));

            Assert.That((result.Outcome, result.RequestedQuantity, result.ActualQuantity, result.PaidCopper),
                Is.EqualTo((outcome, 7, actual, copper)));
            Assert.That((scenario.Shop.Stock.Count(Apple), scenario.BuyerStock.Count(Apple)), Is.EqualTo((2 - actual, actual)));
            Assert.That((scenario.BuyerWallet.Balance, scenario.Shop.OwnerWallet.Balance), Is.EqualTo((100 - copper, copper)));
            AssertEvent(scenario.World.Events.Query().Single(), eventType, actual, copper, ActorId.Player);
        }

        [TestCase(true, PurchaseOutcome.Partial, 2, 6)]
        [TestCase(false, PurchaseOutcome.Failed, 0, 0)]
        public void InsufficientFundsPartiallyFillOrFailWithoutCredit(bool partial, PurchaseOutcome outcome, int actual, int paid)
        {
            Scenario scenario = Create(stock: 20, buyerCopper: 8);
            PurchaseResult result = scenario.Shop.Purchase(scenario.World,
                new PurchaseRequest(ActorId.ForNpc(Tom), scenario.BuyerStock, scenario.BuyerWallet, Apple, 5, partial));
            Assert.That((result.Outcome, result.ActualQuantity, result.PaidCopper), Is.EqualTo((outcome, actual, paid)));
            Assert.That((scenario.BuyerWallet.Balance, scenario.Shop.OwnerWallet.Balance), Is.EqualTo((8 - paid, paid)));
            Assert.That((scenario.Shop.Stock.Count(Apple), scenario.BuyerStock.Count(Apple)), Is.EqualTo((20 - actual, actual)));
        }

        [Test]
        public void InvalidOrIncompatiblePurchaseInputsAreAtomic()
        {
            Scenario scenario = Create();
            Assert.Throws<ArgumentNullException>(() => scenario.Shop.Purchase(null,
                new PurchaseRequest(ActorId.Player, scenario.BuyerStock, scenario.BuyerWallet, Apple, 1, false)));
            Assert.Throws<ArgumentNullException>(() => scenario.Shop.Purchase(scenario.World, null));
            Assert.Throws<ArgumentException>(() => new PurchaseRequest(default, scenario.BuyerStock, scenario.BuyerWallet, Apple, 1, false));
            Assert.Throws<ArgumentNullException>(() => new PurchaseRequest(ActorId.Player, null, scenario.BuyerWallet, Apple, 1, false));
            Assert.Throws<ArgumentNullException>(() => new PurchaseRequest(ActorId.Player, scenario.BuyerStock, null, Apple, 1, false));
            Assert.Throws<ArgumentException>(() => new PurchaseRequest(ActorId.Player, scenario.BuyerStock, scenario.BuyerWallet, default, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseRequest(ActorId.Player, scenario.BuyerStock, scenario.BuyerWallet, Apple, 0, false));
            Assert.Throws<ArgumentException>(() => scenario.Shop.Purchase(scenario.World,
                new PurchaseRequest(ActorId.Player, scenario.BuyerStock, scenario.BuyerWallet,
                    new ItemTypeId("item_unpriced"), 1, false)));
            var incompatible = new Inventory(new ItemCatalog(new[] { Define(Bread) }));
            Assert.Throws<ArgumentException>(() => scenario.Shop.Purchase(scenario.World,
                new PurchaseRequest(ActorId.Player, incompatible, scenario.BuyerWallet, Apple, 1, false)));
            AssertUnchanged(scenario, 20, 100, 0);
        }

        [Test]
        public void CapacitySellerOverflowAndLogFailureLeaveAllAccountsUnchanged()
        {
            Scenario capacity = Create(stock: 2);
            capacity.BuyerStock.Add(Apple, int.MaxValue);
            Assert.Throws<OverflowException>(() => capacity.Shop.Purchase(capacity.World,
                new PurchaseRequest(ActorId.Player, capacity.BuyerStock, capacity.BuyerWallet, Apple, 1, true)));
            AssertUnchanged(capacity, 2, 100, 0, int.MaxValue);

            Scenario seller = Create(stock: 2, sellerCopper: int.MaxValue);
            Assert.Throws<OverflowException>(() => seller.Shop.Purchase(seller.World,
                new PurchaseRequest(ActorId.Player, seller.BuyerStock, seller.BuyerWallet, Apple, 1, true)));
            AssertUnchanged(seller, 2, 100, int.MaxValue);

            Scenario logging = Create(stock: 2);
            logging.World.Events.Append(new GameTime(10), Stall, WorldEventType.Conversation);
            Assert.Throws<ArgumentException>(() => logging.Shop.Purchase(logging.World,
                new PurchaseRequest(ActorId.Player, logging.BuyerStock, logging.BuyerWallet, Apple, 1, true)));
            AssertUnchanged(logging, 2, 100, 0);
            Assert.That(logging.World.Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void TradeConservesTotalsAndSelfOwnedAccountsRemainSafe()
        {
            Scenario scenario = Create(stock: 20, buyerCopper: 100, sellerCopper: 50);
            int itemsBefore = scenario.Shop.Stock.Count(Apple) + scenario.BuyerStock.Count(Apple);
            int copperBefore = scenario.BuyerWallet.Balance + scenario.Shop.OwnerWallet.Balance;
            scenario.Shop.Purchase(scenario.World,
                new PurchaseRequest(ActorId.ForNpc(Tom), scenario.BuyerStock, scenario.BuyerWallet, Apple, 7, false));
            Assert.That(scenario.Shop.Stock.Count(Apple) + scenario.BuyerStock.Count(Apple), Is.EqualTo(itemsBefore));
            Assert.That(scenario.BuyerWallet.Balance + scenario.Shop.OwnerWallet.Balance, Is.EqualTo(copperBefore));

            var self = new Shop(Stall, Mira, scenario.Shop.Stock, scenario.Shop.OwnerWallet,
                new[] { new KeyValuePair<ItemTypeId, int>(Apple, 3) });
            int stock = self.Stock.Count(Apple), copper = self.OwnerWallet.Balance;
            PurchaseResult result = self.Purchase(scenario.World,
                new PurchaseRequest(ActorId.ForNpc(Mira), self.Stock, self.OwnerWallet, Apple, 1, false));
            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.Full));
            Assert.That((self.Stock.Count(Apple), self.OwnerWallet.Balance), Is.EqualTo((stock, copper)));
        }

        [Test]
        public void ShopValidationPricesAndRepeatedRunsAreDeterministic()
        {
            Scenario first = Create();
            Scenario second = Create();
            Assert.That(first.Shop.Location, Is.EqualTo(Stall));
            Assert.That(first.Shop.Owner, Is.EqualTo(Mira));
            Assert.That(first.Shop.Prices.Select(pair => pair.Key), Is.EqualTo(new[] { Apple, Bread }));
            Assert.Throws<NotSupportedException>(() => ((IList<KeyValuePair<ItemTypeId, int>>)first.Shop.Prices).Clear());
            Assert.That(first.Shop.UnitPrice(Apple), Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => first.Shop.UnitPrice(new ItemTypeId("unknown")));
            Assert.Throws<ArgumentException>(() => NewShop(default, Mira, new[] { Price(Apple, 3) }));
            Assert.Throws<ArgumentException>(() => NewShop(Stall, default, new[] { Price(Apple, 3) }));
            Assert.Throws<ArgumentNullException>(() => new Shop(Stall, Mira, null, new Wallet(), new[] { Price(Apple, 3) }));
            Assert.Throws<ArgumentNullException>(() => new Shop(Stall, Mira, new Inventory(TinyCatalog()), null, new[] { Price(Apple, 3) }));
            Assert.Throws<ArgumentNullException>(() => NewShop(Stall, Mira, null));
            Assert.Throws<ArgumentException>(() => NewShop(Stall, Mira, new[] { Price(Apple, 3), Price(Apple, 4) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => NewShop(Stall, Mira, new[] { Price(Apple, 0) }));
            Assert.Throws<ArgumentException>(() => NewShop(Stall, Mira, new[] { Price(new ItemTypeId("unknown"), 3) }));

            PurchaseRequest request1 = new PurchaseRequest(ActorId.Player, first.BuyerStock, first.BuyerWallet, Apple, 7, true);
            PurchaseRequest request2 = new PurchaseRequest(ActorId.Player, second.BuyerStock, second.BuyerWallet, Apple, 7, true);
            PurchaseResult result1 = first.Shop.Purchase(first.World, request1);
            PurchaseResult result2 = second.Shop.Purchase(second.World, request2);
            Assert.That((result1.Outcome, result1.ActualQuantity, result1.PaidCopper),
                Is.EqualTo((result2.Outcome, result2.ActualQuantity, result2.PaidCopper)));
            Assert.That(Trace(first.World), Is.EqualTo(Trace(second.World)));
        }

        private static Scenario Create(int stock = 20, int buyerCopper = 100, int sellerCopper = 0)
        {
            var catalog = TinyCatalog();
            var world = new WorldState(42);
            var sellerStock = new Inventory(catalog);
            sellerStock.Add(Apple, stock);
            var sellerWallet = new Wallet(sellerCopper);
            var shop = new Shop(Stall, Mira, sellerStock, sellerWallet, new[] { Price(Bread, 3), Price(Apple, 3) });
            world.Shops.Register(shop);
            var buyerStock = new Inventory(catalog);
            var buyerWallet = new Wallet(buyerCopper);
            world.Belongings.Register(ActorId.ForNpc(Tom), buyerStock, buyerWallet);
            return new Scenario(shop, buyerStock, buyerWallet, world);
        }

        private static Shop NewShop(LocationId location, NpcId owner, IEnumerable<KeyValuePair<ItemTypeId, int>> prices)
        {
            return new Shop(location, owner, new Inventory(TinyCatalog()), new Wallet(), prices);
        }

        private static KeyValuePair<ItemTypeId, int> Price(ItemTypeId item, int copper) =>
            new KeyValuePair<ItemTypeId, int>(item, copper);

        private static void AssertEvent(WorldEvent entry, WorldEventType type, int quantity, int copper,
            ActorId? buyer = null)
        {
            Assert.That((entry.Time, entry.Location, entry.Type, entry.Actor, entry.Visibility, entry.ItemType, entry.Quantity, entry.Copper),
                Is.EqualTo((default(GameTime), Stall, type, buyer ?? ActorId.ForNpc(Tom), EventVisibility.Normal,
                    (ItemTypeId?)Apple, (int?)quantity, (int?)copper)));
            Assert.That(entry.Targets, Is.EqualTo(new[] { ActorId.ForNpc(Mira) }));
        }

        private static void AssertUnchanged(Scenario scenario, int shopStock, int buyerCopper, int sellerCopper,
            int buyerStock = 0)
        {
            Assert.That((scenario.Shop.Stock.Count(Apple), scenario.BuyerStock.Count(Apple)), Is.EqualTo((shopStock, buyerStock)));
            Assert.That((scenario.BuyerWallet.Balance, scenario.Shop.OwnerWallet.Balance), Is.EqualTo((buyerCopper, sellerCopper)));
        }

        private static string Trace(WorldState state) => string.Join("|", state.Events.Query().Select(entry =>
            $"{entry.Id.Value}:{entry.Time.TotalMinutes}:{entry.Type}:{entry.Actor.Value.IsPlayer}:{entry.Location}:{entry.ItemType}:{entry.Quantity}:{entry.Copper}"));

        private sealed class Scenario
        {
            public Shop Shop { get; }
            public Inventory BuyerStock { get; }
            public Wallet BuyerWallet { get; }
            public WorldState World { get; }
            public Scenario(Shop shop, Inventory buyerStock, Wallet buyerWallet, WorldState world)
            { Shop = shop; BuyerStock = buyerStock; BuyerWallet = buyerWallet; World = world; }
        }
    }
}
