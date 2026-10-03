using System;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Hungry NPCs eat from their own inventories at mealtimes (07:00, 12:00, 19:00).
    /// Each meal, an NPC whose hunger is at least <see cref="MealHungerThreshold"/> eats
    /// up to <see cref="MaxItemsPerMeal"/> foods, always the most filling available first.
    /// Stale food applies its approved stale effect via <see cref="Consumption"/>. Eating
    /// never increases hunger: two Content foods carry positive hunger values (a data
    /// quirk — roasted fish and porridge), and those are treated as non-filling rather
    /// than hunger-inducing. Runs in the Actions phase, after Decisions.
    /// </summary>
    public sealed class EatSystem : IWorldSystem
    {
        /// <summary>Below this hunger an NPC skips the meal: they are not hungry enough.</summary>
        public const int MealHungerThreshold = 15;

        /// <summary>An NPC stops eating once hunger drops below this within a meal.</summary>
        public const int SatiatedHungerThreshold = 15;

        /// <summary>Even the hungriest NPC eats at most this many items per meal.</summary>
        public const int MaxItemsPerMeal = 3;

        private static readonly int[] MealHours = { 7, 12, 19 };
        private const string FoodCategory = "food";

        private readonly ItemCatalog _catalog;

        public EatSystem(ItemCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public string Id => "agents.eat";
        public SimulationPhase Phase => SimulationPhase.Actions;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Clock.Minute != 0) return;
            bool isMealHour = false;
            foreach (int hour in MealHours)
                if (state.Clock.Hour == hour) { isMealHour = true; break; }
            if (!isMealHour) return;
            foreach (NpcState npc in state.Npcs.Npcs)
                TryEatMeal(state, npc);
        }

        private void TryEatMeal(WorldState state, NpcState npc)
        {
            if (npc.Needs.Hunger < MealHungerThreshold) return;
            if (!state.Belongings.TryGet(ActorId.ForNpc(npc.Definition.Id), out NpcBelongingsEntry belongings))
                return;
            Inventory pantry = belongings.Inventory;
            int eaten = 0;
            while (eaten < MaxItemsPerMeal && npc.Needs.Hunger >= SatiatedHungerThreshold)
            {
                ItemTypeId? best = BestFood(pantry);
                if (!best.HasValue) break;
                // Oldest lot leaves first: query its freshness before removing it.
                Freshness freshness = pantry.FreshnessOfOldestLot(best.Value);
                if (!pantry.TryRemove(best.Value, 1)) break;
                Consumption.EffectiveEffects(_catalog[best.Value], freshness,
                    out int? hungerEffect, out _);
                int restore = Math.Max(0, -(hungerEffect ?? 0));
                if (restore > 0) npc.Needs.ReduceHunger(restore);
                eaten++;
            }
        }

        private ItemTypeId? BestFood(Inventory pantry)
        {
            // The most filling food is the one with the most negative hunger effect.
            // Foods with positive hunger values (a Content data quirk) are still food:
            // a hungry NPC eats them, but eating never increases hunger (clamped in
            // TryEatMeal).
            ItemTypeId? best = null;
            int bestHungerEffect = int.MaxValue;
            foreach (var row in pantry.Contents)
            {
                if (row.Value <= 0) continue;
                ItemDefinition definition = _catalog[row.Key];
                if (definition.Category != FoodCategory) continue;
                int hungerEffect = definition.HungerEffect ?? 0;
                if (hungerEffect < bestHungerEffect)
                {
                    bestHungerEffect = hungerEffect;
                    best = row.Key;
                }
            }
            return best;
        }
    }
}
