using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Builds the prototype's starting animal populations from the species
    /// definitions (docs/design/ANIMALS.md, Content/animals/species.json):
    /// 20 chickens (14 at Alder Farm, 6 across other households), 12 pigs
    /// (2 sows + 4 piglets domestic at the farm, 6 wild boars at the forest
    /// edge — the species' untamable cousins), 40 deer, a 6-wolf pack and
    /// 25 bramblebacks (17 forest edge, 8 farm margins).
    ///
    /// Fully deterministic: IDs are ordinal ("animal_chicken_001"), wild animals
    /// start at trust 0 and domestic ones at 20-30 by ordinal. No RNG.
    /// </summary>
    public static class AnimalPopulationFactory
    {
        /// <summary>Domestic animals start here (wild animals start at 0).</summary>
        public const int DomesticTrustMin = 20;
        /// <summary>Domestic animals start here (wild animals start at 0).</summary>
        public const int DomesticTrustMax = 30;

        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly LocationId ForestEdge = new LocationId("loc_forest_edge");
        private static readonly LocationId FennHome = new LocationId("loc_home_fenn");

        private sealed class GroupPlan
        {
            public GroupPlan(LocationId location, int count, bool wild, AnimalAge age)
            {
                Location = location;
                Count = count;
                Wild = wild;
                Age = age;
            }
            public LocationId Location { get; }
            public int Count { get; }
            public bool Wild { get; }
            public AnimalAge Age { get; }
        }

        public static IReadOnlyList<AnimalState> CreateInitialPopulations(
            IReadOnlyList<SpeciesDefinition> species)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            var byId = new Dictionary<SpeciesId, SpeciesDefinition>();
            foreach (SpeciesDefinition definition in species)
            {
                if (definition == null)
                    throw new ArgumentException("Species definitions must not contain null.", nameof(species));
                byId[definition.Id] = definition;
            }

            var animals = new List<AnimalState>();
            AddSpecies(animals, Require(byId, SpeciesContentLoader.Chicken), "chicken", new[]
            {
                new GroupPlan(Farm, 14, wild: false, AnimalAge.Adult),
                new GroupPlan(FennHome, 6, wild: false, AnimalAge.Adult),
            });
            AddSpecies(animals, Require(byId, SpeciesContentLoader.Pig), "pig", new[]
            {
                new GroupPlan(Farm, 2, wild: false, AnimalAge.Adult),   // the 2 sows
                new GroupPlan(Farm, 4, wild: false, AnimalAge.Young),   // spring piglets
                new GroupPlan(ForestEdge, 6, wild: true, AnimalAge.Adult), // wild boars, never tameable
            });
            AddSpecies(animals, Require(byId, SpeciesContentLoader.Deer), "deer", new[]
            {
                new GroupPlan(ForestEdge, 40, wild: true, AnimalAge.Adult),
            });
            AddSpecies(animals, Require(byId, SpeciesContentLoader.Wolf), "wolf", new[]
            {
                new GroupPlan(ForestEdge, 6, wild: true, AnimalAge.Adult),
            });
            AddSpecies(animals, Require(byId, SpeciesContentLoader.Brambleback), "brambleback", new[]
            {
                new GroupPlan(ForestEdge, 17, wild: true, AnimalAge.Adult),
                new GroupPlan(Farm, 8, wild: true, AnimalAge.Adult),
            });
            return new ReadOnlyCollection<AnimalState>(animals);
        }

        private static SpeciesDefinition Require(Dictionary<SpeciesId, SpeciesDefinition> byId,
            SpeciesId id)
        {
            if (!byId.TryGetValue(id, out SpeciesDefinition definition))
                throw new ArgumentException(
                    "Initial populations need the '" + id + "' species.", nameof(byId));
            return definition;
        }

        private static void AddSpecies(List<AnimalState> animals, SpeciesDefinition definition,
            string idPrefix, GroupPlan[] plans)
        {
            int ordinal = 0;
            foreach (GroupPlan plan in plans)
            {
                if (!definition.Habitats.Contains(plan.Location))
                    throw new ArgumentException("Population plan for '" + definition.Id
                        + "' uses '" + plan.Location + "', which the species does not list as a habitat.");
                for (int i = 0; i < plan.Count; i++)
                {
                    ordinal++;
                    var id = new AnimalId("animal_" + idPrefix + "_" + ordinal.ToString("D3"));
                    // Wild animals start at trust 0; domestic ones spread 20-30 by ordinal —
                    // deterministic, no RNG.
                    int trust = plan.Wild ? AnimalState.MinTrust
                        : DomesticTrustMin + (ordinal % (DomesticTrustMax - DomesticTrustMin + 1));
                    animals.Add(AnimalState.Create(id, definition.Id, plan.Location, trust,
                        null, plan.Age, AnimalState.MaxHealth));
                }
            }
            if (ordinal != definition.PopulationCount)
                throw new ArgumentException("Population plan for '" + definition.Id + "' creates "
                    + ordinal + " animals, but the content declares " + definition.PopulationCount + ".");
        }
    }
}
