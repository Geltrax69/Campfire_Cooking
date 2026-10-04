using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// One village's level-of-detail state (P6-01). Millbrook registers with
    /// Lod = Full; its abstract stat fields are placeholders (zeros) that the
    /// drift system never touches — Millbrook's real stats come from the full
    /// simulation (town stats, NPCs, shops). Abstract neighbors carry live values
    /// that VillageDriftSystem nudges once per day.
    /// </summary>
    public sealed class AbstractVillageState
    {
        public const int MinStat = 0;
        public const int MaxPercentStat = 100;

        public AbstractVillageState(VillageId id, string name, VillageLod lod,
            LocationId anchorLocation, int travelDaysFromMillbrook,
            int population, int wealthCopper, int foodSupply, int mood)
        {
            if (!id.IsValid) throw new ArgumentException("A village needs a valid ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A village needs a name.", nameof(name));
            if (travelDaysFromMillbrook < 0)
                throw new ArgumentOutOfRangeException(nameof(travelDaysFromMillbrook));
            if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
            if (wealthCopper < 0) throw new ArgumentOutOfRangeException(nameof(wealthCopper));
            if (foodSupply < MinStat || foodSupply > MaxPercentStat)
                throw new ArgumentOutOfRangeException(nameof(foodSupply));
            if (mood < MinStat || mood > MaxPercentStat)
                throw new ArgumentOutOfRangeException(nameof(mood));

            Id = id;
            Name = name;
            Lod = lod;
            AnchorLocation = anchorLocation;
            TravelDaysFromMillbrook = travelDaysFromMillbrook;
            Population = population;
            WealthCopper = wealthCopper;
            FoodSupply = foodSupply;
            Mood = mood;
        }

        public VillageId Id { get; }
        public string Name { get; }
        public VillageLod Lod { get; }

        /// <summary>
        /// Nearest Millbrook location for the road outward (flavor/direction).
        /// May be invalid for distant villages with no Millbrook-map anchor.
        /// </summary>
        public LocationId AnchorLocation { get; }

        /// <summary>Whole travel days between Millbrook and this village.</summary>
        public int TravelDaysFromMillbrook { get; }

        /// <summary>Head count; never negative.</summary>
        public int Population { get; private set; }

        /// <summary>Copper held village-wide; never negative.</summary>
        public int WealthCopper { get; private set; }

        /// <summary>0-100 abstract food security.</summary>
        public int FoodSupply { get; private set; }

        /// <summary>0-100 abstract contentment, like Millbrook's happiness stat.</summary>
        public int Mood { get; private set; }

        /// <summary>Moves population by a delta; clamps at zero (an empty village is abandoned, not negative).</summary>
        public void AdjustPopulation(int delta)
        {
            Population = Math.Max(0, Population + delta);
        }

        /// <summary>Moves wealth by a delta in copper; clamps at zero.</summary>
        public void AdjustWealthCopper(int delta)
        {
            WealthCopper = Math.Max(0, WealthCopper + delta);
        }

        /// <summary>Sets food supply; clamps to 0-100.</summary>
        public void SetFoodSupply(int value)
        {
            FoodSupply = Math.Max(MinStat, Math.Min(MaxPercentStat, value));
        }

        /// <summary>Sets mood; clamps to 0-100.</summary>
        public void SetMood(int value)
        {
            Mood = Math.Max(MinStat, Math.Min(MaxPercentStat, value));
        }
    }
}
