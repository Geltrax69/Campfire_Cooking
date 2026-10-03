using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Loads and validates NPC definitions from the approved JSON schema.</summary>
    public static class NpcContentLoader
    {
        private const int SupportedVersion = 1;

        public static IReadOnlyList<NpcDefinition> Load(Stream stream, LocationMap locations)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (locations == null) throw new ArgumentNullException(nameof(locations));

            var serializer = new DataContractJsonSerializer(typeof(NpcDocumentDto),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            var document = (NpcDocumentDto)serializer.ReadObject(stream);
            if (document == null) throw new SerializationException("NPC document must not be null.");
            if (!document.Version.HasValue || document.Version.Value != SupportedVersion)
                throw new SerializationException("Unsupported or missing NPC content version.");
            if (document.Npcs == null) throw new SerializationException("NPC rows are required.");

            var definitions = new SortedList<NpcId, NpcDefinition>();
            foreach (var row in document.Npcs)
            {
                if (row == null) throw new SerializationException("NPC rows must not contain null.");
                var id = new NpcId(RequiredText(row.Id, "id"));
                var home = new LocationId(RequiredText(row.Home, "home"));
                var workplace = new LocationId(RequiredText(row.Workplace, "workplace"));
                ValidateLocation(locations, home, "home");
                ValidateLocation(locations, workplace, "workplace");
                if (!row.Age.HasValue) throw new SerializationException("NPC age is required.");
                if (!row.Money.HasValue) throw new SerializationException("NPC money is required.");
                if (row.Traits == null) throw new SerializationException("NPC traits are required.");
                if (row.NeedRates == null) throw new SerializationException("NPC needRates are required.");
                if (!row.NeedRates.HungerPerHour.HasValue || !row.NeedRates.EnergyPerHour.HasValue
                    || !row.NeedRates.SocialPerHour.HasValue)
                    throw new SerializationException("All NPC need rates are required.");

                var rates = new NeedRates(row.NeedRates.HungerPerHour.Value,
                    row.NeedRates.EnergyPerHour.Value, row.NeedRates.SocialPerHour.Value);
                var definition = new NpcDefinition(id, RequiredText(row.Name, "name"), row.Age.Value,
                    RequiredText(row.Gender, "gender"), RequiredText(row.Occupation, "occupation"),
                    home, workplace, row.Money.Value, row.Traits, rates);
                if (definitions.ContainsKey(id)) throw new SerializationException("NPC IDs must be unique.");
                definitions.Add(id, definition);
            }

            return new ReadOnlyCollection<NpcDefinition>(definitions.Values);
        }

        private static string RequiredText(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new SerializationException("NPC " + field + " is required.");
            return value;
        }

        private static void ValidateLocation(LocationMap locations, LocationId id, string field)
        {
            try { locations.GetTravelMinutes(id, id); }
            catch (ArgumentException exception)
            {
                throw new SerializationException("NPC " + field + " references an unknown location.", exception);
            }
        }

        [DataContract]
        private sealed class NpcDocumentDto
        {
            [DataMember(Name = "version", IsRequired = true)] public int? Version { get; set; }
            [DataMember(Name = "npcs", IsRequired = true)] public NpcDto[] Npcs { get; set; }
        }

        [DataContract]
        private sealed class NpcDto
        {
            [DataMember(Name = "id", IsRequired = true)] public string Id { get; set; }
            [DataMember(Name = "name", IsRequired = true)] public string Name { get; set; }
            [DataMember(Name = "age", IsRequired = true)] public int? Age { get; set; }
            [DataMember(Name = "gender", IsRequired = true)] public string Gender { get; set; }
            [DataMember(Name = "occupation", IsRequired = true)] public string Occupation { get; set; }
            [DataMember(Name = "home", IsRequired = true)] public string Home { get; set; }
            [DataMember(Name = "workplace", IsRequired = true)] public string Workplace { get; set; }
            [DataMember(Name = "money", IsRequired = true)] public int? Money { get; set; }
            [DataMember(Name = "traits", IsRequired = true)] public Dictionary<string, int> Traits { get; set; }
            [DataMember(Name = "needRates", IsRequired = true)] public NeedRatesDto NeedRates { get; set; }
        }

        [DataContract]
        private sealed class NeedRatesDto
        {
            [DataMember(Name = "hungerPerHour", IsRequired = true)] public int? HungerPerHour { get; set; }
            [DataMember(Name = "energyPerHour", IsRequired = true)] public int? EnergyPerHour { get; set; }
            [DataMember(Name = "socialPerHour", IsRequired = true)] public int? SocialPerHour { get; set; }
        }
    }
}
