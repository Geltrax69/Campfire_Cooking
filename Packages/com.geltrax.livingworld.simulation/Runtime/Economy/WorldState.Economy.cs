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
        public MerchantScheduleState MerchantSchedule { get; private set; } = new MerchantScheduleState();
        public TravelerSpendState TravelerSpend { get; private set; } = new TravelerSpendState();
        public WolfBountyState WolfBounty { get; private set; } = new WolfBountyState();
        public VillageFundState VillageFund { get; private set; } = new VillageFundState();
        public HarvestState Harvest { get; private set; } = new HarvestState();

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

        /// <summary>
        /// Installs validated merchant schedule progress for Persistence. P2-12 wires the JSON
        /// saver/loader to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreMerchantSchedule(MerchantScheduleState state)
        {
            MerchantSchedule = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Installs validated traveler spend progress for Persistence. P2-12 wires the JSON
        /// saver/loader to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreTravelerSpend(TravelerSpendState state)
        {
            TravelerSpend = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Installs validated wolf bounty progress for Persistence. P2-12 wires the JSON
        /// saver/loader to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreWolfBounty(WolfBountyState state)
        {
            WolfBounty = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Installs validated village fund progress for Persistence. P2-12 wires the JSON
        /// saver/loader to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreVillageFund(VillageFundState state)
        {
            VillageFund = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Installs validated harvest wage progress for Persistence. P2-12 wires the JSON
        /// saver/loader to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreHarvest(HarvestState state)
        {
            Harvest = state ?? throw new ArgumentNullException(nameof(state));
        }
    }
}
