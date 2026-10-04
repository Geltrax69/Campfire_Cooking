using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>The two recipe families from approved Content: cooking at a fire or
    /// kitchen, crafting at a forge or workbench.</summary>
    public enum RecipeCategory
    {
        Cooking,
        Crafting
    }

    /// <summary>One consumed input: an item type and how many units the recipe needs.</summary>
    public sealed class RecipeIngredient
    {
        public RecipeIngredient(ItemTypeId item, int count)
        {
            if (!item.IsValid) throw new ArgumentException("An ingredient needs a valid item type.", nameof(item));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), "Ingredient count must be positive.");
            Item = item;
            Count = count;
        }

        public ItemTypeId Item { get; }
        public int Count { get; }
    }

    /// <summary>What a recipe output produces: new items, or a repair to an existing tool.</summary>
    public enum RecipeOutputKind
    {
        Item,
        Repair
    }

    /// <summary>
    /// One recipe output. Item outputs name the item type and count; repair outputs name
    /// the condition restored and the target category (e.g. "tool", "weapon").
    /// </summary>
    public sealed class RecipeOutput
    {
        private RecipeOutput(RecipeOutputKind kind, ItemTypeId item, int count,
            int conditionRestored, string targetCategory)
        {
            Kind = kind;
            Item = item;
            Count = count;
            ConditionRestored = conditionRestored;
            TargetCategory = targetCategory;
        }

        public static RecipeOutput ForItem(ItemTypeId item, int count)
        {
            if (!item.IsValid) throw new ArgumentException("An item output needs a valid item type.", nameof(item));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), "Output count must be positive.");
            return new RecipeOutput(RecipeOutputKind.Item, item, count, 0, null);
        }

        public static RecipeOutput ForRepair(int conditionRestored, string targetCategory)
        {
            if (conditionRestored < 1)
                throw new ArgumentOutOfRangeException(nameof(conditionRestored), "Repair must restore condition.");
            if (string.IsNullOrWhiteSpace(targetCategory))
                throw new ArgumentException("A repair needs a target category.", nameof(targetCategory));
            return new RecipeOutput(RecipeOutputKind.Repair, default(ItemTypeId), 0,
                conditionRestored, targetCategory);
        }

        public RecipeOutputKind Kind { get; }
        public ItemTypeId Item { get; }
        public int Count { get; }
        public int ConditionRestored { get; }
        public string TargetCategory { get; }
    }

    /// <summary>
    /// Whose tool or kitchen a recipe needs: the permitting NPC and what the actor is
    /// asking to use (e.g. "use of the bakery oven"). Willingness is a simplification:
    /// the permitting NPC agrees when they trust the actor (trust >= 40) or when the
    /// actor IS the permitting NPC. A real persuasion or rental system is future work.
    /// </summary>
    public sealed class RecipePermission
    {
        public RecipePermission(NpcId npc, string what)
        {
            if (!npc.IsValid) throw new ArgumentException("Permission needs a valid NPC.", nameof(npc));
            if (string.IsNullOrWhiteSpace(what))
                throw new ArgumentException("Permission needs to say what is permitted.", nameof(what));
            Npc = npc;
            What = what;
        }

        public NpcId Npc { get; }
        public string What { get; }
    }

    /// <summary>
    /// Immutable recipe loaded from Content/recipes/recipes.json: inputs consumed,
    /// outputs produced, the skill and level gate, difficulty, where it can be done,
    /// whose permission it needs, and how quality is computed.
    /// </summary>
    public sealed class RecipeDefinition
    {
        public RecipeDefinition(
            RecipeId id,
            string name,
            RecipeCategory category,
            SkillId? skill,
            int minLevel,
            int difficulty,
            IEnumerable<RecipeIngredient> inputs,
            IEnumerable<RecipeOutput> outputs,
            int timeMinutes,
            LocationId? location,
            string tool,
            RecipePermission permission,
            string qualityFormula,
            int? qualityFloor,
            int? fermentDays)
        {
            if (!id.IsValid) throw new ArgumentException("A recipe needs a valid ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A recipe needs a name.", nameof(name));
            if (minLevel < 0 || minLevel > 5)
                throw new ArgumentOutOfRangeException(nameof(minLevel), "Min level is 0–5.");
            if (difficulty < 1 || difficulty > 5)
                throw new ArgumentOutOfRangeException(nameof(difficulty), "Difficulty is 1–5.");
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (outputs == null) throw new ArgumentNullException(nameof(outputs));
            if (timeMinutes < 1)
                throw new ArgumentOutOfRangeException(nameof(timeMinutes), "Cooking takes at least a minute.");
            if (qualityFloor.HasValue && (qualityFloor.Value < 0 || qualityFloor.Value > 100))
                throw new ArgumentOutOfRangeException(nameof(qualityFloor), "Quality floor is 0–100.");
            if (fermentDays.HasValue && fermentDays.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(fermentDays), "Ferment days must be positive.");

            var inputList = new List<RecipeIngredient>(inputs);
            var outputList = new List<RecipeOutput>(outputs);
            if (outputList.Count == 0)
                throw new ArgumentException("A recipe must have at least one output.", nameof(outputs));
            foreach (RecipeIngredient ingredient in inputList)
                if (ingredient == null) throw new ArgumentException("Inputs must not contain null.", nameof(inputs));
            foreach (RecipeOutput output in outputList)
                if (output == null) throw new ArgumentException("Outputs must not contain null.", nameof(outputs));

            Id = id;
            Name = name;
            Category = category;
            Skill = skill;
            MinLevel = minLevel;
            Difficulty = difficulty;
            Inputs = new ReadOnlyCollection<RecipeIngredient>(inputList);
            Outputs = new ReadOnlyCollection<RecipeOutput>(outputList);
            TimeMinutes = timeMinutes;
            Location = location;
            Tool = tool;
            Permission = permission;
            QualityFormula = qualityFormula;
            QualityFloor = qualityFloor;
            FermentDays = fermentDays;
        }

        public RecipeId Id { get; }
        public string Name { get; }
        public RecipeCategory Category { get; }
        /// <summary>The skill this recipe practices, or null for open crafting recipes.</summary>
        public SkillId? Skill { get; }
        public int MinLevel { get; }
        public int Difficulty { get; }
        public IReadOnlyList<RecipeIngredient> Inputs { get; }
        public IReadOnlyList<RecipeOutput> Outputs { get; }
        public int TimeMinutes { get; }
        /// <summary>Where the recipe must be performed, or null for anywhere.</summary>
        public LocationId? Location { get; }
        /// <summary>The tool or station used (campfire, oven, forge...), informational except for fuel.</summary>
        public string Tool { get; }
        public RecipePermission Permission { get; }
        /// <summary>The approved quality formula text, e.g. "clamp(avgInputQuality + skillQualityBonus, 0, 100)".</summary>
        public string QualityFormula { get; }
        /// <summary>Minimum quality no matter the inputs, e.g. 80 for the master stew.</summary>
        public int? QualityFloor { get; }
        /// <summary>Days until outputs are ready, or null when they are immediate.</summary>
        public int? FermentDays { get; }
    }
}
