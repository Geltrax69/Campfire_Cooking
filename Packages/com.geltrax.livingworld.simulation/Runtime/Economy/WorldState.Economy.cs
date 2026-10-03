using System;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Owns world-level economic state: shops, actor belongings and system progress.</summary>
    public sealed partial class WorldState
    {
        public ShopRegistry Shops { get; } = new ShopRegistry();
        public NpcBelongings Belongings { get; } = new NpcBelongings();
        public ProductionState Production { get; private set; } = new ProductionState();
        public RestockState Restock { get; private set; } = new RestockState();
        public PriceAdjustmentState Prices { get; private set; } = new PriceAdjustmentState();

        /// <summary>Installs validated production progress for Persistence.</summary>
        internal void RestoreProduction(ProductionState state)
        {
            Production = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Installs validated restock progress for Persistence.</summary>
        internal void RestoreRestock(RestockState state)
        {
            Restock = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Installs validated price-adjustment progress for Persistence.</summary>
        internal void RestorePrices(PriceAdjustmentState state)
        {
            Prices = state ?? throw new ArgumentNullException(nameof(state));
        }
    }
}
