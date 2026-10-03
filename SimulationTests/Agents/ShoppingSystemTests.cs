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
    /// Verifies the daily pantry run: at 09:00 an NPC whose food store is worth less
    /// than 150 hunger points buys rye bread from the bakery (up to ~270 points' worth),
    /// through the normal Shop.Purchase path — friend discounts, partial fills and
    /// money conservation all apply. Well-fed, penniless or shopless NPCs are left alone.
    /// </summary>
    public sealed class ShoppingSystemTests
    {
        private static readonly ItemTypeId Bread = new ItemTypeId("item_bread_rye");
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

            // 2 loaves (60 points) -> buys 7 to reach 270 points of food value.
            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(9));
            Assert.That(Wallet(state, npc).Balance, Is.EqualTo(500 - 7 * 3));
            Assert.That(state.Shops[BakeryLocation].Stock.Count(Bread), Is.EqualTo(100 - 7));
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

            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(2 + 3),
                "10 copper buys 3 loaves at 3 copper; partial fill, never debt.");
            Assert.That(Wallet(state, npc).Balance, Is.EqualTo(1));
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
            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(9));
            Assert.That(paid, Is.LessThan(7 * 3),
                "80 affection earns a friend discount off the 21-copper list total.");
            Assert.That(paid, Is.GreaterThan(0));
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
