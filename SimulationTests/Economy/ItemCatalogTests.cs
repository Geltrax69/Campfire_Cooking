using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Checks catalog validation, immutable metadata and approved Phase 1 content.</summary>
    public sealed class ItemCatalogTests
    {
        internal static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        internal static readonly ItemTypeId Bread = new ItemTypeId("item_bread_rye");
        internal static ItemDefinition Define(ItemTypeId id) => new ItemDefinition(id, id.Value, "food", 3, 1);
        internal static ItemCatalog TinyCatalog() => new ItemCatalog(new[] { Define(Bread), Define(Apple) });

        [Test]
        public void ApprovedPhaseOneItemsPreserveValuesAndEffectsWithoutLoadingLaterPhases()
        {
            DirectoryInfo root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/items/items.json"))) root = root.Parent;
            Assert.That(root, Is.Not.Null, "Find repository content above test output.");
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "Content/items/items.json")));
            var selected = json.RootElement.GetProperty("items").EnumerateArray()
                .Where(item => item.GetProperty("phase").GetInt32() == 1).ToArray();
            var catalog = new ItemCatalog(selected.Select(item => new ItemDefinition(
                new ItemTypeId(item.GetProperty("id").GetString()), item.GetProperty("name").GetString(),
                item.GetProperty("category").GetString(), item.GetProperty("baseValue").GetInt32(),
                item.GetProperty("phase").GetInt32(), Effect(item, "hunger"), Effect(item, "health"), Effect(item, "social"))));
            Assert.That(catalog.Items.Count, Is.EqualTo(14));
            Assert.That(catalog.Items.Select(item => item.Id.Value), Is.EqualTo(new[] {
                "item_ale", "item_apple", "item_axe", "item_bread_barley", "item_bread_rye", "item_egg", "item_firewood",
                "item_fish", "item_flour", "item_grain", "item_herb_bundle", "item_knife", "item_remedy", "item_stew" }));
            foreach (var item in selected)
            {
                var actual = catalog[new ItemTypeId(item.GetProperty("id").GetString())];
                Assert.That((actual.Name, actual.Category, actual.BaseValue, actual.Phase,
                    actual.HungerEffect, actual.HealthEffect, actual.SocialEffect), Is.EqualTo((
                    item.GetProperty("name").GetString(), item.GetProperty("category").GetString(),
                    item.GetProperty("baseValue").GetInt32(), 1, Effect(item, "hunger"), Effect(item, "health"), Effect(item, "social"))));
            }
            Assert.That((catalog[Apple].BaseValue, catalog[Apple].HungerEffect, catalog[Apple].HealthEffect), Is.EqualTo((3, -15, 1)));
            Assert.That((catalog[Bread].BaseValue, catalog[Bread].HungerEffect, catalog[Bread].SocialEffect), Is.EqualTo((3, (int?)-30, (int?)null)));
            var ale = catalog[new ItemTypeId("item_ale")];
            Assert.That((ale.BaseValue, ale.HungerEffect, ale.SocialEffect), Is.EqualTo((2, -5, 5)));
            Assert.Throws<ArgumentException>(() => { _ = catalog[new ItemTypeId("item_pear")]; });
            Assert.Throws<ArgumentException>(() => { _ = catalog[new ItemTypeId("item_coin")]; });
        }

        [Test]
        public void CatalogCopiesInputsAndExposesOrdinalReadOnlyDefinitions()
        {
            var upper = Define(new ItemTypeId("item_Z"));
            var definitions = new[] { Define(Bread), Define(Apple), upper };
            var catalog = new ItemCatalog(definitions);
            definitions[0] = Define(new ItemTypeId("replacement"));
            Assert.That(catalog.Items.Select(item => item.Id), Is.EqualTo(new[] { upper.Id, Apple, Bread }));
            Assert.That(catalog[Bread].Id, Is.EqualTo(Bread));
            Assert.That(catalog[Apple].HungerEffect, Is.Null);
            Assert.Throws<NotSupportedException>(() => ((IList<ItemDefinition>)catalog.Items).Clear());
            Assert.Throws<ArgumentException>(() => { _ = catalog[new ItemTypeId("ITEM_APPLE")]; });
            Assert.That(new ItemCatalog(Array.Empty<ItemDefinition>()).Items, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void DefinitionRejectsBlankNameAndCategory(string value)
        {
            Assert.Throws<ArgumentException>(() => new ItemDefinition(Apple, value, "food", 3, 1));
            Assert.Throws<ArgumentException>(() => new ItemDefinition(Apple, "Apple", value, 3, 1));
        }

        [Test]
        public void DefinitionsAndCatalogRejectInvalidInputs()
        {
            Assert.Throws<ArgumentException>(() => Define(default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemDefinition(Apple, "Apple", "food", -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemDefinition(Apple, "Apple", "food", 3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemDefinition(Apple, "Apple", "food", 3, -1));
            Assert.That(new ItemDefinition(Apple, "Apple", "food", 0, 1).BaseValue, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => new ItemCatalog(null));
            Assert.Throws<ArgumentException>(() => new ItemCatalog(new ItemDefinition[] { null }));
            Assert.Throws<ArgumentException>(() => new ItemCatalog(new[] { Define(Apple), Define(Apple) }));
            Assert.Throws<ArgumentException>(() => { _ = TinyCatalog()[default]; });
        }

        private static int? Effect(JsonElement item, string name) =>
            item.GetProperty("effects").TryGetProperty(name, out var effect) ? effect.GetInt32() : (int?)null;
    }
}
