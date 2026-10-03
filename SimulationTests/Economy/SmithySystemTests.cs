using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Proves the forge: make-to-stock production consuming exact iron, money conservation
    /// across the full produce-and-sell chain, a single iron-exhaustion order, determinism,
    /// and save/load keeping iron, till and progress.
    /// </summary>
    public sealed class SmithySystemTests
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

        private static WorldState SmithyWorld(out World world, out SmithySetup.SmithyHandle smithy,
            out ItemCatalog catalog, ulong seed)
        {
            catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(seed, new GameTime(0));
            smithy = SmithySetup.Stock(state, catalog);
            world = new World(state);
            world.RegisterSystem(new SmithySystem(new[] { smithy.Forging() }));
            return state;
        }

        private static World Revive(WorldState loaded)
        {
            var handle = new SmithySetup.SmithyHandle(loaded.Shops[SmithySetup.Smithy]);
            var world = new World(loaded);
            world.RegisterSystem(new SmithySystem(new[] { handle.Forging() }));
            return world;
        }

        private static void Tick(World world, int minutes)
        {
            for (int i = 0; i < minutes; i++) world.Tick();
        }

        private static int CountEvents(WorldState state, WorldEventType type) =>
            state.Events.Query(null, null, SmithySetup.Smithy, type).Count;

        private static void BuyAll(Shop shop, WorldState state, Buyer buyer)
        {
            foreach (ItemTypeId item in new[] { SmithySetup.Nails, SmithySetup.Tools, SmithySetup.Horseshoes })
            {
                int stock = shop.Stock.Count(item);
                if (stock > 0)
                    shop.Purchase(state, new PurchaseRequest(ActorId.Player,
                        buyer.Inventory, buyer.Wallet, item, stock, true));
            }
        }

        [Test]
        public void ForgeFillsRackConsumingExactIron()
        {
            WorldState state = SmithyWorld(out World world, out SmithySetup.SmithyHandle smithy,
                out ItemCatalog _, 11);

            Tick(world, 3); // one batch per recipe per tick.

            Shop shop = smithy.SmithyShop;
            Assert.That((shop.Stock.Count(SmithySetup.Nails),
                    shop.Stock.Count(SmithySetup.Tools),
                    shop.Stock.Count(SmithySetup.Horseshoes)),
                Is.EqualTo((60, 2, 2)), "Three ticks fill the rack to its targets.");
            Assert.That(shop.Stock.Count(SmithySetup.Iron), Is.EqualTo(11),
                "20 - 3 (nails) - 4 (tools) - 2 (shoes) = 11 kg: every batch consumed its exact iron.");
            Assert.That(CountEvents(state, WorldEventType.Produced), Is.EqualTo(7));
        }

        [Test]
        public void ProductionStopsWhenRackIsFull()
        {
            WorldState state = SmithyWorld(out World world, out SmithySetup.SmithyHandle smithy,
                out ItemCatalog _, 11);

            Tick(world, 100);

            Assert.That(smithy.SmithyShop.Stock.Count(SmithySetup.Iron), Is.EqualTo(11),
                "No iron burned once the rack was full.");
            Assert.That(CountEvents(state, WorldEventType.Produced), Is.EqualTo(7));
        }

        [Test]
        public void FullChainProducesSellsAndConservesMoney()
        {
            WorldState state = SmithyWorld(out World world, out SmithySetup.SmithyHandle smithy,
                out ItemCatalog catalog, 11);
            var buyer = new Buyer(2000, catalog);
            const int totalCopper = 900 + 2000;
            Shop shop = smithy.SmithyShop;

            Tick(world, 3); // forge fills the rack: 9 kg of iron become goods.
            BuyAll(shop, state, buyer); // 60 nails + 2 tools + 2 shoes sold.
            Tick(world, 3); // forge refills the empty rack.

            int copper = shop.OwnerWallet.Balance + buyer.Wallet.Balance;
            Assert.That(copper, Is.EqualTo(totalCopper),
                "Sales and production are pure transfers: every copper is tracked.");
            Assert.That(shop.OwnerWallet.Balance, Is.EqualTo(900 + 204),
                "60 nails at 1 + 2 tools at 60 + 2 shoes at 12 = 204 copper earned.");
            Assert.That(shop.Stock.Count(SmithySetup.Iron), Is.EqualTo(2),
                "Two full rack cycles burned 18 kg of the 20 kg stock.");
            Assert.That((shop.Stock.Count(SmithySetup.Nails),
                    shop.Stock.Count(SmithySetup.Tools),
                    shop.Stock.Count(SmithySetup.Horseshoes)),
                Is.EqualTo((60, 2, 2)), "The forge refilled the rack after the sale.");
        }

        [Test]
        public void IronExhaustionLogsOneOrderAndStopsProduction()
        {
            WorldState state = SmithyWorld(out World world, out SmithySetup.SmithyHandle smithy,
                out ItemCatalog catalog, 11);
            var buyer = new Buyer(100000, catalog);
            Shop shop = smithy.SmithyShop;

            // Sell every rack cycle until the iron cannot cover the cheapest batch.
            for (int cycle = 0; cycle < 10 && shop.Stock.Count(SmithySetup.Iron) > 0; cycle++)
            {
                Tick(world, 3);
                BuyAll(shop, state, buyer);
            }
            Tick(world, 3); // let the forge notice the empty bin.

            Assert.That(shop.Stock.Count(SmithySetup.Iron), Is.EqualTo(0),
                "The finite 20 kg stock sold down to nothing.");
            var orders = state.Events.Query(null, null, SmithySetup.Smithy, WorldEventType.RestockOrdered);
            Assert.That(orders.Count, Is.EqualTo(1), "Exactly one exhaustion order is logged.");
            Assert.That((orders[0].ItemType, orders[0].Quantity, orders[0].Copper),
                Is.EqualTo((SmithySetup.Iron, 20, 200)),
                "Doran orders a full 20 kg at the design-doc merchant price of ~10 copper/kg.");
            Assert.That(state.Smithy.IronExhaustionOrdered, Is.True);

            int producedBefore = CountEvents(state, WorldEventType.Produced);
            Tick(world, 500); // the forge stays cold while merchants are away (P2-08).
            Assert.That(CountEvents(state, WorldEventType.Produced), Is.EqualTo(producedBefore));
            Assert.That(CountEvents(state, WorldEventType.RestockOrdered), Is.EqualTo(1),
                "The order is not re-logged every tick.");
        }

        [Test]
        public void SameSeedGivesSameForge()
        {
            WorldState first = SmithyWorld(out World firstWorld, out _, out _, 4242);
            WorldState second = SmithyWorld(out World secondWorld, out _, out _, 4242);

            for (int day = 0; day < 5; day++)
            {
                Tick(firstWorld, 37);
                Tick(secondWorld, 37);
            }

            Shop firstShop = first.Shops[SmithySetup.Smithy];
            Shop secondShop = second.Shops[SmithySetup.Smithy];
            Assert.That(
                (secondShop.Stock.Count(SmithySetup.Iron),
                    secondShop.Stock.Count(SmithySetup.Nails),
                    secondShop.Stock.Count(SmithySetup.Tools),
                    secondShop.Stock.Count(SmithySetup.Horseshoes)),
                Is.EqualTo((
                    firstShop.Stock.Count(SmithySetup.Iron),
                    firstShop.Stock.Count(SmithySetup.Nails),
                    firstShop.Stock.Count(SmithySetup.Tools),
                    firstShop.Stock.Count(SmithySetup.Horseshoes))));
            Assert.That(CountEvents(second, WorldEventType.Produced),
                Is.EqualTo(CountEvents(first, WorldEventType.Produced)));
        }

        [Test]
        public void SaveLoadKeepsIronTillAndKeepsForging()
        {
            string root = MillingSystemTests.RepositoryRoot();
            WorldState state = SmithyWorld(out World world, out SmithySetup.SmithyHandle smithy,
                out ItemCatalog _, 11);
            Tick(world, 3); // rack full, 11 kg iron left.

            string json = WorldSaver.Save(state);
            WorldState loaded = WorldLoader.Load(json, root);
            Shop shop = loaded.Shops[SmithySetup.Smithy];

            Assert.That((shop.Stock.Count(SmithySetup.Iron),
                    shop.Stock.Count(SmithySetup.Nails),
                    shop.Stock.Count(SmithySetup.Tools),
                    shop.Stock.Count(SmithySetup.Horseshoes)),
                Is.EqualTo((11, 60, 2, 2)), "Iron and rack survive save/load.");
            Assert.That(shop.OwnerWallet.Balance, Is.EqualTo(900), "The till survives save/load.");

            // The reloaded forge keeps working exactly like an untouched world.
            World revived = Revive(loaded);
            Tick(world, 100); // control world: rack already full, nothing more happens.
            Tick(revived, 100);
            Shop control = state.Shops[SmithySetup.Smithy];
            Assert.That(shop.Stock.Count(SmithySetup.Iron),
                Is.EqualTo(control.Stock.Count(SmithySetup.Iron)));
            Assert.That(CountEvents(loaded, WorldEventType.Produced),
                Is.EqualTo(CountEvents(state, WorldEventType.Produced)));
        }

        [Test]
        public void RestoredSmithyProgressGatesTheExhaustionOrder()
        {
            // Until P2-12 writes SmithyState into the save JSON, the capture/restore convention
            // itself is what this test proves: a restored "already ordered" flag suppresses the
            // duplicate order on a reloaded world.
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(5, new GameTime(0));
            SmithySetup.SmithyHandle smithy = SmithySetup.Stock(state, catalog);
            var world = new World(state);
            world.RegisterSystem(new SmithySystem(new[] { smithy.Forging() }));

            state.RestoreSmithy(new SmithyState(ironExhaustionOrdered: true));
            Assert.That(smithy.SmithyShop.Stock.TryRemove(SmithySetup.Iron, 20), Is.True);
            Tick(world, 10);

            Assert.That(CountEvents(state, WorldEventType.RestockOrdered), Is.EqualTo(0),
                "The restored flag stops a second exhaustion order for the same depletion.");
        }
    }
}
