using System;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// The stored town-stats section of WorldState (P5-01). Holds the last monthly
    /// computation; TownStatsSystem refreshes it on month boundaries. The Persistence
    /// agent saves and restores this section so stats survive save/load.
    /// </summary>
    public sealed class TownStatsState
    {
        public TownStatsState()
        {
            ComputedMonth = -1;
        }

        public TownStatsState(long computedMonth, TownStats values)
        {
            if (computedMonth < 0) throw new ArgumentOutOfRangeException(nameof(computedMonth));
            ComputedMonth = computedMonth;
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public bool IsComputed => Values != null;

        /// <summary>Absolute month index ((day - 1) / 30) the values were computed for; -1 until computed.</summary>
        public long ComputedMonth { get; private set; }

        /// <summary>The last computed stats; null until the first computation.</summary>
        public TownStats Values { get; private set; }

        internal void Store(long computedMonth, TownStats values)
        {
            if (computedMonth < 0) throw new ArgumentOutOfRangeException(nameof(computedMonth));
            ComputedMonth = computedMonth;
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }
    }
}
