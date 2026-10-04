using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Loads the approved "family" arrays from Content/npcs/npcs.json (P7-02).
    /// Kept separate from NpcContentLoader so the NPC definition schema stays
    /// untouched; relations map NPC ID to their family links in document order.
    /// </summary>
    public static class FamilyContentLoader
    {
        private static readonly HashSet<string> KnownRelations = new HashSet<string>(StringComparer.Ordinal)
        {
            "father", "mother", "son", "daughter", "husband", "wife", "brother", "sister"
        };

        /// <summary>
        /// Reads the family arrays keyed by NPC ID. NPCs without a family array
        /// are absent from the result (not an error).
        /// </summary>
        public static IReadOnlyDictionary<NpcId, IReadOnlyList<FamilyRelation>> Load(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (!root.TryGetProperty("npcs", out var npcs) || npcs.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("NPC content requires an \"npcs\" array.");

            var result = new SortedDictionary<NpcId, IReadOnlyList<FamilyRelation>>();
            foreach (var npc in npcs.EnumerateArray())
            {
                if (!npc.TryGetProperty("id", out var idProperty) || idProperty.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("NPC rows require a string \"id\".");
                var id = new NpcId(idProperty.GetString());
                if (!npc.TryGetProperty("family", out var family) || family.ValueKind == JsonValueKind.Null)
                    continue;
                if (family.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException($"NPC \"{id.Value}\" has a non-array \"family\".");

                var relations = new List<FamilyRelation>();
                foreach (var entry in family.EnumerateArray())
                {
                    if (!entry.TryGetProperty("relation", out var relationProperty)
                        || relationProperty.ValueKind != JsonValueKind.String)
                        throw new InvalidDataException($"NPC \"{id.Value}\" has a family entry without a \"relation\".");
                    string relation = relationProperty.GetString();
                    if (!KnownRelations.Contains(relation))
                        throw new InvalidDataException($"NPC \"{id.Value}\" has unknown relation \"{relation}\".");
                    if (!entry.TryGetProperty("id", out var targetProperty)
                        || targetProperty.ValueKind != JsonValueKind.String)
                        throw new InvalidDataException($"NPC \"{id.Value}\" has a family entry without an \"id\".");
                    relations.Add(new FamilyRelation(relation, new NpcId(targetProperty.GetString())));
                }
                result[id] = new ReadOnlyCollection<FamilyRelation>(relations);
            }
            return new ReadOnlyDictionary<NpcId, IReadOnlyList<FamilyRelation>>(result);
        }
    }
}
