using System;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Connects a finished cook to the living world (P3-03, SKILLS.md section 2):
    /// cooking raises ingredient demand, a cook working the tavern kitchen moves
    /// tavern popularity, and skilled cooks waste less. Call
    /// <see cref="RecordCook"/> after <see cref="RecipeExecution.Cook"/>; it only
    /// acts on successful cooks — a burned batch creates no demand and teaches the
    /// tavern nothing. Also owns the quality-adjusted sale price for cooked dishes
    /// and the direct sale of a cooked dish between two actors.
    /// </summary>
    public static class CookingEffects
    {
        // The village's evening room, from the approved Content/world/locations.json
        // (same convention as RelationshipDynamicsSystem).
        private static readonly LocationId Tavern = new LocationId("loc_tavern");

        /// <summary>Skill level at which a cook starts wasting less (SKILLS.md: level 2).</summary>
        public const int WasteSavingLevel = 2;

        /// <summary>
        /// Seeded percent chance that a level-2+ cook's success refunds one input
        /// unit. Simplification, documented per the brief: real waste is partial
        /// loss across many units; the books model it as all-or-nothing on one unit.
        /// </summary>
        public const int WasteRefundChancePercent = 50;

        /// <summary>
        /// Records a finished cook in the world: demand, tavern popularity and waste.
        /// Only successful cooks count. All randomness comes from the world's seeded
        /// RNG, so identical seeds give identical results.
        /// </summary>
        public static void RecordCook(WorldState state, RecipeDefinition recipe, NpcId cook,
            SkillStore skills, CookResult result, LocationId location)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (!cook.IsValid) throw new ArgumentException("A cook is required.", nameof(cook));
            if (skills == null) throw new ArgumentNullException(nameof(skills));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!location.IsValid) throw new ArgumentException("A cook location is required.", nameof(location));

            if (result.Outcome != CookOutcome.Success) return;

            int level = recipe.Skill.HasValue ? skills.GetLevel(recipe.Skill.Value) : 0;

            // Good cooked food bids up raw ingredients: one demand point per input unit.
            foreach (RecipeIngredient ingredient in recipe.Inputs)
                state.IngredientDemand.AddDemand(ingredient.Item, ingredient.Count);

            // A cook working the tavern kitchen moves its renown by (level - 2).
            if (location == Tavern && recipe.Category == RecipeCategory.Cooking)
            {
                state.TavernPopularity.Shift(level - 2);
                if (level >= WasteSavingLevel)
                    state.TavernPopularity.LastSkilledCookDay = state.Clock.Day;
            }

            // Level 2+ wastes less: a seeded 50% chance to spare one unit of a
            // random input back into the cook's own inventory.
            if (level >= WasteSavingLevel && recipe.Inputs.Count > 0 &&
                state.Rng.NextInt(100) < WasteRefundChancePercent &&
                state.Belongings.TryGet(ActorId.ForNpc(cook), out NpcBelongingsEntry holdings))
            {
                RecipeIngredient spared = recipe.Inputs[state.Rng.NextInt(recipe.Inputs.Count)];
                holdings.Inventory.Add(spared.Item, 1);
            }
        }

        /// <summary>
        /// What a cooked dish sells for: baseValue + round((quality - 50) / 25),
        /// never below 1 copper. Average food sells at base value; a master's dish
        /// commands a premium and a burned one barely moves.
        /// </summary>
        public static int QualitySalePrice(int baseValue, int quality)
        {
            if (baseValue < 0) throw new ArgumentOutOfRangeException(nameof(baseValue));
            if (quality < 0 || quality > 100)
                throw new ArgumentOutOfRangeException(nameof(quality), "Quality is 0-100.");
            return Math.Max(1, baseValue + (int)Math.Round((quality - 50) / 25.0));
        }

        /// <summary>
        /// Sells one cooked dish from seller to buyer at its quality-adjusted price:
        /// the dish changes hands, the price in copper moves from the buyer's wallet
        /// to the seller's, and a Purchase truth event is recorded (actor buyer,
        /// targets[0] seller — the P2-03 honest-trade convention, so a fair sale
        /// also warms the relationship). Money is conserved; a failed sale moves
        /// nothing. The seller must hold the dish and the buyer must afford it —
        /// both are caller bugs otherwise.
        /// </summary>
        public static void SellCookedDish(WorldState state, ItemCatalog catalog, NpcId seller,
            NpcId buyer, ItemTypeId item, int quality, LocationId location)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!seller.IsValid) throw new ArgumentException("A seller is required.", nameof(seller));
            if (!buyer.IsValid) throw new ArgumentException("A buyer is required.", nameof(buyer));
            if (seller == buyer) throw new ArgumentException("An actor cannot buy from itself.", nameof(buyer));
            if (!item.IsValid) throw new ArgumentException("A dish needs a valid item type.", nameof(item));
            if (!location.IsValid) throw new ArgumentException("A sale needs a valid location.", nameof(location));
            if (quality < 0 || quality > 100)
                throw new ArgumentOutOfRangeException(nameof(quality), "Quality is 0-100.");

            if (!state.Belongings.TryGet(ActorId.ForNpc(seller), out NpcBelongingsEntry sellerHoldings))
                throw new InvalidOperationException("The seller has no registered belongings.");
            if (!state.Belongings.TryGet(ActorId.ForNpc(buyer), out NpcBelongingsEntry buyerHoldings))
                throw new InvalidOperationException("The buyer has no registered belongings.");
            if (sellerHoldings.Inventory.Count(item) < 1)
                throw new InvalidOperationException(
                    "SellCookedDish called without the dish in the seller's inventory: " + item.Value + ".");

            int price = QualitySalePrice(catalog[item].BaseValue, quality);
            if (!buyerHoldings.Wallet.TryDebit(price))
                throw new InvalidOperationException(
                    "The buyer cannot afford " + price + " copper for " + item.Value + ".");

            // Debit succeeded, so the dish is certainly there; move both halves.
            if (!sellerHoldings.Inventory.TryRemove(item, 1))
                throw new InvalidOperationException("The dish vanished mid-sale: " + item.Value + ".");
            sellerHoldings.Wallet.Credit(price);
            buyerHoldings.Inventory.Add(item, 1);

            state.Events.Append(state.Clock, location, WorldEventType.Purchase,
                ActorId.ForNpc(buyer), new[] { ActorId.ForNpc(seller) },
                EventVisibility.Normal, itemType: item, quantity: 1, copper: price);
        }
    }
}
