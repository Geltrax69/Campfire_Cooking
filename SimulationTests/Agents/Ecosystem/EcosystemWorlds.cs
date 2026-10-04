using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Tests.Agents.Ecosystem
{
    /// <summary>
    /// Shared builders for the P4-02 ecosystem tests: species/location IDs, animal
    /// construction, and day-granular ticking (the ecosystem systems catch up one
    /// day at a time, so advancing the clock a day and ticking once is exact).
    /// </summary>
    internal static class EcosystemWorlds
    {
        public static readonly SpeciesId Chicken = new SpeciesId("species_chicken");
        public static readonly SpeciesId Pig = new SpeciesId("species_pig");
        public static readonly SpeciesId Deer = new SpeciesId("species_deer");
        public static readonly SpeciesId Wolf = new SpeciesId("species_wolf");
        public static readonly SpeciesId Brambleback = new SpeciesId("species_brambleback");

        public static readonly LocationId Farm = new LocationId("loc_farm");
        public static readonly LocationId ForestEdge = new LocationId("loc_forest_edge");
        public static readonly LocationId FennHome = new LocationId("loc_home_fenn");

        public const long FirstWinterDay = 91;
        public const long FirstSpringDay = 181;
        public const long FirstSummerDay = 271;

        public static WorldState CreateWorld(ulong seed, long startDay) =>
            new WorldState(seed, new GameTime((startDay - 1) * 1440));

        public static AnimalState MakeAnimal(string id, SpeciesId species, LocationId location,
            AnimalAge age = AnimalAge.Adult, int trust = 0) =>
            AnimalState.Create(new AnimalId(id), species, location, trust, null, age,
                AnimalState.MaxHealth);

        /// <summary>Adds a wolf pack of the given size at the forest edge.</summary>
        public static void AddWolves(WorldState state, int count)
        {
            for (int i = 1; i <= count; i++)
                state.Animals.Add(MakeAnimal("animal_wolf_" + i.ToString("D3"), Wolf, ForestEdge));
        }

        /// <summary>Adds deer at the forest edge, all healthy adults.</summary>
        public static void AddDeer(WorldState state, int count)
        {
            for (int i = 1; i <= count; i++)
                state.Animals.Add(MakeAnimal("animal_deer_" + i.ToString("D3"), Deer, ForestEdge));
        }

        /// <summary>Adds the design's starting chickens: 14 at the farm, 6 at Fenn's.</summary>
        public static void AddChickens(WorldState state)
        {
            for (int i = 1; i <= 14; i++)
                state.Animals.Add(MakeAnimal("animal_chicken_" + i.ToString("D3"), Chicken, Farm,
                    AnimalAge.Adult, trust: 25));
            for (int i = 15; i <= 20; i++)
                state.Animals.Add(MakeAnimal("animal_chicken_" + i.ToString("D3"), Chicken, FennHome,
                    AnimalAge.Adult, trust: 25));
        }

        /// <summary>Adds the design's starting pigs: 2 sows + 4 piglets at the farm, 6 wild boars.</summary>
        public static void AddPigs(WorldState state)
        {
            state.Animals.Add(MakeAnimal("animal_pig_001", Pig, Farm, AnimalAge.Adult, trust: 25));
            state.Animals.Add(MakeAnimal("animal_pig_002", Pig, Farm, AnimalAge.Adult, trust: 25));
            for (int i = 3; i <= 6; i++)
                state.Animals.Add(MakeAnimal("animal_pig_" + i.ToString("D3"), Pig, Farm,
                    AnimalAge.Young, trust: 25));
            for (int i = 7; i <= 12; i++)
                state.Animals.Add(MakeAnimal("animal_pig_" + i.ToString("D3"), Pig, ForestEdge));
        }

        /// <summary>Adds the design's starting bramblebacks.</summary>
        public static void AddBramblebacks(WorldState state)
        {
            for (int i = 1; i <= 25; i++)
                state.Animals.Add(MakeAnimal("animal_brambleback_" + i.ToString("D3"),
                    Brambleback, i <= 17 ? ForestEdge : Farm));
        }

        /// <summary>Advances the clock one full day per tick so daily systems run once each.</summary>
        public static void TickDays(World world, int days)
        {
            for (int day = 0; day < days; day++)
            {
                world.State.Clock = world.State.Clock.Advance(1440);
                world.Tick();
            }
        }

        public static int CountEvents(WorldState state, WorldEventType type) =>
            state.Events.Query(type: type).Count;
    }
}
