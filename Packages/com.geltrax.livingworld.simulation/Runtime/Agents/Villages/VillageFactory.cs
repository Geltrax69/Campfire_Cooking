using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Builds the starting village registry (P6-01). Millbrook is the only
    /// Full-LOD village; its abstract stat fields are placeholders the drift
    /// system ignores. King's Rest is the Wesmark capital (WORLD.md: the kingdom
    /// is small, ruled from the capital); Oakhollow is a nearby hamlet invented
    /// for the prototype's trade-road geometry.
    /// </summary>
    public static class VillageFactory
    {
        public static readonly VillageId Millbrook = new VillageId("village_millbrook");

        private static readonly LocationId Square = new LocationId("loc_square");

        /// <summary>Millbrook (Full) plus two abstract neighbors, ready to register.</summary>
        public static VillageRegistry CreateInitialVillages()
        {
            var registry = new VillageRegistry();
            // Millbrook's abstract stats are placeholders (zeros): the full
            // simulation owns its population, wealth, food and happiness.
            registry.Register(new AbstractVillageState(
                Millbrook, "Millbrook", VillageLod.Full, Square,
                travelDaysFromMillbrook: 0,
                population: 0, wealthCopper: 0, foodSupply: 0, mood: 0));
            registry.Register(new AbstractVillageState(
                new VillageId("village_kings_rest"), "King's Rest", VillageLod.Abstract, Square,
                travelDaysFromMillbrook: 2,
                population: 5000, wealthCopper: 200000, foodSupply: 60, mood: 55));
            registry.Register(new AbstractVillageState(
                new VillageId("village_oakhollow"), "Oakhollow", VillageLod.Abstract, Square,
                travelDaysFromMillbrook: 1,
                population: 60, wealthCopper: 8000, foodSupply: 45, mood: 50));
            return registry;
        }
    }
}
