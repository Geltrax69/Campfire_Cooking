using System;
using System.Collections.Generic;
using System.IO;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Checks Bray's general store opens with the seven documented goods at approved prices,
    /// that each good trades through the existing shop buy flow, and that stock survives
    /// save and load.
    /// </summary>
    public sealed class GeneralStoreSetupTests
    {
        // Documented opening stock, mirrored from GeneralStoreSetup's table.
        private static readonly (string Id, int Quantity)[] ExpectedStock =
        {
            ("item_salt", 24),
            ("item_cloth_local", 4),
            ("item_cloth_imported", 6),
            ("item_lamp_oil", 20),
            ("item_nails", 200),
            ("item_rope", 12),
            ("item_tool_basic", 8),
        };

        [Test]
        public void StockingOpensSevenGoodsAtApprovedPrices()
        {
            Shop shop = Stocked(out _, out _, out ItemCatalog catalog);

            Assert.That(shop.Location, Is.EqualTo(GeneralStoreSetup.Store));
            Assert.That(shop.Owner, Is.EqualTo(GeneralStoreSetup.Owner));
            foreach ((string id, int quantity) in ExpectedStock)
            {
                var item = new ItemTypeId(id);
                Assert.That(shop.Stock.Count(item), Is.EqualTo(quantity), "Opening stock of " + id);
                // The price must be the approved baseValue from Content/items/items.json, never
                // an invented number in code.
                Assert.That(shop.UnitPrice(item), Is.EqualTo(catalog[item].BaseValue),
                    "Unit price of " + id);
            }
        }

        [Test]
        public void EachGoodCanBeBoughtAndMoneyIsConserved()
        {
            Shop shop = Stocked(out WorldState world, out Buyer buyer, out ItemCatalog _);
            int totalBefore = buyer.Wallet.Balance + shop.OwnerWallet.Balance;

            foreach ((string id, int _) in ExpectedStock)
            {
                var item = new ItemTypeId(id);
                int before = shop.Stock.Count(item);
                PurchaseResult result = shop.Purchase(world,
                    new PurchaseRequest(ActorId.Player, buyer.Inventory, buyer.Wallet, item, 1, false));

                Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.Full), "Buying " + id);
                Assert.That(result.ActualQuantity, Is.EqualTo(1));
                Assert.That(result.PaidCopper, Is.EqualTo(shop.UnitPrice(item)));
                Assert.That(shop.Stock.Count(item), Is.EqualTo(before - 1));
                Assert.That(buyer.Inventory.Count(item), Is.EqualTo(1));
            }

            Assert.That(buyer.Wallet.Balance + shop.OwnerWallet.Balance, Is.EqualTo(totalBefore),
                "Every copper spent lands in the owner's wallet: nothing is created or destroyed.");
            Assert.That(world.Events.Query().Count, Is.EqualTo(ExpectedStock.Length),
                "One Purchase truth event per good.");
        }

        [TestCase(true, PurchaseOutcome.Partial, 2, 2)]
        [TestCase(false, PurchaseOutcome.Failed, 0, 0)]
        public void WantsSevenNailsWithTwoAvailableHonorsPartialChoice(bool allowPartial,
            PurchaseOutcome outcome, int actual, int paid)
        {
            Shop shop = Stocked(out WorldState world, out Buyer buyer, out ItemCatalog _);
            // Drain the 200 nails down to 2 first (each nail costs 1 copper).
            _ = shop.Purchase(world,
                new PurchaseRequest(ActorId.Player, buyer.Inventory, buyer.Wallet,
                    new ItemTypeId("item_nails"), 198, false));
            Assert.That(shop.Stock.Count(new ItemTypeId("item_nails")), Is.EqualTo(2));

            PurchaseResult result = shop.Purchase(world,
                new PurchaseRequest(ActorId.Player, buyer.Inventory, buyer.Wallet,
                    new ItemTypeId("item_nails"), 7, allowPartial));

            Assert.That((result.Outcome, result.ActualQuantity, result.PaidCopper),
                Is.EqualTo((outcome, actual, paid)));
            Assert.That(shop.Stock.Count(new ItemTypeId("item_nails")), Is.EqualTo(2 - actual));
        }

        [Test]
        public void SaveLoadKeepsStockPricesAndCoinBoxIntact()
        {
            string root = RepositoryRoot();
            Shop shop = Stocked(out WorldState world, out Buyer buyer, out ItemCatalog _, root);
            // Mutate: buy 3 salt and 1 rope, the way a real morning of trade would.
            _ = shop.Purchase(world, new PurchaseRequest(ActorId.Player, buyer.Inventory,
                buyer.Wallet, new ItemTypeId("item_salt"), 3, false));
            _ = shop.Purchase(world, new PurchaseRequest(ActorId.Player, buyer.Inventory,
                buyer.Wallet, new ItemTypeId("item_rope"), 1, false));

            var expectedCounts = new Dictionary<ItemTypeId, int>();
            var expectedPrices = new Dictionary<ItemTypeId, int>();
            foreach ((string id, int _) in ExpectedStock)
            {
                var item = new ItemTypeId(id);
                expectedCounts[item] = shop.Stock.Count(item);
                expectedPrices[item] = shop.UnitPrice(item);
            }
            int expectedCoinBox = shop.OwnerWallet.Balance;

            string json = WorldSaver.Save(world);
            Shop reloaded = WorldLoader.Load(json, root).Shops[GeneralStoreSetup.Store];

            foreach ((string id, int _) in ExpectedStock)
            {
                var item = new ItemTypeId(id);
                Assert.That(reloaded.Stock.Count(item), Is.EqualTo(expectedCounts[item]),
                    "Stock of " + id + " after save/load");
                Assert.That(reloaded.UnitPrice(item), Is.EqualTo(expectedPrices[item]),
                    "Price of " + id + " after save/load");
            }
            Assert.That(reloaded.OwnerWallet.Balance, Is.EqualTo(expectedCoinBox),
                "Owner coin box after save/load");
        }

        private static Shop Stocked(out WorldState world, out Buyer buyer, out ItemCatalog catalog,
            string contentRoot = null)
        {
            catalog = ContentBundle.Load(contentRoot ?? RepositoryRoot()).Catalog;
            world = new WorldState(42);
            Shop shop = GeneralStoreSetup.Stock(world, catalog);
            buyer = new Buyer(new Inventory(catalog), new Wallet(5000));
            return shop;
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }

        private sealed class Buyer
        {
            internal Buyer(Inventory inventory, Wallet wallet)
            {
                Inventory = inventory;
                Wallet = wallet;
            }

            internal Inventory Inventory { get; }
            internal Wallet Wallet { get; }
        }
    }
}
