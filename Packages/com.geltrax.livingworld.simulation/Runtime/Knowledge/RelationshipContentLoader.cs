using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Loads and validates directed NPC relationships from the approved npcs.json schema.
    /// Follows the NpcContentLoader pattern: private DTOs, strict required fields, and
    /// clear SerializationException failures. The world build calls this after NPC
    /// definitions are parsed and installs the result with
    /// KnowledgeState.InitializeRelationships.
    /// </summary>
    public static class RelationshipContentLoader
    {
        private const int SupportedVersion = 1;

        public static IReadOnlyList<Relationship> Load(Stream stream, IReadOnlyCollection<NpcId> knownNpcs)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (knownNpcs == null) throw new ArgumentNullException(nameof(knownNpcs));

            var serializer = new DataContractJsonSerializer(typeof(NpcDocumentDto));
            var document = (NpcDocumentDto)serializer.ReadObject(stream);
            if (document == null) throw new SerializationException("NPC document must not be null.");
            if (!document.Version.HasValue || document.Version.Value != SupportedVersion)
                throw new SerializationException("Unsupported or missing NPC content version.");
            if (document.Npcs == null) throw new SerializationException("NPC rows are required.");

            var pairs = new HashSet<string>(StringComparer.Ordinal);
            var relationships = new List<Relationship>();
            foreach (NpcDto row in document.Npcs)
            {
                if (row == null) throw new SerializationException("NPC rows must not contain null.");
                var from = new NpcId(RequiredText(row.Id, "id"));
                if (!knownNpcs.Contains(from))
                    throw new SerializationException("Relationship row for unknown NPC '" + from.Value + "'.");
                if (row.Relationships == null) continue;

                foreach (RelationshipDto entry in row.Relationships)
                {
                    if (entry == null) throw new SerializationException("Relationship entries must not be null.");
                    var to = new NpcId(RequiredText(entry.With, "with"));
                    if (!knownNpcs.Contains(to))
                        throw new SerializationException("NPC '" + from.Value +
                            "' has a relationship with unknown NPC '" + to.Value + "'.");
                    if (!entry.Trust.HasValue) throw new SerializationException("Relationship trust is required.");
                    if (!entry.Affection.HasValue)
                        throw new SerializationException("Relationship affection is required.");
                    string key = from.Value + "->" + to.Value;
                    if (!pairs.Add(key))
                        throw new SerializationException("Duplicate relationship '" + key + "'.");
                    try
                    {
                        relationships.Add(new Relationship(from, to, entry.Trust.Value,
                            entry.Affection.Value, RequiredText(entry.Reason, "reason")));
                    }
                    catch (ArgumentException failure)
                    {
                        throw new SerializationException("Invalid relationship '" + key + "': " + failure.Message,
                            failure);
                    }
                }
            }

            relationships.Sort((left, right) =>
            {
                int byFrom = left.From.CompareTo(right.From);
                return byFrom != 0 ? byFrom : left.To.CompareTo(right.To);
            });
            return relationships.AsReadOnly();
        }

        private static string RequiredText(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new SerializationException("NPC " + field + " is required.");
            return value;
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
            [DataMember(Name = "relationships")] public RelationshipDto[] Relationships { get; set; }
        }

        [DataContract]
        private sealed class RelationshipDto
        {
            [DataMember(Name = "with", IsRequired = true)] public string With { get; set; }
            [DataMember(Name = "trust", IsRequired = true)] public int? Trust { get; set; }
            [DataMember(Name = "affection", IsRequired = true)] public int? Affection { get; set; }
            [DataMember(Name = "reason", IsRequired = true)] public string Reason { get; set; }
        }
    }
}
