using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Loads and validates animal species definitions from the approved JSON schema.</summary>
    public static class SpeciesContentLoader
    {
        private const int SupportedVersion = 1;

        /// <summary>The five prototype species from docs/design/ANIMALS.md; all must be present.</summary>
        public static readonly SpeciesId Chicken = new SpeciesId("species_chicken");
        public static readonly SpeciesId Pig = new SpeciesId("species_pig");
        public static readonly SpeciesId Deer = new SpeciesId("species_deer");
        public static readonly SpeciesId Wolf = new SpeciesId("species_wolf");
        public static readonly SpeciesId Brambleback = new SpeciesId("species_brambleback");

        public static IReadOnlyList<SpeciesDefinition> Load(Stream stream, LocationMap locations)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (locations == null) throw new ArgumentNullException(nameof(locations));

            var serializer = new DataContractJsonSerializer(typeof(SpeciesDocumentDto),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            var document = (SpeciesDocumentDto)serializer.ReadObject(stream);
            if (document == null) throw new SerializationException("Species document must not be null.");
            if (!document.Version.HasValue || document.Version.Value != SupportedVersion)
                throw new SerializationException("Unsupported or missing species content version.");
            if (document.Species == null) throw new SerializationException("Species rows are required.");

            var definitions = new SortedList<SpeciesId, SpeciesDefinition>();
            foreach (var row in document.Species)
            {
                if (row == null) throw new SerializationException("Species rows must not contain null.");
                var id = new SpeciesId(RequiredText(row.Id, "id"));
                var habitats = LoadHabitats(row, locations);
                if (row.Diet == null || row.Diet.Length == 0)
                    throw new SerializationException("Species diet is required.");
                foreach (string food in row.Diet)
                    if (string.IsNullOrWhiteSpace(food))
                        throw new SerializationException("Species diet entries must not be blank.");
                if (!row.Wild.HasValue) throw new SerializationException("Species wild flag is required.");
                if (row.Taming == null) throw new SerializationException("Species taming is required.");
                if (!row.Taming.Tameable.HasValue)
                    throw new SerializationException("Species taming.tameable is required.");
                if (!row.Taming.BondThreshold.HasValue)
                    throw new SerializationException("Species taming.bondThreshold is required.");
                if (!row.Taming.DaysToBond.HasValue)
                    throw new SerializationException("Species taming.daysToBond is required.");
                if (row.Population == null || !row.Population.Count.HasValue)
                    throw new SerializationException("Species population.count is required.");

                var definition = new SpeciesDefinition(id, RequiredText(row.Name, "name"), habitats,
                    new List<string>(row.Diet).AsReadOnly(), RequiredText(row.Temperament, "temperament"),
                    row.Wild.Value, row.Taming.Tameable.Value, row.Taming.BondThreshold.Value,
                    row.Taming.DaysToBond.Value, row.Population.Count.Value,
                    ToReadOnly(row.Taming.Builds), ToReadOnly(row.Taming.Breaks), row.Taming.Requires);
                if (definitions.ContainsKey(id))
                    throw new SerializationException("Species IDs must be unique.");
                definitions.Add(id, definition);
            }

            Require(definitions, Chicken, "chicken");
            Require(definitions, Pig, "pig");
            Require(definitions, Deer, "deer");
            Require(definitions, Wolf, "wolf");
            Require(definitions, Brambleback, "brambleback");

            return new ReadOnlyCollection<SpeciesDefinition>(definitions.Values);
        }

        private static void Require(SortedList<SpeciesId, SpeciesDefinition> definitions,
            SpeciesId id, string name)
        {
            if (!definitions.ContainsKey(id))
                throw new SerializationException("The design's " + name + " species is missing.");
        }

        private static string RequiredText(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new SerializationException("Species " + field + " is required.");
            return value;
        }

        private static IReadOnlyList<string> ToReadOnly(string[] values) =>
            values == null ? (IReadOnlyList<string>)Array.Empty<string>()
                : (IReadOnlyList<string>)new List<string>(values).AsReadOnly();

        private static IReadOnlyList<LocationId> LoadHabitats(SpeciesDto row, LocationMap locations)
        {
            if (row.Habitat == null || row.Habitat.Length == 0)
                throw new SerializationException("Species habitat is required.");
            var habitats = new List<LocationId>(row.Habitat.Length);
            foreach (string habitat in row.Habitat)
            {
                var id = new LocationId(RequiredText(habitat, "habitat"));
                try { locations.GetTravelMinutes(id, id); }
                catch (ArgumentException exception)
                {
                    throw new SerializationException(
                        "Species habitat '" + id + "' references an unknown location.", exception);
                }
                habitats.Add(id);
            }
            return habitats.AsReadOnly();
        }

        [DataContract]
        private sealed class SpeciesDocumentDto
        {
            [DataMember(Name = "version", IsRequired = true)] public int? Version { get; set; }
            [DataMember(Name = "species", IsRequired = true)] public SpeciesDto[] Species { get; set; }
        }

        [DataContract]
        private sealed class SpeciesDto
        {
            [DataMember(Name = "id", IsRequired = true)] public string Id { get; set; }
            [DataMember(Name = "name", IsRequired = true)] public string Name { get; set; }
            [DataMember(Name = "habitat", IsRequired = true)] public string[] Habitat { get; set; }
            [DataMember(Name = "diet", IsRequired = true)] public string[] Diet { get; set; }
            [DataMember(Name = "temperament", IsRequired = true)] public string Temperament { get; set; }
            [DataMember(Name = "wild", IsRequired = true)] public bool? Wild { get; set; }
            [DataMember(Name = "taming", IsRequired = true)] public TamingDto Taming { get; set; }
            [DataMember(Name = "population", IsRequired = true)] public PopulationDto Population { get; set; }
        }

        [DataContract]
        private sealed class TamingDto
        {
            [DataMember(Name = "tameable", IsRequired = true)] public bool? Tameable { get; set; }
            [DataMember(Name = "bondThreshold", IsRequired = true)] public int? BondThreshold { get; set; }
            [DataMember(Name = "daysToBond", IsRequired = true)] public int? DaysToBond { get; set; }
            [DataMember(Name = "builds")] public string[] Builds { get; set; }
            [DataMember(Name = "breaks")] public string[] Breaks { get; set; }
            [DataMember(Name = "requires")] public string Requires { get; set; }
        }

        [DataContract]
        private sealed class PopulationDto
        {
            [DataMember(Name = "count", IsRequired = true)] public int? Count { get; set; }
        }
    }
}
