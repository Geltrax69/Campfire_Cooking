using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text.Json;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Persistence
{
    /// <summary>
    /// Approved immutable definitions a save document references by ID: the item catalog,
    /// the location map, every NPC definition and the reputation group IDs. The loader
    /// resolves every saved reference against this bundle, so an unknown ID is rejected
    /// during validation, before any world state is built.
    /// </summary>
    internal sealed class ContentBundle
    {
        private ContentBundle(ItemCatalog catalog, LocationMap map,
            IReadOnlyDictionary<NpcId, NpcDefinition> npcDefinitions,
            IReadOnlyDictionary<ReputationGroupId, string> reputationGroups)
        {
            Catalog = catalog;
            Map = map;
            NpcDefinitions = npcDefinitions;
            ReputationGroups = reputationGroups;
        }

        public ItemCatalog Catalog { get; }
        public LocationMap Map { get; }
        public IReadOnlyDictionary<NpcId, NpcDefinition> NpcDefinitions { get; }
        public IReadOnlyDictionary<ReputationGroupId, string> ReputationGroups { get; }

        /// <summary>Loads every approved definition file under <paramref name="contentRoot"/>/Content.</summary>
        /// <param name="contentRoot">Directory containing the approved Content/ folder (usually the repo root).</param>
        public static ContentBundle Load(string contentRoot)
        {
            if (string.IsNullOrWhiteSpace(contentRoot))
                throw new LoadException("A content root directory is required to resolve saved definition IDs.");
            string content = Path.Combine(contentRoot, "Content");
            if (!Directory.Exists(content))
                throw new LoadException("No Content/ folder found under '" + contentRoot + "'.");

            ItemCatalog catalog = LoadItemCatalog(Path.Combine(content, "items", "items.json"));
            LocationMap map = LoadLocationMap(Path.Combine(content, "world", "locations.json"));
            IReadOnlyDictionary<NpcId, NpcDefinition> npcs =
                LoadNpcDefinitions(Path.Combine(content, "npcs", "npcs.json"), map);
            IReadOnlyDictionary<ReputationGroupId, string> groups =
                LoadReputationGroups(Path.Combine(content, "social", "social.json"));
            return new ContentBundle(catalog, map, npcs, groups);
        }

        /// <summary>Rejects with <see cref="LoadException"/> when the item is not in the approved catalog.</summary>
        public void RequireItem(ItemTypeId id, string where)
        {
            try { Catalog.Require(id); }
            catch (Exception failure)
            {
                throw new LoadException("Unknown item type '" + id.Value + "' in " + where + ".", failure);
            }
        }

        private static ItemCatalog LoadItemCatalog(string path)
        {
            JsonDocument document = ReadJson(path, "item catalog");
            var definitions = new List<ItemDefinition>();
            var seen = new HashSet<ItemTypeId>();
            foreach (JsonElement row in Each(document, "items", path))
            {
                var id = new ItemTypeId(RequiredText(row, "id", path));
                if (!seen.Add(id)) throw new LoadException("Duplicate item ID '" + id.Value + "' in " + path + ".");
                definitions.Add(new ItemDefinition(id, RequiredText(row, "name", path),
                    RequiredText(row, "category", path), row.GetProperty("baseValue").GetInt32(),
                    row.GetProperty("phase").GetInt32(), perishable: LoadPerishable(row, path)));
            }
            try { return new ItemCatalog(definitions); }
            catch (Exception failure) { throw new LoadException("Invalid item catalog in " + path + ".", failure); }
        }

        private static PerishableInfo LoadPerishable(JsonElement row, string path)
        {
            // Reason: the approved `perishable` block in items.json is the two-stage
            // spoilage spec (P2-09); items without it never spoil.
            if (!row.TryGetProperty("perishable", out JsonElement spec)
                || spec.ValueKind == JsonValueKind.Null) return null;
            int? hunger = null, health = null;
            if (spec.TryGetProperty("staleEffect", out JsonElement effect))
            {
                if (effect.TryGetProperty("hunger", out JsonElement h)) hunger = h.GetInt32();
                if (effect.TryGetProperty("health", out JsonElement hp)) health = hp.GetInt32();
            }
            string staleName = spec.TryGetProperty("staleName", out JsonElement sn) ? sn.GetString() : null;
            string spoiledName = spec.TryGetProperty("spoiledName", out JsonElement rn) ? rn.GetString() : null;
            int pricePercent = (int)Math.Round(spec.GetProperty("stalePriceFactor").GetDouble() * 100);
            return new PerishableInfo(spec.GetProperty("freshDays").GetInt32(),
                spec.GetProperty("staleDays").GetInt32(), pricePercent,
                staleName, spoiledName, hunger, health);
        }

        private static LocationMap LoadLocationMap(string path)
        {
            JsonDocument document = ReadJson(path, "location map");
            var definitions = new List<LocationDefinition>();
            foreach (JsonElement row in Each(document, "locations", path))
            {
                string owner = RequiredText(row, "owner", path);
                JsonElement position = row.GetProperty("position");
                definitions.Add(new LocationDefinition(
                    new LocationId(RequiredText(row, "id", path)),
                    RequiredText(row, "name", path), RequiredText(row, "type", path),
                    position.GetProperty("x").GetInt32(), position.GetProperty("y").GetInt32(),
                    owner == "village" ? (NpcId?)null : new NpcId(owner)));
            }
            var links = new List<TravelLink>();
            foreach (JsonElement row in Each(document, "travelMinutes", path))
                links.Add(new TravelLink(new LocationId(RequiredText(row, "from", path)),
                    new LocationId(RequiredText(row, "to", path)), row.GetProperty("minutes").GetInt32()));
            try { return new LocationMap(definitions, links); }
            catch (Exception failure) { throw new LoadException("Invalid location map in " + path + ".", failure); }
        }

        private static IReadOnlyDictionary<NpcId, NpcDefinition> LoadNpcDefinitions(string path, LocationMap map)
        {
            if (!File.Exists(path)) throw new LoadException("Missing NPC definitions file: " + path + ".");
            try
            {
                using (Stream stream = File.OpenRead(path))
                {
                    var byId = new SortedDictionary<NpcId, NpcDefinition>();
                    foreach (NpcDefinition definition in NpcContentLoader.Load(stream, map))
                        byId.Add(definition.Id, definition);
                    return byId;
                }
            }
            catch (LoadException) { throw; }
            catch (Exception failure) { throw new LoadException("Invalid NPC definitions in " + path + ".", failure); }
        }

        private static IReadOnlyDictionary<ReputationGroupId, string> LoadReputationGroups(string path)
        {
            JsonDocument document = ReadJson(path, "reputation groups");
            var groups = new SortedDictionary<ReputationGroupId, string>();
            foreach (JsonElement row in Each(document, "reputationGroups", path))
            {
                var id = new ReputationGroupId(RequiredText(row, "id", path));
                if (groups.ContainsKey(id))
                    throw new LoadException("Duplicate reputation group '" + id.Value + "' in " + path + ".");
                groups.Add(id, RequiredText(row, "name", path));
            }
            return groups;
        }

        private static JsonDocument ReadJson(string path, string what)
        {
            if (!File.Exists(path)) throw new LoadException("Missing " + what + " file: " + path + ".");
            try { return JsonDocument.Parse(File.ReadAllText(path)); }
            catch (Exception failure) { throw new LoadException("Invalid " + what + " JSON in " + path + ".", failure); }
        }

        private static IEnumerable<JsonElement> Each(JsonDocument document, string property, string path)
        {
            JsonElement array;
            if (!document.RootElement.TryGetProperty(property, out array) ||
                array.ValueKind != JsonValueKind.Array)
                throw new LoadException("Missing '" + property + "' array in " + path + ".");
            return array.EnumerateArray();
        }

        private static string RequiredText(JsonElement row, string property, string path)
        {
            JsonElement value;
            if (!row.TryGetProperty(property, out value) || value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
                throw new LoadException("Missing '" + property + "' in " + path + ".");
            return value.GetString();
        }
    }
}
