using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// World-build step that opens Doran Kettle's Ember &amp; Iron Smithy (loc_blacksmith) from
    /// ECONOMY.md §"Iron: merchants → smithy → village": 20 kg of imported iron, a 900-copper
    /// till, and a product rack the forge keeps stocked (nails, tools, horseshoes) sold through
    /// the existing shop flow. The smithy's stock and till live in the registered Shop, so the
    /// saver persists them; Doran holds no separate belongings entry — his wealth is his business.
    ///
    /// Nails: Doran and Tilda Bray COMPETE; Doran does not supply her wholesale. Reasons:
    /// (1) money is integer copper and Doran retails nails at 1 copper, so a wholesale price
    /// below 1 copper is unrepresentable — Tilda cannot buy nails below cost; (2) the design
    /// doc fixes both sellers at 1 copper, leaving no margin for a wholesale tier; (3) it keeps
    /// this task in scope — Tilda's 200-nail stock (P2-05) simply sells down until traveling
    /// merchants restock her in P2-08, while Doran forges his own nails. Villagers choose
    /// between the two shops through the existing (Agents-side) shop-choice rules.
    ///
    /// Tool pricing: a fixed 60 copper, the midpoint of the design doc's 40–80 range. Doran's
    /// tools are new-made at the forge; Tilda's 40-copper stock (P2-05) is older and cheaper,
    /// so villagers naturally buy hers first through the normal shop flow.
    /// </summary>
    public static class SmithySetup
    {
        /// <summary>Ember &amp; Iron Smithy, per Content/world/locations.json.</summary>
        public static readonly LocationId Smithy = new LocationId("loc_blacksmith");

        /// <summary>Doran Kettle, the smith, per Content/npcs/npcs.json.</summary>
        public static readonly NpcId Smith = new NpcId("npc_doran_kettle");

        public static readonly ItemTypeId Iron = new ItemTypeId("item_iron_stock");
        public static readonly ItemTypeId Nails = new ItemTypeId("item_nails");
        public static readonly ItemTypeId Tools = new ItemTypeId("item_tool_basic");
        public static readonly ItemTypeId Horseshoes = new ItemTypeId("item_horseshoes");

        // 20 kg of imported iron (ECONOMY.md). Iron restock via merchants comes in P2-08;
        // here the stock is finite and sells down.
        private const int IronStockKg = 20;

        // Doran's till: 900 copper (P2-07 brief). His wealth sits in the business.
        private const int StartingTillCopper = 900;

        // Retail prices. Nails and horseshoes at their approved baseValue; tools at the fixed
        // 60 documented above.
        private const int NailPriceCopper = 1;
        private const int ToolPriceCopper = 60;
        private const int HorseshoePriceCopper = 12;

        // Forge recipes (ECONOMY.md): 1 kg -> 20 nails; 2 kg -> 1 tool; 1 kg -> 1 horseshoe set.
        // Targets are Doran's standing rack levels: a builder's handful of nails, a couple of
        // tools and a couple of shoe sets on hand. The rack starts empty — yesterday's work
        // sold through — and the forge fills it on the first ticks.
        private const int NailsIronKgPerBatch = 1;
        private const int NailsPerBatch = 20;
        private const int NailTargetStock = 60;
        private const int ToolsIronKgPerBatch = 2;
        private const int ToolsPerBatch = 1;
        private const int ToolTargetStock = 2;
        private const int HorseshoesIronKgPerBatch = 1;
        private const int HorseshoesPerBatch = 1;
        private const int HorseshoeTargetStock = 2;

        // When the iron runs out Doran logs a standing order for a full 20 kg restock at the
        // design-doc merchant price of ~10 copper/kg (200 copper). P2-08 merchants fulfill it.
        private const int IronOrderQuantityKg = 20;
        private const int IronOrderPricePerKg = 10;

        /// <summary>
        /// Stocks the smithy's iron, sets the till, registers the smithy as a shop and returns
        /// the handle. The caller registers the forging system from the handle's factory.
        /// </summary>
        public static SmithyHandle Stock(WorldState state, ItemCatalog catalog)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var stock = new Inventory(catalog);
            stock.Add(Iron, IronStockKg);
            var prices = new List<KeyValuePair<ItemTypeId, int>>
            {
                new KeyValuePair<ItemTypeId, int>(Nails, NailPriceCopper),
                new KeyValuePair<ItemTypeId, int>(Tools, ToolPriceCopper),
                new KeyValuePair<ItemTypeId, int>(Horseshoes, HorseshoePriceCopper),
            };
            var shop = new Shop(Smithy, Smith, stock, new Wallet(StartingTillCopper), prices);
            state.Shops.Register(shop);

            return new SmithyHandle(shop);
        }

        /// <summary>Caller-owned handle to the stocked smithy: shop and forging configuration.</summary>
        public sealed class SmithyHandle
        {
            public SmithyHandle(Shop smithyShop)
            {
                SmithyShop = smithyShop ?? throw new ArgumentNullException(nameof(smithyShop));
            }

            public Shop SmithyShop { get; }

            public SmithyConfiguration Forging() => new SmithyConfiguration(
                "smithy-doran", Smithy, Smith, SmithyShop.Stock, Iron,
                new List<SmithyRecipe>
                {
                    new SmithyRecipe(Nails, NailsIronKgPerBatch, NailsPerBatch, NailTargetStock),
                    new SmithyRecipe(Tools, ToolsIronKgPerBatch, ToolsPerBatch, ToolTargetStock),
                    new SmithyRecipe(Horseshoes, HorseshoesIronKgPerBatch, HorseshoesPerBatch,
                        HorseshoeTargetStock),
                },
                IronOrderQuantityKg, IronOrderPricePerKg, EventVisibility.Normal);
        }
    }
}
