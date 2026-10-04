using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Caller-owned per-village price lists for inter-village trade (P6-02).
    /// Deliberate simplification: prices are hardcoded by the world-build step
    /// instead of emerging from each village's supply and demand (abstract
    /// villages have no simulated stocks to read). King's Rest pays more for
    /// Millbrook's apples and cloth than Millbrook charges — that spread is
    /// what makes the merchant's road worthwhile.
    /// </summary>
    public sealed class VillageTradePrices
    {
        private readonly Dictionary<VillageId, Dictionary<ItemTypeId, int>> _prices =
            new Dictionary<VillageId, Dictionary<ItemTypeId, int>>();

        /// <summary>Sets one village's trade price for one good, in copper per unit.</summary>
        public void SetPrice(VillageId village, ItemTypeId item, int copper)
        {
            if (!village.IsValid) throw new ArgumentException("A village ID must be valid.", nameof(village));
            if (!item.IsValid) throw new ArgumentException("An item type must be valid.", nameof(item));
            if (copper < 1) throw new ArgumentOutOfRangeException(nameof(copper), "Trade prices must be positive.");
            if (!_prices.TryGetValue(village, out var villagePrices))
            {
                villagePrices = new Dictionary<ItemTypeId, int>();
                _prices.Add(village, villagePrices);
            }
            villagePrices[item] = copper;
        }

        /// <summary>Returns the village's trade price; throws when no price was set.</summary>
        public int GetPrice(VillageId village, ItemTypeId item)
        {
            if (!village.IsValid) throw new ArgumentException("A village ID must be valid.", nameof(village));
            if (!item.IsValid) throw new ArgumentException("An item type must be valid.", nameof(item));
            if (!_prices.TryGetValue(village, out var villagePrices) || !villagePrices.TryGetValue(item, out int copper))
                throw new ArgumentException(
                    "No trade price for '" + item + "' at village '" + village + "'.", nameof(item));
            return copper;
        }
    }
}
