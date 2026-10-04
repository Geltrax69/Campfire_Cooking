using System;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Proves food supply is computed from granary sacks plus household stores. P5-01.</summary>
    public sealed class FoodSupplyTests
    {
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly ItemTypeId Bread = new ItemTypeId("item_bread");
        private static readonly ItemTypeId Grain = new ItemTypeId("item_grain");
        private static readonly ItemTypeId Flour = new ItemTypeId("item_flour");

        private static ItemCatalog Catalog() => new ItemCatalog(new[]
        {
            new ItemDefinition(Apple, "Apple", "food", 3, 1, hungerEffect: -15),
            new ItemDefinition(Bread, "Bread", "food", 3, 1, hungerEffect: -30),
            new ItemDefinition(Grain, "Grain", "material", 8, 1),
            new ItemDefinition(Flour, "Flour", "material", 10, 1),
        });

        private static WorldState WorldWithVillagers(int npcCount)
        {
            var state = new WorldState(777, new GameTime(0));
            for (int i = 0; i < npcCount; i++)
            {
                var npcId = new NpcId("npc_food_" + i);
                var definition = new NpcDefinition(npcId, "npc_food_" + i, 30,
                    "unspecified", "farmer", new LocationId("loc_home"), new LocationId("loc_home"),
                    0, new System.Collections.Generic.Dictionary<string, int>(),
                    new NeedRates(0, 0, 0),
                    new NpcSchedule(
                        Array.Empty<ScheduleEntry>(),
                        Array.Empty<ScheduleEntry>()));
                state.Npcs.Register(new NpcState(definition, 30, 80, 70));
                state.Belongings.Register(ActorId.ForNpc(npcId), new Inventory(Catalog()), new Wallet(0));
            }
            return state;
        }

        [Test]
        public void FoodSupplyReflectsGranary()
        {
            ItemCatalog catalog = Catalog();
            WorldState state = WorldWithVillagers(20); // population 120
            state.RestoreGranary(new GranaryState(sacks: 50));

            int baseline = FoodSupplyCalculator.ComputeFoodSupply(state, catalog);
            // 50 sacks x 5520 hunger points / (100 x 120 people) = 23 days -> 23/180 x 100 = 13.
            Assert.That(baseline, Is.EqualTo(13));

            state.Granary.AddSacks(50);
            int fuller = FoodSupplyCalculator.ComputeFoodSupply(state, catalog);
            Assert.That(fuller, Is.GreaterThan(baseline));

            Assert.That(state.Granary.TryRemoveSacks(90), Is.True);
            int emptier = FoodSupplyCalculator.ComputeFoodSupply(state, catalog);
            Assert.That(emptier, Is.LessThan(baseline));

            Assert.That(state.Granary.TryRemoveSacks(11), Is.False); // only 10 left
            Assert.That(state.Granary.Sacks, Is.EqualTo(10));
        }

        [Test]
        public void FoodSupplyCountsHouseholdStores()
        {
            ItemCatalog catalog = Catalog();
            WorldState state = WorldWithVillagers(2); // population 102
            state.RestoreGranary(new GranaryState());

            int empty = FoodSupplyCalculator.ComputeFoodSupply(state, catalog);
            Assert.That(empty, Is.Zero);

            // One villager stores 2000 loaves of bread: 2000 x 30 = 60000 hunger points,
            // about 5.9 village-days for 102 people -> food supply 3.
            var first = state.Belongings.Entries[0];
            first.Inventory.Add(Bread, 2000);
            int stored = FoodSupplyCalculator.ComputeFoodSupply(state, catalog);
            Assert.That(stored, Is.GreaterThan(empty));

            // Spoiled-positive hunger effects (should not exist after the P5 content fix,
            // but the runtime clamps them) contribute nothing.
            var catalogWithBadFood = new ItemCatalog(new[]
            {
                new ItemDefinition(Apple, "Apple", "food", 3, 1, hungerEffect: 15),
            });
            var badState = new WorldState(778, new GameTime(0));
            var npcId = new NpcId("npc_bad");
            var definition = new NpcDefinition(npcId, "npc_bad", 30,
                "unspecified", "farmer", new LocationId("loc_home"), new LocationId("loc_home"),
                0, new System.Collections.Generic.Dictionary<string, int>(),
                new NeedRates(0, 0, 0),
                new NpcSchedule(
                    Array.Empty<ScheduleEntry>(),
                    Array.Empty<ScheduleEntry>()));
            badState.Npcs.Register(new NpcState(definition, 30, 80, 70));
            var inventory = new Inventory(catalogWithBadFood);
            inventory.Add(Apple, 500);
            badState.Belongings.Register(ActorId.ForNpc(npcId), inventory, new Wallet(0));
            Assert.That(FoodSupplyCalculator.ComputeFoodSupply(badState, catalogWithBadFood), Is.Zero);
        }

        [Test]
        public void FoodSupplyCapsAtOneHundred()
        {
            ItemCatalog catalog = Catalog();
            WorldState state = WorldWithVillagers(20);
            // Far more than 180 days of food: the stat caps at 100.
            state.RestoreGranary(new GranaryState(sacks: 500));
            Assert.That(FoodSupplyCalculator.ComputeFoodSupply(state, catalog), Is.EqualTo(100));
        }

        [Test]
        public void GranaryStateValidates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GranaryState(-1));
            var granary = new GranaryState();
            Assert.Throws<ArgumentOutOfRangeException>(() => granary.AddSacks(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => granary.TryRemoveSacks(0));
            Assert.That(granary.TryRemoveSacks(1), Is.False);
        }
    }
}
