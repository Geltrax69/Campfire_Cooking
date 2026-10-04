using System;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Ticks abstract (LOD_ABSTRACT) villages once per day at low fidelity (P6-01).
    /// Full-LOD Millbrook is never touched: its stats come from the live simulation.
    ///
    /// Daily drift per abstract village, all from the world's seeded RNG in a fixed
    /// draw order (population, wealth, food), in ordinal VillageId order:
    /// - Population: -1/0/+1 (a quiet random walk; clamps at zero — an empty
    ///   village is abandoned, not negative).
    /// - Wealth: -10..+10 copper (placeholder until P6-02 wires real trade flows).
    /// - FoodSupply: seasonal — winter always removes 1-2 (hard winter), autumn
    ///   adds 0-2 (harvest surplus), spring/summer drift -1..+1.
    /// - Mood: moves 1/day toward a food-dependent target — 50 when food is
    ///   adequate, 40 when food supply is below 25 (hungry villages are glum).
    ///
    /// The drift runs once per absolute day (guarded by the registry's drift-day
    /// cursor, which is part of the save/load snapshot so a day is never drifted
    /// twice across save/load — the determinism invariant).
    /// </summary>
    public sealed class VillageDriftSystem : IWorldSystem
    {
        /// <summary>Food supply below this counts as hungry for the mood target.</summary>
        public const int HungryFoodSupplyBelow = 25;

        /// <summary>Mood target when food is adequate.</summary>
        public const int ContentMoodTarget = 50;

        /// <summary>Mood target when the village is hungry.</summary>
        public const int HungryMoodTarget = 40;

        /// <summary>Wealth drift bounds in copper per day (placeholder for P6-02 trade).</summary>
        public const int WealthDriftCopper = 10;

        public string Id => "agents.village-drift";
        public SimulationPhase Phase => SimulationPhase.Memory;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            long day = state.Clock.Day;
            if (state.Villages.LastDriftDay >= day) return;

            state.Villages.RecordDriftDay(day);
            Season season = VillageCalendar.SeasonAt(state.Clock);
            foreach (AbstractVillageState village in state.Villages.GetByLod(VillageLod.Abstract))
                DriftOne(village, state.Rng, season);
        }

        private static void DriftOne(AbstractVillageState village, SimRng rng, Season season)
        {
            // Fixed draw order per village: population, wealth, food. Mood draws nothing.
            village.AdjustPopulation(rng.NextInt(3) - 1);
            village.AdjustWealthCopper(rng.NextInt(2 * WealthDriftCopper + 1) - WealthDriftCopper);

            int foodDelta;
            switch (season)
            {
                case Season.Winter:
                    foodDelta = -(1 + rng.NextInt(2)); // -2..-1: the hard winter always bites.
                    break;
                case Season.Autumn:
                    foodDelta = rng.NextInt(3); // 0..+2: harvest surplus.
                    break;
                default:
                    foodDelta = rng.NextInt(3) - 1; // -1..+1: spring and summer.
                    break;
            }
            village.SetFoodSupply(village.FoodSupply + foodDelta);

            int target = village.FoodSupply < HungryFoodSupplyBelow ? HungryMoodTarget : ContentMoodTarget;
            if (village.Mood < target) village.SetMood(village.Mood + 1);
            else if (village.Mood > target) village.SetMood(village.Mood - 1);
        }
    }
}
