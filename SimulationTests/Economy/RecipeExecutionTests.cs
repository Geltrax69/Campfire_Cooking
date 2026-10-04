using System;
using System.IO;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Recipe execution: level gates, input checks, location and permission rules,
    /// campfire fuel, failure rolls, quality scaling, fermentation and repair.
    /// Statistical tests use fixed SimRng seeds so they are exactly reproducible.
    /// </summary>
    [TestFixture]
    public sealed class RecipeExecutionTests
    {
        private static readonly SkillId Cooking = new SkillId("skill_cooking");
        private static readonly NpcId Player = new NpcId("npc_player");
        private static readonly NpcId Oda = new NpcId("npc_oda_fenn");
        private static readonly NpcId Bessa = new NpcId("npc_bessa_marlowe");
        private static readonly NpcId Doran = new NpcId("npc_doran_kettle");
        private const long Day = 12;

        private RecipeCatalog _recipes;
        private ItemCatalog _items;

        [OneTimeSetUp]
        public void LoadContent()
        {
            string root = RepositoryRoot();
            _recipes = RecipeCatalog.Load(root);
            _items = ContentBundle.Load(root).Catalog;
        }

        [Test]
        public void AtLevelRecipeSucceedsUsually()
        {
            // Level 1 cook, difficulty 1 recipe: fail chance is 10%, so ~90% of
            // 100 trials should succeed (seed 1 gives exactly 94).
            int successes = CountSuccesses(_recipes.Get(new RecipeId("recipe_campfire_stew")), 1, seed: 1, trials: 100);
            Assert.That(successes, Is.InRange(80, 95), "Expected ~90% success, got " + successes + "%.");
        }

        [Test]
        public void TwoLevelsAboveFailsOften()
        {
            // Level 1 cook, difficulty 3 recipe: fail chance is 30%, so ~30% of
            // 100 trials should fail (seed 1 gives exactly 22 failures).
            // Cook executes without level gates; CanCook is the caller's check.
            int failures = 100 - CountSuccesses(_recipes.Get(new RecipeId("recipe_apple_pie")), 1, seed: 1, trials: 100);
            Assert.That(failures, Is.InRange(20, 40), "Expected ~30% failure, got " + failures + "%.");
        }

        [Test]
        public void FailureLosesInputs()
        {
            // Find a seed whose first roll fails (30% fail chance), then check the
            // failure burned the inputs and produced nothing.
            RecipeDefinition pie = _recipes.Get(new RecipeId("recipe_apple_pie"));
            CookResult failure = null;
            for (ulong seed = 1; seed <= 1000 && failure == null; seed++)
            {
                var attempt = CookOnce(pie, 1, seed);
                if (attempt.Result.Outcome == CookOutcome.Failure) failure = attempt.Result;
            }
            Assert.That(failure, Is.Not.Null, "No failing roll found in 1000 seeds.");

            Assert.That(failure.Outputs.Count, Is.EqualTo(0));
            Assert.That(failure.PracticePoints, Is.EqualTo(0));
            Assert.That(failure.Quality, Is.EqualTo(0));
        }

        [Test]
        public void FailureConsumesInputsFromInventory()
        {
            RecipeDefinition pie = _recipes.Get(new RecipeId("recipe_apple_pie"));
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                var attempt = CookOnce(pie, 1, seed);
                if (attempt.Result.Outcome != CookOutcome.Failure) continue;
                Assert.That(attempt.Inventory.Count(new ItemTypeId("item_apple")), Is.EqualTo(0));
                Assert.That(attempt.Inventory.Count(new ItemTypeId("item_flour")), Is.EqualTo(0));
                return;
            }
            Assert.Fail("No failing roll found in 1000 seeds.");
        }

        [Test]
        public void QualityScalesWithSkill()
        {
            // Level 1: quality = 50 + 0 bonus. Level 5: quality = 50 + 4 bonus.
            RecipeDefinition stew = _recipes.Get(new RecipeId("recipe_campfire_stew"));
            CookResult low = CookUntilSuccess(stew, 1);
            CookResult high = CookUntilSuccess(stew, 5);
            Assert.That(high.Quality, Is.GreaterThan(low.Quality));
            Assert.That(low.Quality, Is.EqualTo(50));
            Assert.That(high.Quality, Is.EqualTo(54));
        }

        [Test]
        public void LevelGateBlocks()
        {
            // A level-1 cook cannot attempt the minLevel-2 rye bake, even standing
            // in the bakery as Oda himself with a full basket.
            RecipeDefinition bread = _recipes.Get(new RecipeId("recipe_bake_bread_rye"));
            var skills = SkillsAt(1);
            var inventory = StockedInventory(bread);
            var (canDo, reason) = RecipeExecution.CanCook(Oda, bread, skills, inventory,
                new LocationId("loc_bakery"), new RelationshipRegistry());
            Assert.That(canDo, Is.False);
            Assert.That(reason, Does.Contain("level").IgnoreCase);
        }

        [Test]
        public void MissingInputsBlocks()
        {
            RecipeDefinition stew = _recipes.Get(new RecipeId("recipe_campfire_stew"));
            var (canDo, reason) = RecipeExecution.CanCook(Player, stew, SkillsAt(1),
                new Inventory(_items), new LocationId("loc_river_alder"), new RelationshipRegistry());
            Assert.That(canDo, Is.False);
            Assert.That(reason, Does.Contain("item_fish"));
        }

        [Test]
        public void WrongLocationBlocks()
        {
            RecipeDefinition stew = _recipes.Get(new RecipeId("recipe_campfire_stew"));
            var (canDo, reason) = RecipeExecution.CanCook(Player, stew, SkillsAt(1),
                StockedInventory(stew), new LocationId("loc_tavern"), new RelationshipRegistry());
            Assert.That(canDo, Is.False);
            Assert.That(reason, Does.Contain("loc_river_alder"));
        }

        [Test]
        public void CampfireNeedsFirewoodFuel()
        {
            // A campfire-tool recipe whose inputs somehow lack firewood is still
            // blocked: the fire itself needs fuel, consumed with the inputs.
            var recipe = new RecipeDefinition(
                new RecipeId("recipe_test_campfire"), "Test Campfire", RecipeCategory.Cooking,
                Cooking, 1, 1,
                new[] { new RecipeIngredient(new ItemTypeId("item_fish"), 1) },
                new[] { RecipeOutput.ForItem(new ItemTypeId("item_roasted_fish"), 1) },
                20, new LocationId("loc_river_alder"), "campfire", null, "clamp(avgInputQuality, 0, 100)", null, null);
            var inventory = new Inventory(_items);
            inventory.Add(new ItemTypeId("item_fish"), 1);
            var (canDo, reason) = RecipeExecution.CanCook(Player, recipe, SkillsAt(1),
                inventory, new LocationId("loc_river_alder"), new RelationshipRegistry());
            Assert.That(canDo, Is.False);
            Assert.That(reason, Does.Contain("firewood"));
        }

        [Test]
        public void PermissionBlockedWhenDistrusted()
        {
            // The rye bake needs Oda's oven. A stranger Oda distrusts (trust 10)
            // is refused; a trusted friend (trust 40) is allowed.
            RecipeDefinition bread = _recipes.Get(new RecipeId("recipe_bake_bread_rye"));
            var trusted = new RelationshipRegistry();
            trusted.Set(new Relationship(Oda, Player, 40, 50, "shared a harvest"));
            var distrusted = new RelationshipRegistry();
            distrusted.Set(new Relationship(Oda, Player, 10, 30, "a bad first impression"));

            var (refused, refuseReason) = RecipeExecution.CanCook(Player, bread, SkillsAt(2),
                StockedInventory(bread), new LocationId("loc_bakery"), distrusted);
            Assert.That(refused, Is.False);
            Assert.That(refuseReason, Does.Contain("permission").IgnoreCase);

            var (allowed, _) = RecipeExecution.CanCook(Player, bread, SkillsAt(2),
                StockedInventory(bread), new LocationId("loc_bakery"), trusted);
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void PermittingNpcAlwaysAllowed()
        {
            // Oda needs no one's permission to use his own oven.
            RecipeDefinition bread = _recipes.Get(new RecipeId("recipe_bake_bread_rye"));
            var (canDo, reason) = RecipeExecution.CanCook(Oda, bread, SkillsAt(2),
                StockedInventory(bread), new LocationId("loc_bakery"), new RelationshipRegistry());
            Assert.That(canDo, Is.True, reason);
        }

        [Test]
        public void MasterStewQualityFloor()
        {
            // Even a level-5 cook's +4 bonus only reaches 54 from plain inputs;
            // the master's stew is never served below 80.
            RecipeDefinition master = _recipes.Get(new RecipeId("recipe_master_stew"));
            CookResult result = CookUntilSuccess(master, 5);
            Assert.That(result.Quality, Is.GreaterThanOrEqualTo(80));
        }

        [Test]
        public void SuccessfulCookAddsOutputsAndPractice()
        {
            RecipeDefinition stew = _recipes.Get(new RecipeId("recipe_campfire_stew"));
            var attempt = CookUntilSuccessWithState(stew, 1);
            Assert.That(attempt.Result.Outcome, Is.EqualTo(CookOutcome.Success));
            Assert.That(attempt.Result.PracticePoints, Is.EqualTo(1));
            Assert.That(attempt.Inventory.Count(new ItemTypeId("item_stew")), Is.EqualTo(2));
            Assert.That(attempt.Inventory.Count(new ItemTypeId("item_fish")), Is.EqualTo(0));
            Assert.That(attempt.Inventory.Count(new ItemTypeId("item_firewood")), Is.EqualTo(0));
            Assert.That(attempt.Skills.Get(new SkillId("skill_cooking")).PracticePoints, Is.EqualTo(1));
        }

        [Test]
        public void BrewAleFermentsInsteadOfDelivering()
        {
            // Ale's outputs are not available for 3 days; the grain is spent now.
            RecipeDefinition ale = _recipes.Get(new RecipeId("recipe_brew_ale"));
            CookResult result = CookUntilSuccess(ale, 2, actor: Bessa);
            Assert.That(result.Outcome, Is.EqualTo(CookOutcome.Fermenting));
            Assert.That(result.AvailableDay, Is.EqualTo(Day + 3));
            Assert.That(result.PracticePoints, Is.EqualTo(1));
            // 20 ale are pending, none in hand yet.
            Assert.That(result.Outputs.Count, Is.EqualTo(1));
            Assert.That(result.Outputs[0].Item, Is.EqualTo(new ItemTypeId("item_ale")));
            Assert.That(result.Outputs[0].Count, Is.EqualTo(20));
        }

        [Test]
        public void RepairRecipeRestoresInsteadOfCreating()
        {
            // Patching a tool consumes the iron and asks for a repair, not an item.
            // Tool condition is not implemented yet, so the repair is reported for
            // a future system to apply.
            RecipeDefinition patch = _recipes.Get(new RecipeId("recipe_patch_tool"));
            CookResult result = CookUntilSuccess(patch, 0, actor: Doran);
            Assert.That(result.Outcome, Is.EqualTo(CookOutcome.Success));
            Assert.That(result.Outputs.Count, Is.EqualTo(0));
            Assert.That(result.Repair, Is.Not.Null);
            Assert.That(result.Repair.ConditionRestored, Is.EqualTo(30));
            Assert.That(result.Repair.TargetCategory, Is.EqualTo("tool"));
        }

        [Test]
        public void CraftingWithoutSkillStillRollsFailure()
        {
            // Forge recipes have no skill: skill level counts as 0, so nails at
            // difficulty 1 fail 20% of the time for an unskilled smith.
            RecipeDefinition nails = _recipes.Get(new RecipeId("recipe_forge_nails"));
            int failures = 0;
            var rng = new SimRng(7);
            for (int i = 0; i < 100; i++)
            {
                var inventory = StockedInventory(nails);
                CookResult result = RecipeExecution.Cook(nails, Doran, new SkillStore(), inventory, Day, rng);
                if (result.Outcome == CookOutcome.Failure) failures++;
            }
            Assert.That(failures, Is.InRange(10, 30), "Expected ~20 failures, got " + failures + ".");
        }

        [Test]
        public void DeterministicSameSeedSameOutcome()
        {
            RecipeDefinition stew = _recipes.Get(new RecipeId("recipe_campfire_stew"));
            CookResult first = CookOnce(stew, 1, seed: 42).Result;
            CookResult second = CookOnce(stew, 1, seed: 42).Result;
            Assert.That(second.Outcome, Is.EqualTo(first.Outcome));
            Assert.That(second.Quality, Is.EqualTo(first.Quality));
            Assert.That(second.PracticePoints, Is.EqualTo(first.PracticePoints));
        }

        private int CountSuccesses(RecipeDefinition recipe, int level, ulong seed, int trials)
        {
            int successes = 0;
            var rng = new SimRng(seed);
            for (int i = 0; i < trials; i++)
            {
                var attempt = CookOnce(recipe, level, rng);
                if (attempt.Result.Outcome == CookOutcome.Success) successes++;
            }
            return successes;
        }

        private CookAttempt CookOnce(RecipeDefinition recipe, int level, SimRng rng)
        {
            var skills = recipe.Skill.HasValue ? SkillsAt(level) : new SkillStore();
            var inventory = StockedInventory(recipe);
            NpcId actor = ActorFor(recipe);
            CookResult result = RecipeExecution.Cook(recipe, actor, skills, inventory, Day, rng);
            return new CookAttempt(result, skills, inventory);
        }

        private CookAttempt CookOnce(RecipeDefinition recipe, int level, ulong seed)
        {
            return CookOnce(recipe, level, new SimRng(seed));
        }

        private CookResult CookUntilSuccess(RecipeDefinition recipe, int level, NpcId? actor = null)
        {
            return CookUntilSuccessWithState(recipe, level, actor).Result;
        }

        private CookAttempt CookUntilSuccessWithState(RecipeDefinition recipe, int level, NpcId? actor = null)
        {
            for (ulong seed = 1; seed <= 10000; seed++)
            {
                var skills = recipe.Skill.HasValue ? SkillsAt(level) : new SkillStore();
                var inventory = StockedInventory(recipe);
                NpcId who = actor ?? ActorFor(recipe);
                CookResult result = RecipeExecution.Cook(recipe, who, skills, inventory, Day, new SimRng(seed));
                if (result.Outcome == CookOutcome.Success || result.Outcome == CookOutcome.Fermenting)
                    return new CookAttempt(result, skills, inventory);
            }
            throw new InvalidOperationException("No successful cook found in 10000 seeds for " + recipe.Id.Value + ".");
        }

        private static NpcId ActorFor(RecipeDefinition recipe)
        {
            // Default to the permitting NPC so permission gates pass; callers that
            // test permission override the actor explicitly.
            return recipe.Permission != null ? recipe.Permission.Npc : Player;
        }

        private static SkillStore SkillsAt(int level)
        {
            var store = new SkillStore();
            store.Restore(new[] { SkillState.Restore(Cooking, level, 0, 0, -1) });
            return store;
        }

        private Inventory StockedInventory(RecipeDefinition recipe)
        {
            var inventory = new Inventory(_items);
            foreach (RecipeIngredient ingredient in recipe.Inputs)
                inventory.Add(ingredient.Item, ingredient.Count);
            return inventory;
        }

        private sealed class CookAttempt
        {
            public CookAttempt(CookResult result, SkillStore skills, Inventory inventory)
            {
                Result = result;
                Skills = skills;
                Inventory = inventory;
            }

            public CookResult Result { get; }
            public SkillStore Skills { get; }
            public Inventory Inventory { get; }
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }
    }
}
