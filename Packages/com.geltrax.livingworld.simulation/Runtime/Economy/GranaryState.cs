using System;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// The village's communal grain store, in whole sacks (P5-01, TOWN.md). This is the
    /// "granary" half of the food-supply stat; the other half is household cellars.
    /// Harvests add sacks here and the mill draws from here (later phases).
    /// </summary>
    public sealed class GranaryState
    {
        public GranaryState(int sacks = 0)
        {
            if (sacks < 0) throw new ArgumentOutOfRangeException(nameof(sacks));
            Sacks = sacks;
        }

        public int Sacks { get; private set; }

        public void AddSacks(int sacks)
        {
            if (sacks < 1) throw new ArgumentOutOfRangeException(nameof(sacks));
            Sacks = checked(Sacks + sacks);
        }

        /// <summary>Removes sacks when enough are stored; returns false without changing anything otherwise.</summary>
        public bool TryRemoveSacks(int sacks)
        {
            if (sacks < 1) throw new ArgumentOutOfRangeException(nameof(sacks));
            if (Sacks < sacks) return false;
            Sacks -= sacks;
            return true;
        }
    }
}
