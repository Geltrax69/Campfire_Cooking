using System;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Immutable caller tuning for inter-village trade (P6-02): how often a
    /// merchant departs, how much cargo one merchant carries, and what the road
    /// costs. A merchant departs on day 1 and then every
    /// <see cref="DepartureIntervalDays"/> days; the route is drawn from the
    /// world's seeded RNG, so busy roads stay deterministic.
    /// </summary>
    public sealed class TradeRoutePolicy
    {
        public TradeRoutePolicy(int departureIntervalDays, int maxUnitsPerGood, int travelCostCopper)
        {
            if (departureIntervalDays < 1)
                throw new ArgumentOutOfRangeException(nameof(departureIntervalDays), "Merchants must depart at least every day.");
            if (maxUnitsPerGood < 1)
                throw new ArgumentOutOfRangeException(nameof(maxUnitsPerGood), "A merchant must carry at least one unit per good.");
            if (travelCostCopper < 0)
                throw new ArgumentOutOfRangeException(nameof(travelCostCopper), "Travel cost cannot be negative.");
            DepartureIntervalDays = departureIntervalDays;
            MaxUnitsPerGood = maxUnitsPerGood;
            TravelCostCopper = travelCostCopper;
        }

        /// <summary>Days between merchant departures (first departure on day 1).</summary>
        public int DepartureIntervalDays { get; }

        /// <summary>Maximum units of each good one merchant loads (1..this, drawn).</summary>
        public int MaxUnitsPerGood { get; }

        /// <summary>Flat copper cost of one journey, subtracted from merchant profit.</summary>
        public int TravelCostCopper { get; }
    }
}
