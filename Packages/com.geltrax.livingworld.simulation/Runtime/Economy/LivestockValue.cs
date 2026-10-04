using System;
using LivingWorld.Simulation.Agents;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Copper value of a live animal, used to record the economic loss when the
    /// winter pack takes livestock (WinterPressureSystem) and when a villager's
    /// animal dies. Values are grounded in item base values from
    /// Content/items/items.json: a laying hen produces ~150 eggs a year at
    /// 1 copper each, so 25 copper is roughly two months of lay; a grown pig
    /// slaughters to about ten pork cuts at 12 copper each, hence 120.
    /// (Flagged for the human to ratify: the design never priced live animals.)
    /// </summary>
    public static class LivestockValue
    {
        /// <summary>A laying hen, in copper.</summary>
        public const int ChickenCopper = 25;
        /// <summary>A grown pig, in copper.</summary>
        public const int PigCopper = 120;

        /// <summary>Returns the copper value of one animal of the given species.</summary>
        public static int ForSpecies(SpeciesId species)
        {
            if (species == new SpeciesId("species_chicken")) return ChickenCopper;
            if (species == new SpeciesId("species_pig")) return PigCopper;
            throw new ArgumentException("No livestock value for species '" + species + "'.",
                nameof(species));
        }
    }
}
