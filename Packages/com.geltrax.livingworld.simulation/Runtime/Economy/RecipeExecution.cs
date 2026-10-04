using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>How a cooking or crafting attempt ended.</summary>
    public enum CookOutcome
    {
        /// <summary>Outputs were made (or a repair performed) and added to the inventory.</summary>
        Success,
        /// <summary>The attempt failed: inputs are lost, nothing is produced.</summary>
        Failure,
        /// <summary>
        /// The work is done and the inputs are spent, but the outputs need time
        /// (e.g. ale fermenting): they arrive on <see cref="CookResult.AvailableDay"/>.
        /// Delayed delivery is future work; the result carries everything needed.
        /// </summary>
        Fermenting
    }

    /// <summary>One item output produced by a successful or fermenting cook.</summary>
    public sealed class CookedOutput
    {
        public CookedOutput(ItemTypeId item, int count)
        {
            if (!item.IsValid) throw new ArgumentException("An output needs a valid item type.", nameof(item));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), "Output count must be positive.");
            Item = item;
            Count = count;
        }

        public ItemTypeId Item { get; }
        public int Count { get; }
    }

    /// <summary>
    /// A repair the cook performed instead of creating items. Tool condition is not
    /// implemented yet, so this is reported for a future system to apply; the inputs
    /// are still consumed and practice is still granted.
    /// </summary>
    public sealed class PerformedRepair
    {
        public PerformedRepair(int conditionRestored, string targetCategory)
        {
            if (conditionRestored < 1)
                throw new ArgumentOutOfRangeException(nameof(conditionRestored), "Repair must restore condition.");
            if (string.IsNullOrWhiteSpace(targetCategory))
                throw new ArgumentException("A repair needs a target category.", nameof(targetCategory));
            ConditionRestored = conditionRestored;
            TargetCategory = targetCategory;
        }

        public int ConditionRestored { get; }
        public string TargetCategory { get; }
    }

    /// <summary>Everything a recipe execution produced: outcome, quality, outputs, practice.</summary>
    public sealed class CookResult
    {
        public CookResult(CookOutcome outcome, int quality, IEnumerable<CookedOutput> outputs,
            int practicePoints, long availableDay, PerformedRepair repair)
        {
            if (outputs == null) throw new ArgumentNullException(nameof(outputs));
            if (quality < 0 || quality > 100)
                throw new ArgumentOutOfRangeException(nameof(quality), "Quality is 0–100.");
            if (practicePoints < 0)
                throw new ArgumentOutOfRangeException(nameof(practicePoints));
            Outcome = outcome;
            Quality = quality;
            Outputs = new ReadOnlyCollection<CookedOutput>(new List<CookedOutput>(outputs));
            PracticePoints = practicePoints;
            AvailableDay = availableDay;
            Repair = repair;
        }

        public CookOutcome Outcome { get; }
        /// <summary>0–100 on success/fermenting (0 for repairs, which have no quality); 0 on failure.</summary>
        public int Quality { get; }
        /// <summary>
        /// Items made. On <see cref="CookOutcome.Success"/> they are already in the
        /// inventory; on <see cref="CookOutcome.Fermenting"/> they are pending until
        /// <see cref="AvailableDay"/>; on <see cref="CookOutcome.Failure"/> empty.
        /// </summary>
        public IReadOnlyList<CookedOutput> Outputs { get; }
        /// <summary>Skill practice points actually granted (0 on failure, or when the recipe has no skill).</summary>
        public int PracticePoints { get; }
        /// <summary>Game day the fermented outputs become available; -1 unless fermenting.</summary>
        public long AvailableDay { get; }
        /// <summary>The repair performed, or null when the recipe creates items.</summary>
        public PerformedRepair Repair { get; }
    }

    /// <summary>
    /// Executes recipes: checks whether an actor may attempt one (<see cref="CanCook"/>)
    /// and runs it (<see cref="Cook"/>), consuming inputs, rolling failure on the
    /// world's seeded RNG, and producing quality-scaled outputs. All randomness comes
    /// from the caller's RNG, so identical seeds give identical results.
    /// </summary>
    public static class RecipeExecution
    {
        /// <summary>Trust the permitting NPC needs toward the actor (simplification; see RecipePermission).</summary>
        public const int PermissionTrustThreshold = 40;

        /// <summary>The tool name that demands firewood fuel, matching Content recipes.</summary>
        public const string CampfireTool = "campfire";

        /// <summary>Content ID of the fuel a campfire recipe burns.</summary>
        public static readonly ItemTypeId FirewoodItemId = new ItemTypeId("item_firewood");

        /// <summary>
        /// Quality assigned to inputs because inventories track age, not quality, per lot.
        /// Per-lot quality tracking is future work; until then every input counts as 50.
        /// </summary>
        public const int DefaultInputQuality = 50;

        /// <summary>
        /// Failure chance in percent: (difficulty - skillLevel + 1) × 10, clamped to 0–40.
        /// A recipe with no skill counts skill level 0, so an unskilled smith fails
        /// simple forge work 20% of the time. The design's "~30% waste at levels 1–2"
        /// is simplified to all-or-nothing: failure loses 100% of inputs, success wastes 0%.
        /// </summary>
        public static int FailureChance(int difficulty, int skillLevel)
        {
            int chance = (difficulty - skillLevel + 1) * 10;
            if (chance < 0) return 0;
            if (chance > 40) return 40;
            return chance;
        }

        /// <summary>
        /// Checks whether the actor may attempt the recipe right now: skill gate,
        /// inputs, campfire fuel, location, and the permitting NPC's willingness.
        /// Returns (true, "") when everything passes, otherwise (false, reason).
        /// </summary>
        public static (bool CanDo, string Reason) CanCook(
            NpcId actor,
            RecipeDefinition recipe,
            SkillStore skills,
            Inventory inventory,
            LocationId location,
            RelationshipRegistry relationships)
        {
            if (!actor.IsValid) throw new ArgumentException("An actor is required.", nameof(actor));
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (skills == null) throw new ArgumentNullException(nameof(skills));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (relationships == null) throw new ArgumentNullException(nameof(relationships));

            if (recipe.Skill.HasValue)
            {
                int level = skills.GetLevel(recipe.Skill.Value);
                if (level < recipe.MinLevel)
                    return (false, "Needs " + recipe.Skill.Value.Value + " level " + recipe.MinLevel +
                        " (have " + level + ").");
            }

            foreach (RecipeIngredient ingredient in recipe.Inputs)
            {
                int have = inventory.Count(ingredient.Item);
                if (have < ingredient.Count)
                    return (false, "Missing " + (ingredient.Count - have) + "× " + ingredient.Item.Value +
                        " (have " + have + ").");
            }

            // Fuel is part of the inputs for every approved campfire recipe, but the
            // fire itself needs fuel even if a recipe forgot to list it.
            if (string.Equals(recipe.Tool, CampfireTool, StringComparison.OrdinalIgnoreCase) &&
                inventory.Count(FirewoodItemId) < 1)
                return (false, "Needs 1× " + FirewoodItemId.Value + " to fuel the campfire.");

            if (recipe.Location.HasValue && location != recipe.Location.Value)
                return (false, "Must be at " + recipe.Location.Value.Value + " to make this.");

            if (recipe.Permission != null &&
                actor != recipe.Permission.Npc &&
                relationships.Trust(recipe.Permission.Npc, actor) < PermissionTrustThreshold)
                return (false, "Needs " + recipe.Permission.Npc.Value + "'s permission (" +
                    recipe.Permission.What + "); trust is below " + PermissionTrustThreshold + ".");

            return (true, string.Empty);
        }

        /// <summary>
        /// Runs the recipe: consumes all inputs (including fuel), rolls failure on
        /// <paramref name="rng"/>, and on success produces quality-scaled outputs,
        /// grants 1 practice point (taught practice is the caller's logic), and
        /// handles fermentation and repairs. Call <see cref="CanCook"/> first; a
        /// missing input throws because that is a caller bug, not a dice roll.
        /// </summary>
        public static CookResult Cook(
            RecipeDefinition recipe,
            NpcId actor,
            SkillStore skills,
            Inventory inventory,
            long day,
            SimRng rng)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (!actor.IsValid) throw new ArgumentException("An actor is required.", nameof(actor));
            if (skills == null) throw new ArgumentNullException(nameof(skills));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day), "Game day must not be negative.");
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            // Inputs burn first: a failed attempt still eats the ingredients.
            foreach (RecipeIngredient ingredient in recipe.Inputs)
                if (!inventory.TryRemove(ingredient.Item, ingredient.Count))
                    throw new InvalidOperationException(
                        "Cook called without inputs present; call CanCook first. Missing: " +
                        ingredient.Item.Value + ".");

            int skillLevel = recipe.Skill.HasValue ? skills.GetLevel(recipe.Skill.Value) : 0;
            if (rng.NextInt(100) < FailureChance(recipe.Difficulty, skillLevel))
                return new CookResult(CookOutcome.Failure, 0,
                    new CookedOutput[0], 0, -1, null);

            // Success: quality from default input quality plus the cook's bonus,
            // clamped to 0–100 and never below the recipe's floor (master stew: 80).
            // Inventories don't track per-lot quality yet, so cooked quality lives
            // on the result; quality-aware pricing is P3-03 work.
            int quality = DefaultInputQuality +
                (recipe.Skill.HasValue ? skills.GetQualityBonus(recipe.Skill.Value) : 0);
            if (quality > 100) quality = 100;
            if (recipe.QualityFloor.HasValue && quality < recipe.QualityFloor.Value)
                quality = recipe.QualityFloor.Value;

            int practice = recipe.Skill.HasValue
                ? skills.Practice(recipe.Skill.Value, 1, false, day, rng)
                : 0;

            var itemOutputs = new List<CookedOutput>();
            PerformedRepair repair = null;
            foreach (RecipeOutput output in recipe.Outputs)
            {
                if (output.Kind == RecipeOutputKind.Repair)
                {
                    repair = new PerformedRepair(output.ConditionRestored, output.TargetCategory);
                }
                else
                {
                    itemOutputs.Add(new CookedOutput(output.Item, output.Count));
                }
            }

            if (recipe.FermentDays.HasValue)
            {
                // Brewed but not ready: outputs are pending until the available day.
                return new CookResult(CookOutcome.Fermenting, quality, itemOutputs,
                    practice, day + recipe.FermentDays.Value, repair);
            }

            foreach (CookedOutput made in itemOutputs)
                inventory.Add(made.Item, made.Count);

            return new CookResult(CookOutcome.Success, quality, itemOutputs, practice, -1, repair);
        }
    }
}
