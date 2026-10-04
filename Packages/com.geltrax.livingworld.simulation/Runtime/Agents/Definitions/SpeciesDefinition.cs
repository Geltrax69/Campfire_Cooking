using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Immutable species definition loaded from Content/animals/species.json
    /// (docs/design/ANIMALS.md): where the species ranges, what it eats, how it
    /// behaves, and its taming parameters. Wild is false for domestic species;
    /// wild variants (forest-edge boars) are handled by
    /// <see cref="AnimalPopulationFactory"/>.
    /// </summary>
    public sealed class SpeciesDefinition
    {
        public SpeciesDefinition(SpeciesId id, string name, IReadOnlyList<LocationId> habitats,
            IReadOnlyList<string> diet, string temperament, bool wild, bool tameable,
            int bondThreshold, int daysToBond, int populationCount,
            IReadOnlyList<string> tamingBuilds, IReadOnlyList<string> tamingBreaks,
            string tamingRequirement)
        {
            if (!id.IsValid) throw new ArgumentException("A species needs a valid ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A species needs a name.", nameof(name));
            Habitats = RequireIds(habitats, nameof(habitats));
            if (Habitats.Count == 0)
                throw new ArgumentException("A species needs at least one habitat.", nameof(habitats));
            if (diet == null) throw new ArgumentNullException(nameof(diet));
            if (diet.Count == 0)
                throw new ArgumentException("A species needs at least one diet entry.", nameof(diet));
            foreach (string food in diet)
                if (string.IsNullOrWhiteSpace(food))
                    throw new ArgumentException("Diet entries must not be blank.", nameof(diet));
            if (string.IsNullOrWhiteSpace(temperament))
                throw new ArgumentException("A species needs a temperament.", nameof(temperament));
            if (bondThreshold < AnimalState.MinTrust || bondThreshold > AnimalState.MaxTrust)
                throw new ArgumentOutOfRangeException(nameof(bondThreshold),
                    "Bond threshold must be 0-100.");
            if (daysToBond <= 0)
                throw new ArgumentOutOfRangeException(nameof(daysToBond),
                    "Days to bond must be positive.");
            if (populationCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(populationCount),
                    "Population count must be positive.");
            if (tamingBuilds == null) throw new ArgumentNullException(nameof(tamingBuilds));
            if (tamingBreaks == null) throw new ArgumentNullException(nameof(tamingBreaks));

            Id = id;
            Name = name;
            Diet = new List<string>(diet).AsReadOnly();
            Temperament = temperament;
            Wild = wild;
            Tameable = tameable;
            BondThreshold = bondThreshold;
            DaysToBond = daysToBond;
            PopulationCount = populationCount;
            TamingBuilds = new List<string>(tamingBuilds).AsReadOnly();
            TamingBreaks = new List<string>(tamingBreaks).AsReadOnly();
            TamingRequirement = tamingRequirement;
        }

        private static IReadOnlyList<LocationId> RequireIds(IReadOnlyList<LocationId> ids, string name)
        {
            if (ids == null) throw new ArgumentNullException(name);
            var copy = new List<LocationId>(ids.Count);
            foreach (LocationId id in ids)
            {
                if (!id.IsValid) throw new ArgumentException("Habitat IDs must be valid.", name);
                copy.Add(id);
            }
            return copy.AsReadOnly();
        }

        public SpeciesId Id { get; }
        public string Name { get; }
        public IReadOnlyList<LocationId> Habitats { get; }
        public IReadOnlyList<string> Diet { get; }
        public string Temperament { get; }
        /// <summary>True for wild species (start at trust 0); domestic start at 20-30.</summary>
        public bool Wild { get; }
        public bool Tameable { get; }
        /// <summary>Trust 0-100 at which the animal counts as bonded.</summary>
        public int BondThreshold { get; }
        /// <summary>Typical real days to bond with daily interaction.</summary>
        public int DaysToBond { get; }
        /// <summary>Starting wild/domestic population from the design.</summary>
        public int PopulationCount { get; }
        /// <summary>Raw trust-gain rules from content (e.g. "hand_feeding_+8_per_day"); P4-03 parses these.</summary>
        public IReadOnlyList<string> TamingBuilds { get; }
        /// <summary>Raw trust-loss rules from content (e.g. "struck_-25"); P4-03 parses these.</summary>
        public IReadOnlyList<string> TamingBreaks { get; }
        /// <summary>Extra bonding condition (e.g. wolves need "council_approval"); null when none.</summary>
        public string TamingRequirement { get; }
    }
}
