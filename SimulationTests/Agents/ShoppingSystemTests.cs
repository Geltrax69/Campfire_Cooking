using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Verifies the daily bread run: at 09:00 an NPC whose food store is worth less
    /// than 150 hunger points buys 2 rye loaves (the daily bread) from the bakery,
    /// through the normal Shop.Purchase path — friend discounts, partial fills and
    /// money conservation all apply. Well-fed, penniless or shopless NPCs are left alone.
    /// </summary>
    public sealed class ShoppingSystemTests
    {
        private static readonly ItemTypeId Bread = new ItemTypeId("item_bread_rye");
        private static readonly ItemTypeId Barley = new ItemTypeId("item_bread_barley");
        private static readonly ItemTypeId Iron = new ItemTypeId("item_iron_stock");
        private static readonly LocationId Home = new LocationId("loc_test_home");
        private static readonly LocationId BakeryLocation = new LocationId("loc_bakery");
        private static readonly NpcId Baker = new NpcId("npc_oda");

        [Test]
        public void LowPantryNpcBuysBread()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world);
            RegisterBakery(state, catalog, Baker);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", copper: 500);
            Pantry(state, npc).Add(Bread, 2);

            world.RegisterSystem(new ShoppingSystem(catalog));
            world.Tick();

            // 2 loaves (60 points) -> buys the 2-loaf daily bread.
            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(4));
            Assert.That(Wallet(state, npc).Balance, Is.EqualTo(500 - 2 * 3));
            Assert.That(state.Shops[BakeryLocation].Stock.Count(Bread), Is.EqualTo(100 - 2));
        }

        [Test]
        public void WellStockedNpcDoesNotShop()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world);
            RegisterBakery(state, catalog, Baker);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", copper: 500);
            Pantry(state, npc).Add(Bread, 8);

            world.RegisterSystem(new ShoppingSystem(catalog));
            world.Tick();

            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(8));
            Assert.That(Wallet(state, npc).Balance, Is.EqualTo(500));
        }

        [Test]
        public void PennilessNpcCannotShop()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world);
            RegisterBakery(state, catalog, Baker);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", copper: 0);
            Pantry(state, npc).Add(Bread, 2);

            world.RegisterSystem(new ShoppingSystem(catalog));
            world.Tick();

            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(2));
        }

        [Test]
        public void PartialFillWhenCopperRunsOut()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world);
            RegisterBakery(state, catalog, Baker);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", copper: 10);
            Pantry(state, npc).Add(Bread, 2);

            world.RegisterSystem(new ShoppingSystem(catalog));
            world.Tick();

            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(2 + 2),
                "10 copper buys the 2-loaf daily bread at 3 copper; never debt.");
            Assert.That(Wallet(state, npc).Balance, Is.EqualTo(4));
        }

        [Test]
        public void FriendDiscountAppliesToShopping()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world);
            Shop bakery = RegisterBakery(state, catalog, Baker);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", copper: 500);
            Pantry(state, npc).Add(Bread, 2);
            state.Knowledge.Register(Baker);
            state.Knowledge.InitializeRelationships(new[]
            {
                new Relationship(Baker, npc, 50, 80, "test"),
                new Relationship(npc, Baker, 50, 80, "test"),
            });
            bakery.DiscountPolicy = FriendPricing.ForShopkeeper(state, Baker);

            world.RegisterSystem(new ShoppingSystem(catalog));
            world.Tick();

            int paid = 500 - Wallet(state, npc).Balance;
            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(4));
            Assert.That(paid, Is.LessThan(2 * 3),
                "80 affection earns a friend discount off the 6-copper list total.");
            Assert.That(paid, Is.GreaterThan(0));
        }

        [Test]
        public void BuysBarleyWhenBakerySellsIt()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world);
            RegisterBakeryWithBarley(state, catalog, Baker);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", copper: 500);
            Pantry(state, npc).Add(Bread, 2);

            world.RegisterSystem(new ShoppingSystem(catalog));
            world.Tick();

            // Daily bread is 2 rye + 2 barley: both loaves the bake makes sell through.
            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(4));
            Assert.That(Pantry(state, npc).Count(Barley), Is.EqualTo(2));
            Assert.That(Wallet(state, npc).Balance, Is.EqualTo(500 - 2 * 3 - 2 * 2));
            Assert.That(state.Shops[BakeryLocation].Stock.Count(Barley), Is.EqualTo(50 - 2));
        }

        [Test]
        public void NoticesSelloutWhenShelfIsEmpty()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world);
            Shop bakery = RegisterBakery(state, catalog, Baker);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", copper: 500);
            Pantry(state, npc).Add(Bread, 2);
            // The shelf is bare: rye sold out to an earlier customer.
            Assert.That(bakery.Stock.TryRemove(Bread, 100), Is.True);

            world.RegisterSystem(new ShoppingSystem(catalog));
            world.Tick();

            Assert.That(state.Knowledge.TryGet(npc, out BeliefStore store), Is.True);
            bool noticed = false;
            foreach (var belief in store.Query())
            {
                if (belief.Claim.Kind == BeliefClaimKind.StockMissing &&
                    belief.Source.Kind == BeliefSourceKind.Seen)
                {
                    noticed = true;
                    break;
                }
            }
            Assert.That(noticed, Is.True,
                "A buyer who finds the shelf empty forms a StockMissing belief (the seed of a rumor).");
        }

        [Test]
        public void NoBakeryNoShopping()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", copper: 500);
            Pantry(state, npc).Add(Bread, 2);

            world.RegisterSystem(new ShoppingSystem(catalog));
            world.Tick();

            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(2));
        }

        [Test]
        public void ShoppingOnlyHappensAtNine()
        {
            ItemCatalog catalog = TestCatalog();
            var state = new WorldState(42, new GameTime(10 * 60));
            var world = new World(state);
            RegisterBakery(state, catalog, Baker);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", copper: 500);
            Pantry(state, npc).Add(Bread, 2);

            world.RegisterSystem(new ShoppingSystem(catalog));
            world.Tick();

            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(2),
                "The shopping hour is 09:00 sharp.");
        }

        private static ItemCatalog TestCatalog() => new ItemCatalog(new[]
        {
            new ItemDefinition(Bread, "Rye loaf", "food", 3, 1, hungerEffect: -30,
                perishable: new PerishableInfo(4, 4, 50, "stale loaf", "moldy loaf",
                    staleHungerEffect: -10)),
            new ItemDefinition(Barley, "Barley loaf", "food", 2, 1, hungerEffect: -25,
                perishable: new PerishableInfo(4, 4, 50, "stale loaf", "moldy loaf",
                    staleHungerEffect: -10)),
            new ItemDefinition(Iron, "Iron stock", "material", 10, 1),
        });

        private static WorldState TestWorld(ItemCatalog catalog, out World world)
        {
            var state = new WorldState(42, new GameTime(9 * 60 - 1));
            world = new World(state);
            return state;
        }

        private static Shop RegisterBakery(WorldState state, ItemCatalog catalog, NpcId baker)
        {
            var stock = new Inventory(catalog);
            stock.Add(Bread, 100);
            var shop = new Shop(BakeryLocation, baker, stock, new Wallet(0),
                new[] { new KeyValuePair<ItemTypeId, int>(Bread, 3) });
            state.Shops.Register(shop);
            return shop;
        }

        private static Shop RegisterBakeryWithBarley(WorldState state, ItemCatalog catalog, NpcId baker)
        {
            var stock = new Inventory(catalog);
            stock.Add(Bread, 100);
            stock.Add(Barley, 50);
            var shop = new Shop(BakeryLocation, baker, stock, new Wallet(0),
                new[]
                {
                    new KeyValuePair<ItemTypeId, int>(Bread, 3),
                    new KeyValuePair<ItemTypeId, int>(Barley, 2),
                });
            state.Shops.Register(shop);
            return shop;
        }

        private static NpcId RegisterNpc(WorldState state, ItemCatalog catalog, string id, int copper)
        {
            var npcId = new NpcId(id);
            var definition = new NpcDefinition(npcId, id, 30, "test", "tester", Home, Home, 0,
                new Dictionary<string, int> { ["honest"] = 50 },
                new NeedRates(0, 0, 0),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            state.Npcs.Register(new NpcState(definition, 30, 80, 50));
            state.Knowledge.Register(npcId);
            var owner = ActorId.ForNpc(npcId);
            state.Belongings.Register(owner, new Inventory(catalog), new Wallet(copper));
            return npcId;
        }

        private static Inventory Pantry(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Inventory;

        private static Wallet Wallet(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Wallet;
    }
}
