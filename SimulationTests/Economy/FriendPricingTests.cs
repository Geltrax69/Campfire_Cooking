using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;
using static LivingWorld.Simulation.Tests.Economy.ItemCatalogTests;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Verifies friend prices: a shopkeeper's discount for a buyer comes from their own
    /// directed trust and affection (neutral pays full price, best friends get 20% off),
    /// the discount is always visible in integer copper, and the price never drops below 1.
    /// </summary>
    public sealed class FriendPricingTests
    {
        private static readonly LocationId Tavern = new LocationId("loc_tavern");
        private static readonly NpcId Bessa = new NpcId("npc_bessa_marlowe");
        private static readonly NpcId Ralf = new NpcId("npc_ralf_hale");
        private static readonly NpcId Stranger = new NpcId("npc_stranger");

        [TestCase(50, 50, 0)]
        [TestCase(60, 55, 3, Description = "Bessa -> Ralf: a copper off, never full price.")]
        [TestCase(100, 100, 20)]
        [TestCase(0, 0, 0)]
        [TestCase(100, 0, 0)]
        [TestCase(75, 75, 10)]
        public void DiscountPercentScalesWithTrustAndAffection(int trust, int affection, int expected)
        {
            Assert.That(FriendPricing.DiscountPercent(trust, affection), Is.EqualTo(expected));
        }

        [TestCase(2, 0, 2)]
        [TestCase(2, 3, 1, Description = "A friend's discount is always visible, even on a 2-copper ale.")]
        [TestCase(1, 20, 1, Description = "The price never drops below 1 copper.")]
        [TestCase(40, 20, 32)]
        [TestCase(100, 99, 80, Description = "The percent is capped at 20.")]
        [TestCase(100, -5, 100, Description = "Negative percents are clamped to zero.")]
        public void DiscountedPriceStaysPositiveAndBounded(int unitPrice, int percent, int expected)
        {
            Assert.That(ShopDiscount.DiscountedPrice(unitPrice, percent), Is.EqualTo(expected));
        }

        [Test]
        public void BessaChargesRalfLessThanAStranger()
        {
            Scenario scenario = Create();
            scenario.Shop.DiscountPolicy = FriendPricing.ForShopkeeper(scenario.World, Bessa);

            PurchaseResult ralf = scenario.Shop.Purchase(scenario.World, scenario.RequestFor(Ralf));
            PurchaseResult stranger = scenario.Shop.Purchase(scenario.World, scenario.RequestFor(Stranger));

            Assert.That(ralf.PaidCopper, Is.EqualTo(1), "Bessa never charges Ralf full price.");
            Assert.That(stranger.PaidCopper, Is.EqualTo(2), "A stranger pays the list price.");
            Assert.That(scenario.RalfWallet.Balance, Is.EqualTo(9));
            Assert.That(scenario.StrangerWallet.Balance, Is.EqualTo(8));
            Assert.That(scenario.Shop.OwnerWallet.Balance, Is.EqualTo(3), "Money is conserved: 1 + 2 in.");
        }

        [Test]
        public void PlayerGetsNoDiscount()
        {
            Scenario scenario = Create();
            scenario.Shop.DiscountPolicy = FriendPricing.ForShopkeeper(scenario.World, Bessa);

            PurchaseResult result = scenario.Shop.Purchase(scenario.World,
                new PurchaseRequest(ActorId.Player, scenario.PlayerStock, scenario.PlayerWallet, Apple, 1, false));

            Assert.That(result.PaidCopper, Is.EqualTo(2));
        }

        [Test]
        public void NoPolicyMeansListPriceForEveryone()
        {
            Scenario scenario = Create();

            PurchaseResult ralf = scenario.Shop.Purchase(scenario.World, scenario.RequestFor(Ralf));

            Assert.That(ralf.PaidCopper, Is.EqualTo(2));
        }

        [Test]
        public void DiscountFlowsIntoThePurchaseEvent()
        {
            Scenario scenario = Create();
            scenario.Shop.DiscountPolicy = FriendPricing.ForShopkeeper(scenario.World, Bessa);

            scenario.Shop.Purchase(scenario.World, scenario.RequestFor(Ralf));

            WorldEvent logged = null;
            foreach (WorldEvent e in scenario.World.Events.Query())
                if (e.Type == WorldEventType.Purchase) logged = e;
            Assert.That(logged, Is.Not.Null);
            Assert.That(logged.Copper, Is.EqualTo(1), "The event records what was actually paid.");
        }

        [Test]
        public void DiscountPolicyValidation()
        {
            var state = new WorldState(42);
            Assert.Throws<System.ArgumentNullException>(() => FriendPricing.ForShopkeeper(null, Bessa));
            Assert.Throws<System.ArgumentException>(() => FriendPricing.ForShopkeeper(state, default));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => FriendPricing.DiscountPercent(-1, 50));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => FriendPricing.DiscountPercent(50, 101));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ShopDiscount.DiscountedPrice(0, 10));
        }

        private static Scenario Create()
        {
            var catalog = TinyCatalog();
            var state = new WorldState(42);
            foreach (NpcId npc in new[] { Bessa, Ralf, Stranger }) state.Knowledge.Register(npc);
            // Bessa -> Ralf: trust 60, affection 55 ("Never charges him full price").
            state.Knowledge.InitializeRelationships(new[]
                { new Relationship(Bessa, Ralf, 60, 55, "Never charges him full price") });

            var stock = new Inventory(catalog);
            stock.Add(Apple, 10);
            var shop = new Shop(Tavern, Bessa, stock, new Wallet(0),
                new[] { new KeyValuePair<ItemTypeId, int>(Apple, 2) });

            var ralfStock = new Inventory(catalog);
            var ralfWallet = new Wallet(10);
            var strangerStock = new Inventory(catalog);
            var strangerWallet = new Wallet(10);
            var playerStock = new Inventory(catalog);
            var playerWallet = new Wallet(10);
            return new Scenario(state, shop,
                ralfStock, ralfWallet, strangerStock, strangerWallet, playerStock, playerWallet);
        }

        private sealed class Scenario
        {
            public Scenario(WorldState world, Shop shop,
                Inventory ralfStock, Wallet ralfWallet, Inventory strangerStock, Wallet strangerWallet,
                Inventory playerStock, Wallet playerWallet)
            {
                World = world; Shop = shop;
                RalfStock = ralfStock; RalfWallet = ralfWallet;
                StrangerStock = strangerStock; StrangerWallet = strangerWallet;
                PlayerStock = playerStock; PlayerWallet = playerWallet;
            }

            public WorldState World { get; }
            public Shop Shop { get; }
            public Inventory RalfStock { get; }
            public Wallet RalfWallet { get; }
            public Inventory StrangerStock { get; }
            public Wallet StrangerWallet { get; }
            public Inventory PlayerStock { get; }
            public Wallet PlayerWallet { get; }

            public PurchaseRequest RequestFor(NpcId buyer) =>
                buyer == Ralf
                    ? new PurchaseRequest(ActorId.ForNpc(Ralf), RalfStock, RalfWallet, Apple, 1, false)
                    : new PurchaseRequest(ActorId.ForNpc(Stranger), StrangerStock, StrangerWallet, Apple, 1, false);
        }
    }
}
