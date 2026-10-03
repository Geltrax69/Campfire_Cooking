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
        public SmithyState Smithy { get; private set; } = new SmithyState();

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

        /// <summary>
        /// Installs validated smithy progress for Persistence. P2-12 wires the JSON saver/loader
        /// to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreSmithy(SmithyState state)
        {
            Smithy = state ?? throw new ArgumentNullException(nameof(state));
        }
    }
}
