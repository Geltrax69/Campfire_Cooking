using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// All approved recipes from Content/recipes/recipes.json, validated on load.
    /// Query by ID, by skill, or by category; every enumeration is deterministic in
    /// ordinal recipe-ID order.
    /// </summary>
    public sealed class RecipeCatalog
    {
        private readonly Dictionary<RecipeId, RecipeDefinition> _byId;

        private RecipeCatalog(IEnumerable<RecipeDefinition> recipes)
        {
            _byId = new Dictionary<RecipeId, RecipeDefinition>();
            foreach (RecipeDefinition recipe in recipes)
            {
                if (recipe == null) throw new ArgumentException("Recipes must not contain null.", nameof(recipes));
                if (!_byId.TryAdd(recipe.Id, recipe))
                    throw new ArgumentException("Duplicate recipe ID '" + recipe.Id.Value + "'.", nameof(recipes));
            }
        }

        /// <summary>Loads every recipe from &lt;contentRoot&gt;/Content/recipes/recipes.json.</summary>
        public static RecipeCatalog Load(string contentRoot)
        {
            if (string.IsNullOrWhiteSpace(contentRoot))
                throw new ArgumentException("A content root directory is required.", nameof(contentRoot));
            return LoadFile(Path.Combine(contentRoot, "Content", "recipes", "recipes.json"));
        }

        /// <summary>Loads recipes from a single recipes.json file (used by Load).</summary>
        public static RecipeCatalog LoadFile(string path)
        {
            if (!File.Exists(path))
                throw new ArgumentException("No recipes file found at '" + path + "'.", nameof(path));

            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                if (!document.RootElement.TryGetProperty("recipes", out JsonElement recipes))
                    throw new InvalidDataException("recipes.json has no 'recipes' array: " + path + ".");
                var definitions = new List<RecipeDefinition>();
                foreach (JsonElement row in recipes.EnumerateArray())
                    definitions.Add(ParseRecipe(row, path));
                return new RecipeCatalog(definitions);
            }
        }

        public int Count => _byId.Count;

        /// <summary>All recipes in deterministic ordinal recipe-ID order.</summary>
        public IReadOnlyList<RecipeDefinition> All =>
            new ReadOnlyCollection<RecipeDefinition>(_byId.Values.OrderBy(r => r.Id).ToList());

        /// <summary>Returns the recipe, or throws when the ID is unknown.</summary>
        public RecipeDefinition Get(RecipeId id)
        {
            if (!id.IsValid) throw new ArgumentException("A recipe lookup needs a valid ID.", nameof(id));
            if (!_byId.TryGetValue(id, out RecipeDefinition recipe))
                throw new KeyNotFoundException("Unknown recipe '" + id.Value + "'.");
            return recipe;
        }

        public bool TryGet(RecipeId id, out RecipeDefinition recipe)
        {
            if (!id.IsValid) throw new ArgumentException("A recipe lookup needs a valid ID.", nameof(id));
            return _byId.TryGetValue(id, out recipe);
        }

        /// <summary>Recipes gated on a skill, in ordinal recipe-ID order.</summary>
        public IReadOnlyList<RecipeDefinition> GetBySkill(SkillId skill)
        {
            if (!skill.IsValid) throw new ArgumentException("A skill lookup needs a valid ID.", nameof(skill));
            return new ReadOnlyCollection<RecipeDefinition>(_byId.Values
                .Where(r => r.Skill.HasValue && r.Skill.Value == skill)
                .OrderBy(r => r.Id).ToList());
        }

        /// <summary>Recipes of one category, in ordinal recipe-ID order.</summary>
        public IReadOnlyList<RecipeDefinition> GetByCategory(RecipeCategory category)
        {
            return new ReadOnlyCollection<RecipeDefinition>(_byId.Values
                .Where(r => r.Category == category)
                .OrderBy(r => r.Id).ToList());
        }

        private static RecipeDefinition ParseRecipe(JsonElement row, string path)
        {
            var id = new RecipeId(RequiredText(row, "id", path));
            string name = RequiredText(row, "name", path);
            RecipeCategory category = ParseCategory(RequiredText(row, "category", path), id, path);
            SkillId? skill = OptionalText(row, "skill") is string skillText
                ? new SkillId?(new SkillId(skillText))
                : null;
            int minLevel = RequiredInt(row, "minLevel", path);
            int difficulty = RequiredInt(row, "difficulty", path);
            int timeMinutes = RequiredInt(row, "timeMinutes", path);
            LocationId? location = OptionalText(row, "location") is string locationText
                ? new LocationId?(new LocationId(locationText))
                : null;
            string tool = OptionalText(row, "tool");
            RecipePermission permission = ParsePermission(row, path);
            string formula = row.TryGetProperty("qualityRule", out JsonElement rule) &&
                rule.TryGetProperty("formula", out JsonElement formulaElement)
                ? formulaElement.GetString() : null;
            int? floor = row.TryGetProperty("qualityRule", out JsonElement ruleElement) &&
                ruleElement.TryGetProperty("min", out JsonElement floorElement)
                ? new int?(floorElement.GetInt32()) : null;
            int? fermentDays = row.TryGetProperty("fermentDays", out JsonElement fermentElement)
                ? new int?(fermentElement.GetInt32()) : null;

            var inputs = new List<RecipeIngredient>();
            if (row.TryGetProperty("inputs", out JsonElement inputsElement))
                foreach (JsonElement input in inputsElement.EnumerateArray())
                    inputs.Add(new RecipeIngredient(
                        new ItemTypeId(RequiredText(input, "item", path)),
                        RequiredInt(input, "count", path)));

            var outputs = new List<RecipeOutput>();
            if (row.TryGetProperty("outputs", out JsonElement outputsElement))
                foreach (JsonElement output in outputsElement.EnumerateArray())
                    outputs.Add(ParseOutput(output, path));

            return new RecipeDefinition(id, name, category, skill, minLevel, difficulty,
                inputs, outputs, timeMinutes, location, tool, permission, formula, floor, fermentDays);
        }

        private static RecipeOutput ParseOutput(JsonElement output, string path)
        {
            if (output.TryGetProperty("repair", out JsonElement repair))
            {
                return RecipeOutput.ForRepair(
                    repair.GetProperty("conditionRestored").GetInt32(),
                    RequiredText(repair, "targetCategory", path));
            }
            return RecipeOutput.ForItem(
                new ItemTypeId(RequiredText(output, "item", path)),
                RequiredInt(output, "count", path));
        }

        private static RecipePermission ParsePermission(JsonElement row, string path)
        {
            if (!row.TryGetProperty("permission", out JsonElement permission)) return null;
            return new RecipePermission(
                new NpcId(RequiredText(permission, "npc", path)),
                RequiredText(permission, "what", path));
        }

        private static RecipeCategory ParseCategory(string value, RecipeId id, string path)
        {
            if (string.Equals(value, "cooking", StringComparison.OrdinalIgnoreCase)) return RecipeCategory.Cooking;
            if (string.Equals(value, "crafting", StringComparison.OrdinalIgnoreCase)) return RecipeCategory.Crafting;
            throw new InvalidDataException("Recipe '" + id.Value + "' has unknown category '" + value + "' in " + path + ".");
        }

        private static string RequiredText(JsonElement element, string property, string path)
        {
            if (!element.TryGetProperty(property, out JsonElement value) ||
                value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
                throw new InvalidDataException("Missing or empty '" + property + "' in " + path + ".");
            return value.GetString();
        }

        private static string OptionalText(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null)
                return null;
            return value.GetString();
        }

        private static int RequiredInt(JsonElement element, string property, string path)
        {
            if (!element.TryGetProperty(property, out JsonElement value) ||
                value.ValueKind != JsonValueKind.Number)
                throw new InvalidDataException("Missing or non-numeric '" + property + "' in " + path + ".");
            return value.GetInt32();
        }
    }
}
