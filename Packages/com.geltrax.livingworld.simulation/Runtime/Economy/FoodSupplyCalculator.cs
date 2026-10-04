using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Computes days of village food in storage and the 0-100 food-supply stat
    /// (P5-01, TOWN.md stat_food_supply): granary sacks plus household cellars
    /// (NPC inventories), converted to hunger points, divided by the village's daily
    /// need, scaled so 180 days is a full 100. Deterministic and RNG-free.
    /// </summary>
    public static class FoodSupplyCalculator
    {
        /// <summary>
        /// Hunger points in one grain sack. From TOWN.md's start math: 50 sacks hold
        /// ~23 village-days of food for 120 people, so one sack is
        /// 23 x 120 x 100 / 50 = 5520 points.
        /// </summary>
        public const int HungerPointsPerGrainSack = 5520;

        /// <summary>Hunger points one villager needs per day (the 0-100 need scale is roughly a day).</summary>
        public const int HungerPointsPerVillagerDay = 100;

        /// <summary>Days of stored food that count as a full food supply (TOWN.md).</summary>
        public const int FullSupplyDays = 180;

        private static readonly ItemTypeId Grain = new ItemTypeId("item_grain");
        private static readonly ItemTypeId Flour = new ItemTypeId("item_flour");
        private const string FoodCategory = "food";

        /// <summary>Uncapped days of village food currently in storage.</summary>
        public static double ComputeFoodDays(WorldState state, ItemCatalog catalog)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            long points = checked((long)state.Granary.Sacks * HungerPointsPerGrainSack);
            foreach (NpcBelongingsEntry entry in state.Belongings.Entries)
                foreach (KeyValuePair<ItemTypeId, int> lot in entry.Inventory.Contents)
                    points = checked(points + (long)lot.Value * HungerPointsPerUnit(catalog, lot.Key));

            int population = state.Npcs.Count + TownStatNumbers.BackgroundVillagers;
            return (double)points / (HungerPointsPerVillagerDay * population);
        }

        /// <summary>The 0-100 food-supply stat: food days / 180 x 100, capped at 100.</summary>
        public static int ComputeFoodSupply(WorldState state, ItemCatalog catalog)
        {
            double days = ComputeFoodDays(state, catalog);
            double supply = days / FullSupplyDays * 100.0;
            if (supply > 100.0) supply = 100.0;
            return (int)Math.Round(supply, MidpointRounding.AwayFromZero);
        }

        private static int HungerPointsPerUnit(ItemCatalog catalog, ItemTypeId item)
        {
            if (item == Grain) return HungerPointsPerGrainSack;
            // A flour sack is a grain sack minus the miller's 1/12 toll in kind.
            if (item == Flour) return HungerPointsPerGrainSack * 11 / 12;
            ItemDefinition definition = Find(catalog, item);
            if (definition == null || definition.Category != FoodCategory) return 0;
            // Only restorative (negative hunger effect) food feeds the village; foods that
            // raise hunger (positive effect) are clamped to no contribution.
            int hungerEffect = definition.HungerEffect ?? 0;
            return hungerEffect < 0 ? -hungerEffect : 0;
        }

        private static ItemDefinition Find(ItemCatalog catalog, ItemTypeId item)
        {
            foreach (ItemDefinition definition in catalog.Items)
                if (definition.Id == item)
                    return definition;
            return null;
        }
    }
}
