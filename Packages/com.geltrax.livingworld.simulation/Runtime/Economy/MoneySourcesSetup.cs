using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// World-build step that opens the village's money gates (ECONOMY.md §6-7): the traveling
    /// merchant's buying and import offers, seasonal traveler spend, the crown's wolf bounties,
    /// and the village fund with its levies and wages. Run after GeneralStoreSetup and
    /// SmithySetup — the merchant needs Tilda's store and Doran's smithy to exist.
    ///
    /// Export stocks (apples, timber, honey, pelts, smoked fish) are the sellers' personal
    /// belongings: they sell down and are NOT produced yet. Timber cutting, honey, and pelts
    /// need a production task; until then the merchant buys what's on hand, up to ~800
    /// copper per visit. Smoked fish stands in for the design's "smoked meat" (Bessa's
    /// smokehouse; item_fish_smoked is the approved smoked good).
    ///
    /// Dye is on Tilda's design-doc order list but her shop has no dye price line and Shop
    /// cannot gain one post-construction; the default wiring restocks only the four imports
    /// she already sells (salt, both cloths, lamp oil). Dye follows when price lines can.
    ///
    /// Personal wallets are seeded from Content/npcs/npcs.json "money". Doran, Tilda, Oda
    /// and Garrick pay the levy from their business tills instead of a personal wallet —
    /// their wealth sits in the smithy, the store, the bakery and the mill.
    /// </summary>
    public static class MoneySourcesSetup
    {
        /// <summary>Market Square, where the merchant's cart sets up.</summary>
        public static readonly LocationId Market = new LocationId("loc_square");

        /// <summary>The Hearthside tavern (Bessa Marlowe).</summary>
        public static readonly LocationId Tavern = new LocationId("loc_tavern");

        /// <summary>Forest edge, where Ralf hunts and brings wolf pelts.</summary>
        public static readonly LocationId ForestEdge = new LocationId("loc_forest_edge");

        /// <summary>Alder Farm, where the harvest hands work.</summary>
        public static readonly LocationId Farm = new LocationId("loc_farm");

        // Sellers and their export goods.
        public static readonly NpcId Mira = new NpcId("npc_mira_holt");
        public static readonly NpcId Tam = new NpcId("npc_tam_oakes");
        public static readonly NpcId Maren = new NpcId("npc_maren_alder");
        public static readonly NpcId Ralf = new NpcId("npc_ralf_hale");
        public static readonly NpcId Bessa = new NpcId("npc_bessa_marlowe");

        // Buyers, officials, earners.
        public static readonly NpcId Tilda = new NpcId("npc_tilda_bray");
        public static readonly NpcId Doran = new NpcId("npc_doran_kettle");
        public static readonly NpcId Oda = new NpcId("npc_oda_fenn");
        public static readonly NpcId Garrick = new NpcId("npc_garrick_alder");
        public static readonly NpcId Bram = new NpcId("npc_bram_stone");
        public static readonly NpcId Elswith = new NpcId("npc_elswith_alder");
        public static readonly NpcId Corvin = new NpcId("npc_corvin_alder");
        public static readonly NpcId Jory = new NpcId("npc_jory_reed");

        // Export goods (Content/items/items.json).
        public static readonly ItemTypeId Apples = new ItemTypeId("item_apple");
        public static readonly ItemTypeId Logs = new ItemTypeId("item_log");
        public static readonly ItemTypeId Honey = new ItemTypeId("item_honey");
        public static readonly ItemTypeId Pelts = new ItemTypeId("item_pelt");
        public static readonly ItemTypeId SmokedFish = new ItemTypeId("item_fish_smoked");

        // Import goods.
        public static readonly ItemTypeId Salt = new ItemTypeId("item_salt");
        public static readonly ItemTypeId ClothLocal = new ItemTypeId("item_cloth_local");
        public static readonly ItemTypeId ClothImported = new ItemTypeId("item_cloth_imported");
        public static readonly ItemTypeId LampOil = new ItemTypeId("item_lamp_oil");
        public static readonly ItemTypeId Iron = new ItemTypeId("item_iron_stock");

        // Merchant economics (ECONOMY.md §6): ~800 copper per visit, every ~3 weeks in warm
        // months. Buy prices sit below village retail — the merchant needs the road margin.
        private const int MerchantBudgetPerVisit = 800;
        private const int FirstVisitMinDay = 10;
        private const int FirstVisitDaySpread = 7;

        // Export opening stocks and the merchant's buy prices.
        private const int MiraAppleStock = 45;
        private const int AppleBuyPrice = 2;
        private const int TamLogStock = 10;
        private const int LogBuyPrice = 6;
        private const int MarenHoneyStock = 10;
        private const int HoneyBuyPrice = 6;
        private const int RalfPeltStock = 5;
        private const int PeltBuyPrice = 8;
        private const int BessaSmokedFishStock = 10;
        private const int SmokedFishBuyPrice = 3;

        // Tilda's import restock targets (= P2-05 opening levels) at approved baseValue.
        private const int SaltTarget = 24;
        private const int SaltPrice = 6;
        private const int ClothLocalTarget = 4;
        private const int ClothLocalPrice = 25;
        private const int ClothImportedTarget = 6;
        private const int ClothImportedPrice = 40;
        private const int LampOilTarget = 20;
        private const int LampOilPrice = 12;

        // Doran's iron: 20 kg at the design-doc merchant price of ~10 copper/kg, delivered
        // only while P2-07's exhaustion order is pending.
        private const int IronTargetKg = 20;
        private const int IronPricePerKg = 10;

        // Wolf bounty: 50 copper per pelt (ECONOMY.md §6), paid to Ralf the hunter.
        private const int BountyPerPelt = 50;

        // Harvest wage: 8 copper/day in autumn (ECONOMY.md §7).
        private const int HarvestWagePerDay = 8;

        // Village fund (ECONOMY.md §7).
        private const int FundStartingCopper = 600;
        private const int LevyCopperPerWeek = 2;
        private const int BackgroundCopperPerWeek = 185;
        private const int BramWagePerDay = 20;
        private const int ElswithStipendPerDay = 8;
        private const int RalfRetainerPerWeek = 60;

        // Traveler spend shares of each month's total: the tavern takes the lion's share.
        private const int BessaTravelerWeight = 7;
        private const int TildaTravelerWeight = 3;

        // Personal starting wallets from Content/npcs/npcs.json ("money"), for the NPCs whose
        // wealth is not already in a shop till or mill belongings.
        private static readonly KeyValuePair<string, int>[] PersonalWallets =
        {
            new KeyValuePair<string, int>("npc_mira_holt", 850),
            new KeyValuePair<string, int>("npc_ralf_hale", 250),
            new KeyValuePair<string, int>("npc_corvin_alder", 1200),
            new KeyValuePair<string, int>("npc_maren_alder", 200),
            new KeyValuePair<string, int>("npc_piotr_alder", 80),
            new KeyValuePair<string, int>("npc_lida_alder", 8),
            new KeyValuePair<string, int>("npc_bessa_marlowe", 1100),
            new KeyValuePair<string, int>("npc_bram_stone", 450),
            new KeyValuePair<string, int>("npc_sima_fenn", 150),
            new KeyValuePair<string, int>("npc_tom_fenn", 30),
            new KeyValuePair<string, int>("npc_sella_wren", 400),
            new KeyValuePair<string, int>("npc_tansy_alder", 12),
            new KeyValuePair<string, int>("npc_elswith_alder", 500),
            new KeyValuePair<string, int>("npc_tam_oakes", 350),
            new KeyValuePair<string, int>("npc_brynn_oakes", 120),
            new KeyValuePair<string, int>("npc_jory_reed", 300),
        };

        /// <summary>
        /// Registers personal wallets, stocks the export goods, initializes the merchant
        /// schedule, traveler payouts, wolf bounties and the village fund, and returns the
        /// handle. The general store and smithy must already be registered; the bakery is
        /// optional (Oda's levy falls back to a personal wallet without it).
        /// </summary>
        public static MoneySourcesHandle Stock(WorldState state, ItemCatalog catalog,
            Shop generalStore, Shop smithy, Shop bakery = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (generalStore == null) throw new ArgumentNullException(nameof(generalStore));
            if (smithy == null) throw new ArgumentNullException(nameof(smithy));

            var wallets = RegisterPersonalWallets(state, catalog);
            var exportStocks = StockExports(state, catalog, wallets);

            Wallet tildaTill = generalStore.OwnerWallet;
            Wallet doranTill = smithy.OwnerWallet;
            Wallet odaTill = bakery != null ? bakery.OwnerWallet : PersonalWallet(state, catalog, wallets, Oda, 700);
            Wallet garrickTill = MillWallet(state, catalog, wallets);

            // Progress cursors start at the world's start day: a world opened mid-year must
            // not collect months of back-levies or back-wages on its first tick.
            long startDay = state.Clock.Day;
            state.RestoreMerchantSchedule(new MerchantScheduleState(initialized: true,
                nextVisitDay: startDay + FirstVisitMinDay + state.Rng.NextInt(FirstVisitDaySpread)));
            var fund = new VillageFundState(initialized: true, startingCopper: FundStartingCopper);
            fund.LastWageDay = startDay - 1;
            fund.LastLevyDay = startDay;
            fund.LastRetainerDay = startDay;
            state.RestoreVillageFund(fund);
            var travelers = new TravelerSpendState(initialized: true,
                lastPayoutDay: startDay - 1, monthIndex: VillageCalendar.MonthIndex(state.Clock),
                paidThisMonth: new List<int> { 0, 0 }.AsReadOnly());
            state.RestoreTravelerSpend(travelers);
            state.RestoreWolfBounty(new WolfBountyState(initialized: true));
            var harvest = new HarvestState(initialized: true, lastWageDay: startDay - 1);
            state.RestoreHarvest(harvest);

            return new MoneySourcesHandle(generalStore, smithy, wallets, exportStocks,
                tildaTill, doranTill, odaTill, garrickTill);
        }

        /// <summary>
        /// Rebuilds the money-gate handle from a loaded world state (post-save/load
        /// reassembly, e.g. the determinism proof or the future Unity Bridge): looks up
        /// the persisted wallets, export stocks and tills instead of creating them.
        /// The progress states (merchant schedule, fund, travelers, bounties, harvest)
        /// are restored by the loader, not rebuilt here.
        /// </summary>
        public static MoneySourcesHandle Reassemble(WorldState state,
            Shop generalStore, Shop smithy, Shop bakery)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (generalStore == null) throw new ArgumentNullException(nameof(generalStore));
            if (smithy == null) throw new ArgumentNullException(nameof(smithy));
            if (bakery == null) throw new ArgumentNullException(nameof(bakery));

            var wallets = new Dictionary<NpcId, Wallet>();
            foreach (KeyValuePair<string, int> row in PersonalWallets)
            {
                var npc = new NpcId(row.Key);
                var owner = ActorId.ForNpc(npc);
                if (!state.Belongings.TryGet(owner, out NpcBelongingsEntry entry))
                    throw new InvalidOperationException(
                        "Loaded world has no belongings for '" + row.Key + "'.");
                wallets[npc] = entry.Wallet;
            }

            // Export stocks are the sellers' own belongings inventories (see StockExports).
            var exportStocks = new Dictionary<NpcId, Inventory>
            {
                { Mira, state.Belongings[ActorId.ForNpc(Mira)].Inventory },
                { Tam, state.Belongings[ActorId.ForNpc(Tam)].Inventory },
                { Maren, state.Belongings[ActorId.ForNpc(Maren)].Inventory },
                { Ralf, state.Belongings[ActorId.ForNpc(Ralf)].Inventory },
                { Bessa, state.Belongings[ActorId.ForNpc(Bessa)].Inventory },
            };

            return new MoneySourcesHandle(generalStore, smithy, wallets, exportStocks,
                generalStore.OwnerWallet, smithy.OwnerWallet, bakery.OwnerWallet,
                state.Belongings[ActorId.ForNpc(Garrick)].Wallet);
        }

        private static Dictionary<NpcId, Wallet> RegisterPersonalWallets(
            WorldState state, ItemCatalog catalog)
        {
            var wallets = new Dictionary<NpcId, Wallet>();
            foreach (KeyValuePair<string, int> row in PersonalWallets)
            {
                var npc = new NpcId(row.Key);
                var owner = ActorId.ForNpc(npc);
                if (state.Belongings.TryGet(owner, out NpcBelongingsEntry existing))
                {
                    wallets[npc] = existing.Wallet;
                    continue;
                }
                var wallet = new Wallet(row.Value);
                state.Belongings.Register(owner, new Inventory(catalog), wallet);
                wallets[npc] = wallet;
            }
            return wallets;
        }

        private static Dictionary<NpcId, Inventory> StockExports(WorldState state, ItemCatalog catalog,
            Dictionary<NpcId, Wallet> wallets)
        {
            var stocks = new Dictionary<NpcId, Inventory>();
            stocks[Mira] = AddExport(state, catalog, wallets, Mira, Apples, MiraAppleStock);
            stocks[Tam] = AddExport(state, catalog, wallets, Tam, Logs, TamLogStock);
            stocks[Maren] = AddExport(state, catalog, wallets, Maren, Honey, MarenHoneyStock);
            stocks[Ralf] = AddExport(state, catalog, wallets, Ralf, Pelts, RalfPeltStock);
            stocks[Bessa] = AddExport(state, catalog, wallets, Bessa, SmokedFish, BessaSmokedFishStock);
            return stocks;
        }

        private static Inventory AddExport(WorldState state, ItemCatalog catalog,
            Dictionary<NpcId, Wallet> wallets, NpcId seller, ItemTypeId item, int quantity)
        {
            // The seller already has a belongings entry from RegisterPersonalWallets; reuse its
            // inventory and wallet so there is exactly one of each per NPC.
            var owner = ActorId.ForNpc(seller);
            if (!state.Belongings.TryGet(owner, out NpcBelongingsEntry entry))
                throw new InvalidOperationException("Seller has no registered belongings.");
            entry.Inventory.Add(item, quantity);
            wallets[seller] = entry.Wallet;
            return entry.Inventory;
        }

        private static Wallet PersonalWallet(WorldState state, ItemCatalog catalog,
            Dictionary<NpcId, Wallet> wallets, NpcId npc, int startingCopper)
        {
            // Oda's and Garrick's wealth sits in their businesses when those are built; without
            // the business they get a personal wallet like everyone else (Content money).
            var owner = ActorId.ForNpc(npc);
            if (state.Belongings.TryGet(owner, out NpcBelongingsEntry entry))
                return entry.Wallet;
            var wallet = new Wallet(startingCopper);
            state.Belongings.Register(owner, new Inventory(catalog), wallet);
            wallets[npc] = wallet;
            return wallet;
        }

        private static Wallet MillWallet(WorldState state, ItemCatalog catalog,
            Dictionary<NpcId, Wallet> wallets)
        {
            // Garrick's mill stock and till were registered as his belongings by the bakery
            // chain setup; without it he gets a personal wallet like everyone else.
            return PersonalWallet(state, catalog, wallets, Garrick, 600);
        }

        /// <summary>Caller-owned handle to the wired money gates: systems' configurations.</summary>
        public sealed class MoneySourcesHandle
        {
            internal MoneySourcesHandle(Shop generalStore, Shop smithy,
                Dictionary<NpcId, Wallet> wallets, Dictionary<NpcId, Inventory> exportStocks,
                Wallet tildaTill, Wallet doranTill, Wallet odaTill, Wallet garrickTill)
            {
                GeneralStore = generalStore;
                Smithy = smithy;
                Wallets = wallets;
                ExportStocks = exportStocks;
                TildaTill = tildaTill;
                DoranTill = doranTill;
                OdaTill = odaTill;
                GarrickTill = garrickTill;
            }

            public Shop GeneralStore { get; }
            public Shop Smithy { get; }
            public IReadOnlyDictionary<NpcId, Wallet> Wallets { get; }
            public IReadOnlyDictionary<NpcId, Inventory> ExportStocks { get; }
            public Wallet TildaTill { get; }
            public Wallet DoranTill { get; }
            public Wallet OdaTill { get; }
            public Wallet GarrickTill { get; }

            public MerchantConfiguration MerchantVisits() => new MerchantConfiguration(
                "merchant-alder-road", Market,
                new List<MerchantPurchaseOffer>
                {
                    new MerchantPurchaseOffer(Mira, ExportStocks[Mira], Wallets[Mira],
                        Apples, AppleBuyPrice, MiraAppleStock, Market),
                    new MerchantPurchaseOffer(Tam, ExportStocks[Tam], Wallets[Tam],
                        Logs, LogBuyPrice, TamLogStock, Market),
                    new MerchantPurchaseOffer(Maren, ExportStocks[Maren], Wallets[Maren],
                        Honey, HoneyBuyPrice, MarenHoneyStock, Market),
                    new MerchantPurchaseOffer(Ralf, ExportStocks[Ralf], Wallets[Ralf],
                        Pelts, PeltBuyPrice, RalfPeltStock, Market),
                    new MerchantPurchaseOffer(Bessa, ExportStocks[Bessa], Wallets[Bessa],
                        SmokedFish, SmokedFishBuyPrice, BessaSmokedFishStock, Market),
                },
                new List<MerchantImportOffer>
                {
                    // Doran's iron first: it only fires while his exhaustion order is pending.
                    new MerchantImportOffer(Doran, Smithy.Stock, DoranTill, Iron, IronTargetKg,
                        IronPricePerKg, Smithy.Location, requiresExhaustionOrder: true),
                    new MerchantImportOffer(Tilda, GeneralStore.Stock, TildaTill, Salt, SaltTarget,
                        SaltPrice, GeneralStore.Location),
                    new MerchantImportOffer(Tilda, GeneralStore.Stock, TildaTill, ClothLocal,
                        ClothLocalTarget, ClothLocalPrice, GeneralStore.Location),
                    new MerchantImportOffer(Tilda, GeneralStore.Stock, TildaTill, ClothImported,
                        ClothImportedTarget, ClothImportedPrice, GeneralStore.Location),
                    new MerchantImportOffer(Tilda, GeneralStore.Stock, TildaTill, LampOil,
                        LampOilTarget, LampOilPrice, GeneralStore.Location),
                },
                MerchantBudgetPerVisit, EventVisibility.Normal);

            public TravelerConfiguration TravelerSpend() => new TravelerConfiguration(
                "travelers-alder-road",
                new List<TravelerPayee>
                {
                    new TravelerPayee(Bessa, Wallets[Bessa], Tavern, BessaTravelerWeight),
                    new TravelerPayee(Tilda, TildaTill, GeneralStore.Location, TildaTravelerWeight),
                },
                EventVisibility.Normal);

            public WolfBountyConfiguration WolfBounties() => new WolfBountyConfiguration(
                "bounty-wolves", Ralf, Wallets[Ralf], ForestEdge, BountyPerPelt, EventVisibility.Normal);

            /// <summary>
            /// Corvin's harvest payroll. The hands list starts empty: the named villagers are
            /// unpaid family labor, so workers are registered when they take harvest work.
            /// </summary>
            public HarvestConfiguration Harvest() => new HarvestConfiguration(
                "harvest-alder-farm", Corvin, Wallets[Corvin],
                new List<HarvestHand>().AsReadOnly(), HarvestWagePerDay, Farm,
                EventVisibility.Normal);

            public VillageFundConfiguration VillageFund()
            {
                var payers = new List<LevyPayer>();
                // The 20 pay the household levy (ECONOMY.md §7); the four whose wealth sits in a
                // business pay from its till — unless the business wasn't built, in which case
                // they already hold a personal wallet above.
                foreach (KeyValuePair<NpcId, Wallet> row in Wallets)
                    payers.Add(new LevyPayer(row.Key, row.Value));
                if (!Wallets.ContainsKey(Doran)) payers.Add(new LevyPayer(Doran, DoranTill));
                if (!Wallets.ContainsKey(Tilda)) payers.Add(new LevyPayer(Tilda, TildaTill));
                if (!Wallets.ContainsKey(Oda)) payers.Add(new LevyPayer(Oda, OdaTill));
                if (!Wallets.ContainsKey(Garrick)) payers.Add(new LevyPayer(Garrick, GarrickTill));
                return new VillageFundConfiguration(
                    "fund-village", payers, LevyCopperPerWeek,
                    new List<WageEarner>
                    {
                        new WageEarner(Bram, Wallets[Bram], BramWagePerDay,
                            new LocationId("loc_guard_post")),
                        new WageEarner(Elswith, Wallets[Elswith], ElswithStipendPerDay, Market),
                    },
                    Ralf, Wallets[Ralf], ForestEdge,
                    RalfRetainerPerWeek, BackgroundCopperPerWeek, Market, EventVisibility.Normal);
            }
        }
    }
}
