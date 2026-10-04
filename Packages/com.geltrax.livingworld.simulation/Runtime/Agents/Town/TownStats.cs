using System;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// The 11 computed town stats (P5-01, TOWN.md section 1). Immutable: every value is
    /// derived from world truth by TownStatsCalculator, never set by hand or scripted.
    /// Scales are 0-100 unless noted; Population, WealthCopper and Crime are counts.
    /// </summary>
    public sealed class TownStats
    {
        public TownStats(int population, int wealthCopper, int foodSupply, int safety,
            int housing, int employment, int trade, int happiness, int crime,
            int infrastructure, int reputation)
        {
            if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
            if (wealthCopper < 0) throw new ArgumentOutOfRangeException(nameof(wealthCopper));
            if (crime < 0) throw new ArgumentOutOfRangeException(nameof(crime));
            Population = population;
            WealthCopper = wealthCopper;
            FoodSupply = ValidateScale(foodSupply, nameof(foodSupply));
            Safety = ValidateScale(safety, nameof(safety));
            Housing = ValidateScale(housing, nameof(housing));
            Employment = ValidateScale(employment, nameof(employment));
            Trade = ValidateScale(trade, nameof(trade));
            Happiness = ValidateScale(happiness, nameof(happiness));
            Crime = crime;
            Infrastructure = ValidateScale(infrastructure, nameof(infrastructure));
            Reputation = ValidateScale(reputation, nameof(reputation));
        }

        /// <summary>Living simulated NPCs + 100 background villagers (count).</summary>
        public int Population { get; }
        /// <summary>NPC money + village fund + granary at base price + shop stocks at base price (copper).</summary>
        public int WealthCopper { get; }
        /// <summary>Days of village food in storage / 180 x 100, capped at 100.</summary>
        public int FoodSupply { get; }
        /// <summary>100 - wolf incidents x 15 - unresolved crimes x 5 + night watch + palisade.</summary>
        public int Safety { get; }
        /// <summary>Sound roofs / households needing them x 100.</summary>
        public int Housing { get; }
        /// <summary>Working-age NPCs with productive work / working-age NPCs x 100.</summary>
        public int Employment { get; }
        /// <summary>This month's traveler + merchant copper / 1700 x 100.</summary>
        public int Trade { get; }
        /// <summary>Average over NPCs of social, food security, festival and fear components.</summary>
        public int Happiness { get; }
        /// <summary>Theft/burglary/vandalism events in the last 30 days (count).</summary>
        public int Crime { get; }
        /// <summary>Wheel x 0.4 + palisade x 0.25 + wells & roads x 0.2 + granary building x 0.15.</summary>
        public int Infrastructure { get; }
        /// <summary>(Apple fame + road safety + hospitality) / 3: how outsiders see Millbrook.</summary>
        public int Reputation { get; }

        private static int ValidateScale(int value, string name)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(name, "Town stats are 0-100.");
            return value;
        }
    }
}
