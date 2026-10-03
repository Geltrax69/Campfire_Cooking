using System;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// NPCs keep their pantries stocked: once a day at 09:00 (after the shops open),
    /// any NPC whose food store holds less than <see cref="LowPantryThreshold"/> hunger
    /// points' worth of food buys rye bread — the village staple — from the bakery,
    /// up to <see cref="TargetPantryValue"/> hunger points' worth. Purchases go through
    /// <see cref="Shop.Purchase"/>, so friend discounts, stock limits and money
    /// conservation all apply exactly as for any other buyer. Shop locations are public
    /// village knowledge: every villager knows where the bakery is.
    /// </summary>
    public sealed class ShoppingSystem : IWorldSystem
    {
        /// <summary>Below this food value (hunger points) an NPC goes shopping.</summary>
        public const int LowPantryThreshold = 150;

        /// <summary>Shopping fills the pantry up to this food value (hunger points).</summary>
        public const int TargetPantryValue = 270;

        private static readonly LocationId BakeryLocation = new LocationId("loc_bakery");
        private static readonly ItemTypeId RyeBread = new ItemTypeId("item_bread_rye");
        private const string FoodCategory = "food";
        private const int ShoppingHour = 9;

        private readonly ItemCatalog _catalog;

        public ShoppingSystem(ItemCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public string Id => "agents.shopping";
        public SimulationPhase Phase => SimulationPhase.Actions;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Clock.Hour != ShoppingHour || state.Clock.Minute != 0) return;
            if (!state.Shops.TryGet(BakeryLocation, out Shop bakery)) return;
            int hungerPerLoaf = -(_catalog[RyeBread].HungerEffect ?? 0);
            if (hungerPerLoaf <= 0) return;
            foreach (NpcState npc in state.Npcs.Npcs)
                TryRestockPantry(state, npc, bakery, hungerPerLoaf);
        }

        private void TryRestockPantry(WorldState state, NpcState npc, Shop bakery, int hungerPerLoaf)
        {
            var buyer = ActorId.ForNpc(npc.Definition.Id);
            if (!state.Belongings.TryGet(buyer, out NpcBelongingsEntry belongings)) return;
            if (belongings.Wallet.Balance < 1) return;
            int foodValue = FoodHungerValue(belongings.Inventory);
            if (foodValue >= LowPantryThreshold) return;
            int want = (TargetPantryValue - foodValue + hungerPerLoaf - 1) / hungerPerLoaf;
            if (want <= 0) return;
            var request = new PurchaseRequest(buyer, belongings.Inventory, belongings.Wallet,
                RyeBread, want, allowPartial: true);
            bakery.Purchase(state, request);
        }

        private int FoodHungerValue(Inventory pantry)
        {
            int total = 0;
            foreach (var row in pantry.Contents)
            {
                if (row.Value <= 0) continue;
                ItemDefinition definition = _catalog[row.Key];
                if (definition.Category != FoodCategory) continue;
                int restore = -(definition.HungerEffect ?? 0);
                if (restore > 0) total += restore * row.Value;
            }
            return total;
        }
    }
}
