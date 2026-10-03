using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Verifies the village's mealtime rule: hungry NPCs (hunger >= 15) eat up to three
    /// foods from their own inventory at 07:00, 12:00 and 19:00, most filling first,
    /// applying the stale effect to stale lots. Foods whose approved hunger value is
    /// positive (a Content data quirk — roast fish and porridge) never add hunger.
    /// </summary>
    public sealed class EatSystemTests
    {
        private static readonly ItemTypeId Bread = new ItemTypeId("item_bread_rye");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly ItemTypeId Fish = new ItemTypeId("item_roasted_fish");
        private static readonly ItemTypeId Iron = new ItemTypeId("item_iron_stock");
        private static readonly LocationId Home = new LocationId("loc_test_home");

        [Test]
        public void HungryNpcEatsMostFillingFoodFirst()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world, 12 * 60 - 1);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", hunger: 60);
            Inventory pantry = Pantry(state, npc);
            pantry.Add(Bread, 1);
            pantry.Add(Apple, 1);

            world.RegisterSystem(new EatSystem(catalog));
            world.Tick();

            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(0));
            Assert.That(Pantry(state, npc).Count(Apple), Is.EqualTo(0));
            Assert.That(Hunger(state, npc), Is.EqualTo(15),
                "60 - 30 (bread) - 15 (apple): most filling first, stops at the third item.");
        }

        [Test]
        public void NotHungryNpcSkipsTheMeal()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world, 12 * 60 - 1);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", hunger: 10);
            Pantry(state, npc).Add(Bread, 2);

            world.RegisterSystem(new EatSystem(catalog));
            world.Tick();

            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(2));
            Assert.That(Hunger(state, npc), Is.EqualTo(10));
        }

        [Test]
        public void StaleBreadRestoresOnlyStaleAmount()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world, 12 * 60 - 1);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", hunger: 60);
            Inventory pantry = Pantry(state, npc);
            pantry.Add(Bread, 1);
            for (int day = 0; day < 5; day++) pantry.AgeOneDay(out _, out _);

            world.RegisterSystem(new EatSystem(catalog));
            world.Tick();

            Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(0));
            Assert.That(Hunger(state, npc), Is.EqualTo(50),
                "Stale bread's approved stale effect (-10) applies, not the fresh -30.");
        }

        [Test]
        public void PositiveHungerFoodNeverAddsHunger()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world, 12 * 60 - 1);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", hunger: 60);
            Pantry(state, npc).Add(Fish, 1);

            world.RegisterSystem(new EatSystem(catalog));
            world.Tick();

            Assert.That(Pantry(state, npc).Count(Fish), Is.EqualTo(0),
                "The fish is eaten — the Content quirk is a non-filling food, not an uneaten one.");
            Assert.That(Hunger(state, npc), Is.EqualTo(60),
                "Eating never increases hunger, even when approved data says +25.");
        }

        [Test]
        public void NonFoodIsNeverEaten()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world, 12 * 60 - 1);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", hunger: 60);
            Pantry(state, npc).Add(Iron, 3);

            world.RegisterSystem(new EatSystem(catalog));
            world.Tick();

            Assert.That(Pantry(state, npc).Count(Iron), Is.EqualTo(3));
            Assert.That(Hunger(state, npc), Is.EqualTo(60));
        }

        [Test]
        public void EmptyPantryLeavesHungerUnchanged()
        {
            ItemCatalog catalog = TestCatalog();
            WorldState state = TestWorld(catalog, out World world, 12 * 60 - 1);
            NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", hunger: 60);

            world.RegisterSystem(new EatSystem(catalog));
            world.Tick();

            Assert.That(Hunger(state, npc), Is.EqualTo(60));
        }

        [Test]
        public void OnlyMealHoursTriggerEating()
        {
            foreach (int minute in new[] { 6 * 60, 12 * 60 + 30, 23 * 60 })
            {
                ItemCatalog catalog = TestCatalog();
                WorldState state = TestWorld(catalog, out World world, minute);
                NpcId npc = RegisterNpc(state, catalog, "npc_test_alda", hunger: 60);
                Pantry(state, npc).Add(Bread, 1);

                world.RegisterSystem(new EatSystem(catalog));
                world.Tick();

                Assert.That(Pantry(state, npc).Count(Bread), Is.EqualTo(1),
                    $"No meal at minute {minute}.");
                Assert.That(Hunger(state, npc), Is.EqualTo(60));
            }
        }

        private static ItemCatalog TestCatalog() => new ItemCatalog(new[]
        {
            new ItemDefinition(Bread, "Rye loaf", "food", 3, 1, hungerEffect: -30,
                perishable: new PerishableInfo(4, 4, 50, "stale loaf", "moldy loaf",
                    staleHungerEffect: -10)),
            new ItemDefinition(Apple, "Apple", "food", 3, 1, hungerEffect: -15,
                perishable: new PerishableInfo(10, 10, 50, "mushy apple", "rotten apple",
                    staleHungerEffect: -5)),
            new ItemDefinition(Fish, "Roast fish", "food", 5, 1, hungerEffect: 25,
                perishable: new PerishableInfo(2, 2, 50, "stale roast fish", "spoiled roast fish",
                    staleHungerEffect: -12)),
            new ItemDefinition(Iron, "Iron stock", "material", 10, 1),
        });

        private static WorldState TestWorld(ItemCatalog catalog, out World world, long startMinute)
        {
            // Ticks advance the clock before systems run: start a minute before the
            // meal hour so the tick lands exactly on it.
            var state = new WorldState(42, new GameTime(startMinute));
            world = new World(state);
            return state;
        }

        private static NpcId RegisterNpc(WorldState state, ItemCatalog catalog, string id, int hunger)
        {
            var npcId = new NpcId(id);
            var definition = new NpcDefinition(npcId, id, 30, "test", "tester", Home, Home, 0,
                new Dictionary<string, int> { ["honest"] = 50 },
                new NeedRates(0, 0, 0),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            state.Npcs.Register(new NpcState(definition, hunger, 80, 50));
            state.Knowledge.Register(npcId);
            var owner = ActorId.ForNpc(npcId);
            state.Belongings.Register(owner, new Inventory(catalog), new Wallet(0));
            return npcId;
        }

        private static Inventory Pantry(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Inventory;

        private static int Hunger(WorldState state, NpcId npc) =>
            state.Npcs[npc].Needs.Hunger;
    }
}
