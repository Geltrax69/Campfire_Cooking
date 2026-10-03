using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// World-build step that opens the village's money sinks (ECONOMY.md §6): the reeve's
    /// tax collection, the council's community fund and feast reserve, adaptive merchant
    /// import orders, and daily spoilage of perishables. Run after MoneySourcesSetup —
    /// the tax table needs the personal wallets and business tills it registers.
    ///
    /// Every assessment, order target and fund movement follows the approved design
    /// numbers; each system also scales with visible prosperity (the stabilizer from
    /// ECONOMY.md §6), so good years pay and order more, lean years less. Copper leaves
    /// the village only through the named gates (taxes to the crown, imports with the
    /// merchant); the fund and feast pots are savings that stay in the village total.
    /// </summary>
    public static class MoneySinksSetup
    {
        // The reeve's assessment table (ECONOMY.md §6): 1221 copper per collection, twice
        // a year (spring and autumn). Tilda pays from the store till, Doran from the
        // smithy till, Oda from the bakery till and Garrick from the mill till — their
        // wealth sits in their businesses, as the design requires.
        private static readonly KeyValuePair<string, int>[] TaxBaseTable =
        {
            new KeyValuePair<string, int>("npc_corvin_alder", 180),
            new KeyValuePair<string, int>("npc_tilda_bray", 200),
            new KeyValuePair<string, int>("npc_bessa_marlowe", 150),
            new KeyValuePair<string, int>("npc_mira_holt", 120),
            new KeyValuePair<string, int>("npc_oda_fenn", 100),
            new KeyValuePair<string, int>("npc_doran_kettle", 100),
            new KeyValuePair<string, int>("npc_garrick_alder", 80),
            new KeyValuePair<string, int>("npc_maren_alder", 21),
            new KeyValuePair<string, int>("npc_piotr_alder", 21),
            new KeyValuePair<string, int>("npc_bram_stone", 21),
            new KeyValuePair<string, int>("npc_sima_fenn", 21),
            new KeyValuePair<string, int>("npc_sella_wren", 21),
            new KeyValuePair<string, int>("npc_elswith_alder", 21),
            new KeyValuePair<string, int>("npc_tam_oakes", 21),
            new KeyValuePair<string, int>("npc_brynn_oakes", 21),
            new KeyValuePair<string, int>("npc_jory_reed", 21),
            new KeyValuePair<string, int>("npc_ralf_hale", 21),
            new KeyValuePair<string, int>("npc_lida_alder", 20),
            new KeyValuePair<string, int>("npc_tansy_alder", 20),
            new KeyValuePair<string, int>("npc_tom_fenn", 20),
        };

        // Community fund (ECONOMY.md §6): 150 copper set aside every thirty days, plus a
        // 200-copper feast reserve each autumn for the Harvest Feast.
        private const int CommunityFundMonthlyCopper = 150;
        private const int FeastReserveCopper = 200;

        /// <summary>
        /// Captures the economy baseline, initializes tax, community fund and spoilage
        /// progress, and returns the handle. Run after MoneySourcesSetup.
        /// </summary>
        public static MoneySinksHandle Stock(WorldState state, ItemCatalog catalog,
            MoneySourcesSetup.MoneySourcesHandle money)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (money == null) throw new ArgumentNullException(nameof(money));

            long startDay = state.Clock.Day;
            // The baseline is the village's visible wealth on opening day; everything the
            // sinks scale against is measured from here.
            long baseline = ProsperityIndex.TotalVillageCopper(state);
            state.RestoreEconomyBaseline(new EconomyBaselineState(initialized: true, baselineCopper: baseline));
            state.RestoreTax(new TaxState(initialized: true, lastCollectionDay: startDay - 1));
            state.RestoreCommunityFund(new CommunityFundState(initialized: true,
                lastMonthlyDay: startDay, lastFeastYear: 0));
            state.RestoreSpoilage(new SpoilageState(initialized: true, lastAgedDay: startDay));

            return new MoneySinksHandle(catalog, money);
        }

        /// <summary>Caller-owned handle to the wired money sinks: systems' configurations.</summary>
        public sealed class MoneySinksHandle
        {
            private readonly ItemCatalog _catalog;
            private readonly MoneySourcesSetup.MoneySourcesHandle _money;

            internal MoneySinksHandle(ItemCatalog catalog, MoneySourcesSetup.MoneySourcesHandle money)
            {
                _catalog = catalog;
                _money = money;
            }

            /// <summary>The reeve's twice-yearly collection from the assessment table.</summary>
            public TaxConfiguration Taxes()
            {
                var assessments = new List<TaxAssessment>();
                foreach (KeyValuePair<string, int> row in TaxBaseTable)
                {
                    var taxpayer = new NpcId(row.Key);
                    assessments.Add(new TaxAssessment(taxpayer, TaxpayerWallet(taxpayer), row.Value));
                }
                return new TaxConfiguration("tax-reeve", assessments,
                    MoneySourcesSetup.Market, EventVisibility.Normal);
            }

            /// <summary>The council's monthly savings and the autumn feast reserve.</summary>
            public CommunityFundConfiguration CommunityFund() => new CommunityFundConfiguration(
                "community-fund", CommunityFundMonthlyCopper, FeastReserveCopper,
                MoneySourcesSetup.Market, EventVisibility.Normal);

            /// <summary>Daily aging of every perishable lot in the village.</summary>
            public SpoilageConfiguration Spoilage() => new SpoilageConfiguration(
                "spoilage", _catalog, MoneySourcesSetup.Market, EventVisibility.Quiet);

            /// <summary>The prosperity-scaled import order policy for the merchant.</summary>
            public IImportOrderPolicy ImportOrders() => new ProsperityImportPolicy();

            /// <summary>
            /// The merchant's visits with adaptive import orders: the same P2-08 offers,
            /// with Tilda's restock targets scaled by prosperity.
            /// </summary>
            public MerchantConfiguration MerchantVisitsAdaptive()
            {
                MerchantConfiguration plain = _money.MerchantVisits();
                return new MerchantConfiguration(plain.Id, plain.Market, plain.PurchaseOffers,
                    plain.ImportOffers, plain.BudgetPerVisitCopper, plain.Visibility,
                    new ProsperityImportPolicy());
            }

            private Wallet TaxpayerWallet(NpcId taxpayer)
            {
                // The business owners pay from their tills (the design's rule); everyone
                // else from their personal wallet.
                if (taxpayer.Equals(new NpcId("npc_tilda_bray"))) return _money.TildaTill;
                if (taxpayer.Equals(new NpcId("npc_doran_kettle"))) return _money.DoranTill;
                if (taxpayer.Equals(new NpcId("npc_oda_fenn"))) return _money.OdaTill;
                if (taxpayer.Equals(new NpcId("npc_garrick_alder"))) return _money.GarrickTill;
                if (!_money.Wallets.TryGetValue(taxpayer, out Wallet wallet))
                    throw new InvalidOperationException(
                        "MoneySinksSetup requires MoneySourcesSetup to have registered a wallet for " + taxpayer.Value + ".");
                return wallet;
            }
        }
    }
}
