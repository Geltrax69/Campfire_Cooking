using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Farm households grow much of their own food (ECONOMY.md §5): once a day at
    /// noon, NPCs whose home is the farm eat a farm meal, reducing hunger directly.
    /// The farm kitchen is not a shop and the meal is not a purchase — it is the
    /// household feeding itself from its own land, which is why farm families'
    /// cash outgoings run lower. Runs in the Actions phase, after the eat system.
    /// </summary>
    public sealed class FarmMealSystem : IWorldSystem
    {
        /// <summary>Hunger points restored by the daily farm meal (a hearty lunch).</summary>
        public const int FarmMealHungerRestore = 50;

        private static readonly LocationId FarmHome = new LocationId("loc_farm");
        private const int MealHour = 12;

        public string Id => "agents.farm-meal";
        public SimulationPhase Phase => SimulationPhase.Actions;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Clock.Hour != MealHour || state.Clock.Minute != 0) return;
            foreach (NpcState npc in state.Npcs.Npcs)
            {
                if (npc.Definition.Home != FarmHome) continue;
                npc.Needs.ReduceHunger(FarmMealHungerRestore);
            }
        }
    }
}
