using System;
using System.Collections.Generic;
using System.IO;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// The recipe catalog loads every approved recipe from Content/recipes/recipes.json:
    /// all 21 recipes, split 13 cooking / 8 crafting, with query helpers by ID, skill
    /// and category.
    /// </summary>
    [TestFixture]
    public sealed class RecipeCatalogTests
    {
        private RecipeCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            _catalog = RecipeCatalog.Load(RepositoryRoot());
        }

        [Test]
        public void LoadsAll21Recipes()
        {
            Assert.That(_catalog.Count, Is.EqualTo(21));
            Assert.That(_catalog.All.Count, Is.EqualTo(21));
        }

        [Test]
        public void CookingAndCraftingSplitIs13And8()
        {
            Assert.That(_catalog.GetByCategory(RecipeCategory.Cooking).Count, Is.EqualTo(13));
            Assert.That(_catalog.GetByCategory(RecipeCategory.Crafting).Count, Is.EqualTo(8));
        }

        [Test]
        public void GetBySkillCookingReturns13Recipes()
        {
            var cooking = _catalog.GetBySkill(new SkillId("skill_cooking"));
            Assert.That(cooking.Count, Is.EqualTo(13));
            foreach (RecipeDefinition recipe in cooking)
                Assert.That(recipe.Skill, Is.EqualTo(new SkillId("skill_cooking")));
        }

        [Test]
        public void ForgeRecipesHaveNoSkillGate()
        {
            // The forge and workbench recipes are open to anyone Doran or Tam trusts
            // with the tools; difficulty and permission are the real gates. The one
            // exception is blade-sharpening, which is skill-gated on swordsmanship.
            foreach (RecipeDefinition recipe in _catalog.GetByCategory(RecipeCategory.Crafting))
            {
                if (recipe.Id == new RecipeId("recipe_sharpen_blade"))
                {
                    Assert.That(recipe.Skill, Is.EqualTo(new SkillId("skill_swordsmanship")));
                    continue;
                }
                Assert.That(recipe.Skill.HasValue, Is.False, recipe.Id.Value);
            }
        }

        [Test]
        public void CampfireStewFieldsMatchApprovedContent()
        {
            RecipeDefinition stew = _catalog.Get(new RecipeId("recipe_campfire_stew"));
            Assert.That(stew.Name, Is.EqualTo("Campfire Stew"));
            Assert.That(stew.Category, Is.EqualTo(RecipeCategory.Cooking));
            Assert.That(stew.Skill, Is.EqualTo(new SkillId("skill_cooking")));
            Assert.That(stew.MinLevel, Is.EqualTo(1));
            Assert.That(stew.Difficulty, Is.EqualTo(1));
            Assert.That(stew.TimeMinutes, Is.EqualTo(45));
            Assert.That(stew.Tool, Is.EqualTo("campfire"));
            Assert.That(stew.Location, Is.EqualTo(new LocationId("loc_river_alder")));
            Assert.That(stew.Permission, Is.Null);
            Assert.That(stew.FermentDays.HasValue, Is.False);

            Assert.That(stew.Inputs.Count, Is.EqualTo(2));
            Assert.That(CountOf(stew, "item_fish"), Is.EqualTo(1));
            Assert.That(CountOf(stew, "item_firewood"), Is.EqualTo(1));

            Assert.That(stew.Outputs.Count, Is.EqualTo(1));
            Assert.That(stew.Outputs[0].Kind, Is.EqualTo(RecipeOutputKind.Item));
            Assert.That(stew.Outputs[0].Item, Is.EqualTo(new ItemTypeId("item_stew")));
            Assert.That(stew.Outputs[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void BakeBreadRequiresOdasPermission()
        {
            RecipeDefinition bread = _catalog.Get(new RecipeId("recipe_bake_bread_rye"));
            Assert.That(bread.Permission, Is.Not.Null);
            Assert.That(bread.Permission.Npc, Is.EqualTo(new NpcId("npc_oda_fenn")));
            Assert.That(bread.Permission.What, Is.EqualTo("use of the bakery oven"));
        }

        [Test]
        public void MasterStewHasQualityFloor80()
        {
            RecipeDefinition master = _catalog.Get(new RecipeId("recipe_master_stew"));
            Assert.That(master.QualityFloor, Is.EqualTo(80));
            Assert.That(master.Difficulty, Is.EqualTo(5));
            Assert.That(master.MinLevel, Is.EqualTo(5));
        }

        [Test]
        public void BrewAleFermentsFor3Days()
        {
            RecipeDefinition ale = _catalog.Get(new RecipeId("recipe_brew_ale"));
            Assert.That(ale.FermentDays, Is.EqualTo(3));
        }

        [Test]
        public void PatchToolIsARepairRecipe()
        {
            RecipeDefinition patch = _catalog.Get(new RecipeId("recipe_patch_tool"));
            Assert.That(patch.Outputs.Count, Is.EqualTo(1));
            Assert.That(patch.Outputs[0].Kind, Is.EqualTo(RecipeOutputKind.Repair));
            Assert.That(patch.Outputs[0].ConditionRestored, Is.EqualTo(30));
            Assert.That(patch.Outputs[0].TargetCategory, Is.EqualTo("tool"));
        }

        [Test]
        public void GetUnknownRecipeThrows()
        {
            Assert.Throws<KeyNotFoundException>(() => _catalog.Get(new RecipeId("recipe_no_such_dish")));
        }

        [Test]
        public void CatalogIterationIsDeterministic()
        {
            // Two loads from the same content must expose recipes in the same order.
            RecipeCatalog again = RecipeCatalog.Load(RepositoryRoot());
            for (int i = 0; i < _catalog.All.Count; i++)
                Assert.That(again.All[i].Id, Is.EqualTo(_catalog.All[i].Id));
        }

        private static int CountOf(RecipeDefinition recipe, string itemId)
        {
            var want = new ItemTypeId(itemId);
            foreach (RecipeIngredient ingredient in recipe.Inputs)
                if (ingredient.Item == want) return ingredient.Count;
            return 0;
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
