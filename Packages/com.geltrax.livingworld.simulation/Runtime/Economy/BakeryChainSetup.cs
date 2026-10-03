using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// World-build step that opens the Crust &amp; Crumb Bakery (loc_bakery, Oda Fenn) and stocks
    /// Garrick's mill (loc_mill) for the grain → flour → bread chain from ECONOMY.md §5:
    /// Corvin's grain is ground at the mill (miller's toll of 1/12 in kind), Oda buys flour at
    /// 10 copper/sack, and bakes ~40 loaves each morning in her single oven.
    /// </summary>
    public static class BakeryChainSetup
    {
        /// <summary>Crust &amp; Crumb Bakery, per Content/world/locations.json.</summary>
        public static readonly LocationId Bakery = new LocationId("loc_bakery");

        /// <summary>Oda Fenn, the baker, per Content/npcs/npcs.json.</summary>
        public static readonly NpcId Baker = new NpcId("npc_oda_fenn");

        /// <summary>The village mill, per Content/world/locations.json.</summary>
        public static readonly LocationId Mill = new LocationId("loc_mill");

        /// <summary>Garrick Alder, the miller, per Content/npcs/npcs.json.</summary>
        public static readonly NpcId Miller = new NpcId("npc_garrick_alder");

        public static readonly ItemTypeId Grain = new ItemTypeId("item_grain");
        public static readonly ItemTypeId Flour = new ItemTypeId("item_flour");
        public static readonly ItemTypeId RyeBread = new ItemTypeId("item_bread_rye");
        public static readonly ItemTypeId BarleyBread = new ItemTypeId("item_bread_barley");

        // Mill grain: ~30 sacks. Oda bakes 2 sacks of flour a day, and 12 grain grind into 11
        // flour, so the bakery needs ~2.2 sacks of grain a day: this is about two weeks of
        // milling. The rest of the ~100-sack autumn harvest sits in the granary and on the
        // farm; Corvin's carting from granary to mill is a later task.
        private const int MillGrainSacks = 30;

        // Mill flour: 6 sacks, three days of Oda's baking. Her wholesale orders refill from
        // here, and the mill grinds its grain stock down promptly.
        private const int MillFlourSacks = 6;

        // Bakery flour: 4 sacks, two days on hand. The restock threshold below is also 4, so
        // her first morning bake (2 sacks) drops her under it and the 6-sack order goes out.
        private const int BakeryFlourSacks = 4;

        // Bread starts at zero: yesterday's bake sold out, which is the usual day
        // ("sells out by early afternoon most days"). The first in-simulation bake fires at
        // the first tick at or after 05:00.
        private const int BakeryRyeLoaves = 0;
        private const int BakeryBarleyLoaves = 0;

        // Oda's till: ~400 copper, roughly four days of bread revenue (20 rye at 3 plus
        // 20 barley at 2 is 100 a day). Covers a 6-sack flour order (60 copper) with room.
        private const int BakeryStartingCopper = 400;

        // Garrick's till: 150 copper. The toll pays him in kind, not coin, and his cash fees
        // are small; the mill's wealth is its grain and flour.
        private const int MillerStartingCopper = 150;

        // Milling: 12 sacks per batch with the traditional 1/12 toll in kind (ECONOMY.md §5).
        private const int GrainPerBatch = 12;
        private const int TollSacksPerBatch = 1;

        // Baking: 2 sacks a morning, 20 loaves a sack — 10 rye and 10 barley. The design doc
        // says ~2.5 sacks a day for ~40 loaves; 2 sacks at 20 loaves is the same ~40 loaves
        // in whole sacks. Rye is the staple loaf, barley the cheaper one; the 50/50 split is
        // the setup's choice (the design doc does not fix it) and keeps both flowing.
        private const int SacksPerBake = 2;
        private const int RyeLoavesPerSack = 10;
        private const int BarleyLoavesPerSack = 10;

        // The oven fires at the first tick at or after 05:00 (ECONOMY.md: "morning bake").
        private const int BakeHour = 5;

        // Flour restock: Oda orders when her flour drops below 4 sacks (two days of baking),
        // 6 sacks at a time (three days) at the design-doc wholesale price of 10 copper/sack.
        // Partial delivery is allowed: the mill may be grinding.
        private const int FlourLowThreshold = 4;
        private const int FlourOrderQuantity = 6;
        private const int FlourWholesalePrice = 10;

        /// <summary>
        /// Builds the bakery shop, Garrick's mill stock and till, registers both on the world,
        /// and returns the chain handle. The caller registers the milling, baking and flour
        /// restock systems from the handle's factories. Garrick's mill stock and till are his
        /// registered belongings, so they persist with the rest of the world's inventories.
        /// </summary>
        public static BakeryChain Stock(WorldState state, ItemCatalog catalog)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var millStock = new Inventory(catalog);
            millStock.Add(Grain, MillGrainSacks);
            millStock.Add(Flour, MillFlourSacks);
            var millerWallet = new Wallet(MillerStartingCopper);
            state.Belongings.Register(ActorId.ForNpc(Miller), millStock, millerWallet);

            var bakeryStock = new Inventory(catalog);
            bakeryStock.Add(Flour, BakeryFlourSacks);
            // Bread starts at zero: Inventory.Add rejects zero quantities, and Count
            // returns 0 for absent items, so yesterday's sold-out bake needs no entries.
            if (BakeryRyeLoaves > 0) bakeryStock.Add(RyeBread, BakeryRyeLoaves);
            if (BakeryBarleyLoaves > 0) bakeryStock.Add(BarleyBread, BakeryBarleyLoaves);
            var prices = new List<KeyValuePair<ItemTypeId, int>>
            {
                // Retail loaves at their approved baseValue. Flour is listed at cost: Oda
                // sells a sack to neighbours at the 10 copper she pays for it.
                new KeyValuePair<ItemTypeId, int>(RyeBread, catalog[RyeBread].BaseValue),
                new KeyValuePair<ItemTypeId, int>(BarleyBread, catalog[BarleyBread].BaseValue),
                new KeyValuePair<ItemTypeId, int>(Flour, FlourWholesalePrice),
            };
            var bakery = new Shop(Bakery, Baker, bakeryStock, new Wallet(BakeryStartingCopper), prices);
            state.Shops.Register(bakery);

            return new BakeryChain(bakery, millStock, millerWallet);
        }

        /// <summary>Caller-owned handle to the stocked chain: shops, stocks and system configs.</summary>
        public sealed class BakeryChain
        {
            public BakeryChain(Shop bakeryShop, Inventory millStock, Wallet millerWallet)
            {
                BakeryShop = bakeryShop ?? throw new ArgumentNullException(nameof(bakeryShop));
                MillStock = millStock ?? throw new ArgumentNullException(nameof(millStock));
                MillerWallet = millerWallet ?? throw new ArgumentNullException(nameof(millerWallet));
            }

            public Shop BakeryShop { get; }
            public Inventory MillStock { get; }
            public Wallet MillerWallet { get; }

            public MillingConfiguration Milling() => new MillingConfiguration(
                "mill-garrick", Mill, Miller, MillStock, Grain, Flour,
                GrainPerBatch, TollSacksPerBatch, EventVisibility.Normal);

            public BakingConfiguration Baking() => new BakingConfiguration(
                "bakery-oda", Bakery, Baker, BakeryShop.Stock, Flour, RyeBread, BarleyBread,
                SacksPerBake, RyeLoavesPerSack, BarleyLoavesPerSack, BakeHour, EventVisibility.Normal);

            public RestockConfiguration FlourRestock() => new RestockConfiguration(
                "flour-mill-to-bakery", BakeryShop, Mill, Miller, MillStock, MillerWallet,
                Flour, FlourLowThreshold, FlourOrderQuantity, FlourWholesalePrice,
                RestockFulfillmentPolicy.AllowPartial, EventVisibility.Normal);
        }
    }
}
