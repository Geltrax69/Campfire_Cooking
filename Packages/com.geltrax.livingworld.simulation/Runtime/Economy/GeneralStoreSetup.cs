using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// World-build step that opens Bray's general store (loc_general_store, owner npc_tilda_bray)
    /// stocked with the seven general goods defined in approved Content: salt, local and imported
    /// cloth, lamp oil, nails, rope and basic tools. Restocking via traveling merchants comes in
    /// P2-08; here the store starts stocked and sells down.
    /// </summary>
    public static class GeneralStoreSetup
    {
        /// <summary>Bray's General Store, per Content/world/locations.json.</summary>
        public static readonly LocationId Store = new LocationId("loc_general_store");

        /// <summary>Tilda Bray, the store owner, per Content/world/locations.json.</summary>
        public static readonly NpcId Owner = new NpcId("npc_tilda_bray");

        // Tilda's coin box opens bigger than Mira's 850 copper (Apple Test): the design doc
        // (ECONOMY.md) says her coin box runs bigger because imports are expensive and takings
        // sit longer between merchant visits.
        private const int StartingCoinBox = 1200;

        // Opening stock: enough for roughly three weeks of village demand, not infinite.
        // The design doc (ECONOMY.md) says traveling merchants restock the store about every
        // three weeks in warm months, so the store opens able to sell down between visits.
        // Unit prices are the items' baseValue from Content/items/items.json (the JSON field is
        // named "baseValue", not "basePriceCopper"); the design fixes local cloth retail at 25
        // copper and baseValue is already 25, so no price override is needed anywhere.
        private static readonly StockRow[] Rows =
        {
            // Everyone preserves meat and fish for winter; the game starts in early autumn, the
            // stockpiling season. ~8 pouches a week across the village is three weeks of demand.
            new StockRow("item_salt", 24),
            // Brynn weaves about 1 length per 2 weeks (ECONOMY.md); the store can only hold what
            // she can supply, so handmade local cloth is scarce: about a month of her output.
            new StockRow("item_cloth_local", 4),
            // Finer and pricier than local; one import batch on the shelf, slower turnover.
            new StockRow("item_cloth_imported", 6),
            // Every household lights winter evenings; early autumn is stock-up season.
            // About one flask per household covers two to three weeks.
            new StockRow("item_lamp_oil", 20),
            // Cheap bulk hardware for building and repairs; cheap goods sit in bulk.
            new StockRow("item_nails", 200),
            // Farm, mill and river work use coils steadily but slowly.
            new StockRow("item_rope", 12),
            // Durable kits at 40 copper: one per household lasts years, so few are on hand.
            new StockRow("item_tool_basic", 8),
        };

        /// <summary>
        /// Builds the general store's inventory, prices every good at its approved baseValue,
        /// and registers the shop on the world. The returned shop is owned by the world's
        /// ShopRegistry; the caller's WorldState is the source of truth from here on.
        /// </summary>
        public static Shop Stock(WorldState state, ItemCatalog catalog)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var stock = new Inventory(catalog);
            var prices = new List<KeyValuePair<ItemTypeId, int>>(Rows.Length);
            foreach (StockRow row in Rows)
            {
                // Fails fast if Content lacks the item: the code may not silently diverge from
                // approved data.
                var item = new ItemTypeId(row.Id);
                int unitPrice = catalog[item].BaseValue;
                stock.Add(item, row.Quantity);
                prices.Add(new KeyValuePair<ItemTypeId, int>(item, unitPrice));
            }

            var shop = new Shop(Store, Owner, stock, new Wallet(StartingCoinBox), prices);
            state.Shops.Register(shop);
            return shop;
        }

        private sealed class StockRow
        {
            internal StockRow(string id, int quantity)
            {
                Id = id;
                Quantity = quantity;
            }

            internal string Id { get; }
            internal int Quantity { get; }
        }
    }
}
