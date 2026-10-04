using System;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Recomputes the 11 town stats once per calendar month (P5-01). Ticks are cheap:
    /// the system does nothing until the absolute 30-day month index changes, then
    /// stores one fresh computation. Runs in the Memory phase so it summarizes the
    /// month after every other system has acted.
    /// </summary>
    public sealed class TownStatsSystem : IWorldSystem
    {
        private readonly ItemCatalog _catalog;

        public TownStatsSystem(ItemCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public string Id => "agents.town_stats";
        public SimulationPhase Phase => SimulationPhase.Memory;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            long month = (state.Clock.Day - 1) / VillageCalendar.DaysPerMonth;
            TownStatsState stored = state.TownStats;
            if (stored.IsComputed && stored.ComputedMonth == month) return;
            stored.Store(month, TownStatsCalculator.Compute(state, _catalog));
        }
    }
}
