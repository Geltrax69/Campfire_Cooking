using System;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// NPCs buy their daily bread: once a day at 09:00 (after the shops open), any
    /// NPC whose food store holds less than <see cref="LowPantryThreshold"/> hunger
    /// points' worth of food buys up to <see cref="DailyRyeLoaves"/> rye loaves (the
    /// staple) plus <see cref="DailyBarleyLoaves"/> barley loaves (the cheaper loaf) —
    /// about a day's food — from the bakery, so the whole bake sells through instead
    /// of barley rotting on the shelf. Daily small purchases (not bulk stockpiling)
    /// keep the bakery selling out most days and give the relationship system a steady
    /// drumbeat of honest trades. Purchases go through <see cref="Shop.Purchase"/>,
    /// so friend discounts, stock limits and money conservation all apply exactly as
    /// for any other buyer. Shop locations are public village knowledge: every
    /// villager knows where the bakery is.
    ///
    /// A buyer who finds the shelf empty notices: they form a StockMissing belief
    /// (seen, high confidence) — "no bread at the bakery" is exactly the kind of
    /// news that travels at the tavern. This is the NPC's own cognition recorded by
    /// their behavior system, following the P2-10 precedent for cross-area writes.
    /// </summary>
    public sealed class ShoppingSystem : IWorldSystem
    {
        /// <summary>Below this food value (hunger points) an NPC goes shopping.</summary>
        public const int LowPantryThreshold = 150;

        /// <summary>Loaves bought per day: 2 rye (staple) + 2 barley (cheaper loaf).</summary>
        public const int DailyRyeLoaves = 2;

        /// <summary>Loaves bought per day: 2 rye (staple) + 2 barley (cheaper loaf).</summary>
        public const int DailyBarleyLoaves = 2;

        private static readonly LocationId BakeryLocation = new LocationId("loc_bakery");
        private static readonly ItemTypeId RyeBread = new ItemTypeId("item_bread_rye");
        private static readonly ItemTypeId BarleyBread = new ItemTypeId("item_bread_barley");
        private const string FoodCategory = "food";
        private const int ShoppingHour = 9;

        /// <summary>Confidence of a seen empty shelf: the buyer looked right at it.</summary>
        private const int SelloutSeenConfidence = 90;

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
            foreach (NpcState npc in state.Npcs.Npcs)
                TryBuyDailyBread(state, npc, bakery);
        }

        private void TryBuyDailyBread(WorldState state, NpcState npc, Shop bakery)
        {
            var buyer = ActorId.ForNpc(npc.Definition.Id);
            if (!state.Belongings.TryGet(buyer, out NpcBelongingsEntry belongings)) return;
            if (belongings.Wallet.Balance < 1) return;
            if (FoodHungerValue(belongings.Inventory) >= LowPantryThreshold) return;
            BuyLoaves(state, npc.Definition.Id, buyer, belongings, bakery, RyeBread, DailyRyeLoaves);
            if (Sells(bakery, BarleyBread))
                BuyLoaves(state, npc.Definition.Id, buyer, belongings, bakery, BarleyBread, DailyBarleyLoaves);
        }

        private static bool Sells(Shop bakery, ItemTypeId item)
        {
            foreach (var price in bakery.Prices)
                if (price.Key == item) return true;
            return false;
        }

        private void BuyLoaves(WorldState state, NpcId npcId, ActorId buyer,
            NpcBelongingsEntry belongings, Shop bakery, ItemTypeId item, int quantity)
        {
            var request = new PurchaseRequest(buyer, belongings.Inventory, belongings.Wallet,
                item, quantity, allowPartial: true);
            PurchaseResult result = bakery.Purchase(state, request);
            if (result.Outcome == PurchaseOutcome.Failed && bakery.Stock.Count(item) == 0)
                NoticeSellout(state, npcId, item);
        }

        private static void NoticeSellout(WorldState state, NpcId npcId, ItemTypeId item)
        {
            if (!state.Knowledge.TryGet(npcId, out BeliefStore store)) return;
            var claim = new BeliefClaim(BeliefClaimKind.StockMissing, BakeryLocation,
                itemType: item, quantity: 0);
            var source = new BeliefSource(BeliefSourceKind.Seen);
            store.Set(new Belief(claim, source, SelloutSeenConfidence, state.Clock));
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
