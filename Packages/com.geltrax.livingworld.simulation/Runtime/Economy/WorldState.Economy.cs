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
        public TaxState Tax { get; private set; } = new TaxState();
        public CommunityFundState CommunityFund { get; private set; } = new CommunityFundState();
        public EconomyBaselineState EconomyBaseline { get; private set; } = new EconomyBaselineState();
        public SpoilageState Spoilage { get; private set; } = new SpoilageState();
        public DebtLedgerState DebtLedger { get; private set; } = new DebtLedgerState();
        public TavernPopularityState TavernPopularity { get; private set; } = new TavernPopularityState();
        public IngredientDemandState IngredientDemand { get; private set; } = new IngredientDemandState();

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

        /// <summary>
        /// Installs validated tax progress for Persistence. P2-12 wires the JSON
        /// saver/loader to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreTax(TaxState state)
        {
            Tax = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Installs validated community fund progress for Persistence. P2-12 wires the JSON
        /// saver/loader to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreCommunityFund(CommunityFundState state)
        {
            CommunityFund = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Installs the validated economy baseline for Persistence. P2-12 wires the JSON
        /// saver/loader to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreEconomyBaseline(EconomyBaselineState state)
        {
            EconomyBaseline = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Installs validated spoilage progress for Persistence. P2-12 wires the JSON
        /// saver/loader to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreSpoilage(SpoilageState state)
        {
            Spoilage = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Installs validated debt ledger progress for Persistence. P2-12 wires the JSON
        /// saver/loader to call this; tests and the loader use it directly until then.
        /// </summary>
        internal void RestoreDebtLedger(DebtLedgerState state)
        {
            DebtLedger = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Installs validated tavern popularity for Persistence. The Phase 3 JSON
        /// saver/loader must call this; tests use it directly until then.
        /// </summary>
        internal void RestoreTavernPopularity(TavernPopularityState state)
        {
            TavernPopularity = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Installs validated ingredient demand for Persistence. The Phase 3 JSON
        /// saver/loader must call this; tests use it directly until then.
        /// </summary>
        internal void RestoreIngredientDemand(IngredientDemandState state)
        {
            IngredientDemand = state ?? throw new ArgumentNullException(nameof(state));
        }
    }
}
