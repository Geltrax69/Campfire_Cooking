using System;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Proves the whole bakery chain for a day: milling with the toll in kind, the flour
    /// wholesale route, the morning bake, bread selling through the shop, money conservation
    /// and save/load keeping mill/bakery stocks and the oven constraint.
    /// </summary>
    public sealed class BakeryChainTests
    {
        private sealed class Buyer
        {
            internal Buyer(int copper, ItemCatalog catalog)
            {
                Inventory = new Inventory(catalog);
                Wallet = new Wallet(copper);
            }

            internal Inventory Inventory { get; }
            internal Wallet Wallet { get; }
        }

        private static WorldState ChainWorld(out World world, out BakeryChainSetup.BakeryChain chain,
            out ItemCatalog catalog, long startMinutes)
        {
            string root = MillingSystemTests.RepositoryRoot();
            catalog = ContentBundle.Load(root).Catalog;
            var state = new WorldState(99, new GameTime(startMinutes));
            chain = BakeryChainSetup.Stock(state, catalog);
            world = new World(state);
            world.RegisterSystem(new MillingSystem(new[] { chain.Milling() }));
            world.RegisterSystem(new BakingSystem(new[] { chain.Baking() }));
            world.RegisterSystem(new RestockSystem(new[] { chain.FlourRestock() }, state));
            return state;
        }

        private static World Revive(WorldState loaded)
        {
            var bakery = loaded.Shops[BakeryChainSetup.Bakery];
            NpcBelongingsEntry mill = loaded.Belongings[ActorId.ForNpc(BakeryChainSetup.Miller)];
            var chain = new BakeryChainSetup.BakeryChain(bakery, mill.Inventory, mill.Wallet);
            var world = new World(loaded);
            world.RegisterSystem(new MillingSystem(new[] { chain.Milling() }));
            world.RegisterSystem(new BakingSystem(new[] { chain.Baking() }));
            world.RegisterSystem(new RestockSystem(new[] { chain.FlourRestock() }, loaded));
            return world;
        }

        private static void TickWorld(WorldState state, World world, int minutes)
        {
            for (int i = 0; i < minutes; i++) world.Tick();
        }

        private static int ProducedBreadEvents(WorldState state) =>
            state.Events.Query(null, null, BakeryChainSetup.Bakery, WorldEventType.Produced).Count;

        [Test]
        public void SetupStocksMillAndBakeryWithDocumentedValues()
        {
            WorldState state = ChainWorld(out World _, out BakeryChainSetup.BakeryChain chain, out ItemCatalog _, 299);
            Shop bakery = chain.BakeryShop;

            Assert.That((chain.MillStock.Count(BakeryChainSetup.Grain),
                    chain.MillStock.Count(BakeryChainSetup.Flour)),
                Is.EqualTo((30, 6)), "Mill opens with grain to grind and flour to sell.");
            Assert.That((bakery.Stock.Count(BakeryChainSetup.Flour),
                    bakery.Stock.Count(BakeryChainSetup.RyeBread),
                    bakery.Stock.Count(BakeryChainSetup.BarleyBread)),
                Is.EqualTo((4, 0, 0)), "Bakery opens with flour; yesterday's bread sold out.");
            Assert.That((bakery.UnitPrice(BakeryChainSetup.RyeBread),
                    bakery.UnitPrice(BakeryChainSetup.BarleyBread),
                    bakery.UnitPrice(BakeryChainSetup.Flour)),
                Is.EqualTo((3, 2, 10)), "Loaves at baseValue; flour at cost.");
            Assert.That((bakery.OwnerWallet.Balance, chain.MillerWallet.Balance),
                Is.EqualTo((400, 150)));
            Assert.That(state.Belongings[ActorId.ForNpc(BakeryChainSetup.Miller)].Inventory,
                Is.SameAs(chain.MillStock), "The mill stock is Garrick's registered belongings.");
        }

        [Test]
        public void FullChainDayMillsBakesRestocksAndSells()
        {
            WorldState state = ChainWorld(out World world, out BakeryChainSetup.BakeryChain chain, out ItemCatalog catalog, 299);
            var ryeBuyer = new Buyer(500, catalog);
            var barleyBuyer = new Buyer(500, catalog);
            const int totalCopper = 400 + 150 + 500 + 500;

            TickWorld(state, world, 61); // 04:59 -> 06:00.

            // Mill: two batches ground (30 -> 8 grain); the 2 toll sacks stayed as grain.
            Assert.That((chain.MillStock.Count(BakeryChainSetup.Grain),
                    chain.MillStock.Count(BakeryChainSetup.Flour)),
                Is.EqualTo((8, 22)));
            // Bake at 05:00: 4 flour -> 2, then the wholesale order refilled 6 (2 -> 8).
            Assert.That(chain.BakeryShop.Stock.Count(BakeryChainSetup.Flour), Is.EqualTo(8));
            Assert.That((chain.BakeryShop.Stock.Count(BakeryChainSetup.RyeBread),
                    chain.BakeryShop.Stock.Count(BakeryChainSetup.BarleyBread)),
                Is.EqualTo((20, 20)));
            // Flour money moved mill-ward: 6 sacks at 10 copper.
            Assert.That((chain.BakeryShop.OwnerWallet.Balance, chain.MillerWallet.Balance),
                Is.EqualTo((340, 210)));

            // Villagers buy the whole bake through the existing shop flow.
            PurchaseResult rye = chain.BakeryShop.Purchase(state, new PurchaseRequest(ActorId.Player,
                ryeBuyer.Inventory, ryeBuyer.Wallet, BakeryChainSetup.RyeBread, 20, false));
            PurchaseResult barley = chain.BakeryShop.Purchase(state, new PurchaseRequest(ActorId.Player,
                barleyBuyer.Inventory, barleyBuyer.Wallet, BakeryChainSetup.BarleyBread, 20, false));
            Assert.That((rye.Outcome, barley.Outcome),
                Is.EqualTo((PurchaseOutcome.Full, PurchaseOutcome.Full)));

            TickWorld(state, world, 18 * 60 - 61); // tick out the rest of the day.

            // The oven stayed cold: no second bake even though flour remained.
            Assert.That((chain.BakeryShop.Stock.Count(BakeryChainSetup.RyeBread),
                    chain.BakeryShop.Stock.Count(BakeryChainSetup.BarleyBread)),
                Is.EqualTo((0, 0)));
            Assert.That(ProducedBreadEvents(state), Is.EqualTo(2));

            int copper = chain.BakeryShop.OwnerWallet.Balance + chain.MillerWallet.Balance
                + ryeBuyer.Wallet.Balance + barleyBuyer.Wallet.Balance;
            Assert.That(copper, Is.EqualTo(totalCopper),
                "Every copper is tracked: bread sales and flour orders are pure transfers.");
        }

        [Test]
        public void SaveLoadKeepsStocksAndTheColdOven()
        {
            string root = MillingSystemTests.RepositoryRoot();
            WorldState state = ChainWorld(out World world, out BakeryChainSetup.BakeryChain _, out ItemCatalog _, 299);
            TickWorld(state, world, 61); // 04:59 -> 06:00: milled, baked, flour restocked.

            string json = WorldSaver.Save(state);
            WorldState loaded = WorldLoader.Load(json, root);
            Shop bakery = loaded.Shops[BakeryChainSetup.Bakery];
            NpcBelongingsEntry mill = loaded.Belongings[ActorId.ForNpc(BakeryChainSetup.Miller)];

            Assert.That((mill.Inventory.Count(BakeryChainSetup.Grain),
                    mill.Inventory.Count(BakeryChainSetup.Flour)),
                Is.EqualTo((8, 22)), "Mill stocks survive save/load.");
            Assert.That((bakery.Stock.Count(BakeryChainSetup.Flour),
                    bakery.Stock.Count(BakeryChainSetup.RyeBread),
                    bakery.Stock.Count(BakeryChainSetup.BarleyBread)),
                Is.EqualTo((8, 20, 20)), "Bakery stocks survive save/load.");
            Assert.That((bakery.OwnerWallet.Balance, mill.Wallet.Balance),
                Is.EqualTo((340, 210)), "Tills survive save/load.");

            World revived = Revive(loaded);
            int breadEventsBefore = ProducedBreadEvents(loaded);
            TickWorld(loaded, revived, 6 * 60); // 06:00 -> 12:00.

            Assert.That(ProducedBreadEvents(loaded), Is.EqualTo(breadEventsBefore),
                "The reloaded world remembers the morning bake: no second bake.");
            Assert.That((bakery.Stock.Count(BakeryChainSetup.RyeBread),
                    bakery.Stock.Count(BakeryChainSetup.BarleyBread)),
                Is.EqualTo((20, 20)));
        }

        [Test]
        public void SaveBeforeBakeStillBakesExactlyOnceAfterLoad()
        {
            string root = MillingSystemTests.RepositoryRoot();
            WorldState state = ChainWorld(out World _, out BakeryChainSetup.BakeryChain _, out ItemCatalog _, 4 * 60 + 30);

            string json = WorldSaver.Save(state); // 04:30, before the bake.
            WorldState loaded = WorldLoader.Load(json, root);
            World revived = Revive(loaded);
            TickWorld(loaded, revived, 90); // 04:30 -> 06:00.

            Shop bakery = loaded.Shops[BakeryChainSetup.Bakery];
            Assert.That(ProducedBreadEvents(loaded), Is.EqualTo(2),
                "Exactly one bake fired after load.");
            Assert.That((bakery.Stock.Count(BakeryChainSetup.RyeBread),
                    bakery.Stock.Count(BakeryChainSetup.BarleyBread)),
                Is.EqualTo((20, 20)));
            Assert.That(bakery.Stock.Count(BakeryChainSetup.Flour), Is.EqualTo(8),
                "Bake consumed 2 of 4 sacks, then the wholesale order refilled 6.");
        }
    }
}
