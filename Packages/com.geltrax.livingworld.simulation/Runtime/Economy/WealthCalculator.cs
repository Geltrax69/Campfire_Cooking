using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Computes village wealth in copper from world truth (P5-01, TOWN.md stat_wealth):
    /// simulated NPC money + village fund + granary stock at base price + shop stocks at
    /// base price. The player's coin is not NPC money, and a shop's separate till is
    /// working cash rather than anyone's money, so neither is counted.
    /// </summary>
    public static class WealthCalculator
    {
        private static readonly ItemTypeId Grain = new ItemTypeId("item_grain");

        public static int ComputeWealthCopper(WorldState state, ItemCatalog catalog)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            long total = 0;
            foreach (NpcBelongingsEntry entry in state.Belongings.Entries)
            {
                if (entry.Owner.IsPlayer) continue;
                total = checked(total + entry.Wallet.Balance);
            }
            total = checked(total + state.VillageFund.Funds.Balance);
            total = checked(total + (long)state.Granary.Sacks * GrainBasePrice(catalog));
            foreach (Shop shop in state.Shops.Shops)
                foreach (KeyValuePair<ItemTypeId, int> lot in shop.Stock.Contents)
                    total = checked(total + (long)lot.Value * catalog[lot.Key].BaseValue);
            // Wealth fits comfortably in an int at prototype scale; checked above guards growth.
            return checked((int)total);
        }

        private static int GrainBasePrice(ItemCatalog catalog)
        {
            foreach (ItemDefinition definition in catalog.Items)
                if (definition.Id == Grain)
                    return definition.BaseValue;
            return 0;
        }
    }
}
