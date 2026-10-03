using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;
using static LivingWorld.Simulation.Tests.Economy.ItemCatalogTests;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Checks nonnegative bulk counts and atomic, count-conserving transfers.</summary>
    public sealed class InventoryTests
    {
        [Test]
        public void AddAndRemoveAggregateBeyondUiStackSizeAndNeverPartiallyRemove()
        {
            var inventory = new Inventory(TinyCatalog());
            Assert.That(inventory.Count(Apple), Is.Zero);
            Assert.That(inventory.TryRemove(Apple, 1), Is.False);
            inventory.Add(Apple, 50);
            inventory.Add(Apple, 20);
            inventory.Add(Bread, 7);
            Assert.That(inventory.Count(Apple), Is.EqualTo(70));
            Assert.That(inventory.TryRemove(Apple, 71), Is.False);
            Assert.That(inventory.Count(Apple), Is.EqualTo(70));
            Assert.That(inventory.TryRemove(Apple, 6), Is.True);
            Assert.That(inventory.Count(Apple), Is.EqualTo(64));
            Assert.That(inventory.TryRemove(Apple, 64), Is.True);
            Assert.That(inventory.Count(Apple), Is.Zero);
            Assert.That(inventory.Contents.Select(pair => pair.Key), Is.EqualTo(new[] { Bread }));
            Assert.That(inventory.Count(Bread), Is.EqualTo(7));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void MutationsRejectNonpositiveQuantitiesWithoutChanges(int quantity)
        {
            var source = new Inventory(TinyCatalog());
            var destination = new Inventory(TinyCatalog());
            source.Add(Apple, 20);
            Assert.Throws<ArgumentOutOfRangeException>(() => source.Add(Apple, quantity));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.TryRemove(Apple, quantity));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.TransferTo(destination, Apple, quantity));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.TransferTo(source, Apple, quantity));
            Assert.That(source.Count(Apple), Is.EqualTo(20));
            Assert.That(destination.Contents, Is.Empty);
        }

        [Test]
        public void InvalidIdsAndNullInputsFailWithoutMutation()
        {
            Assert.Throws<ArgumentNullException>(() => new Inventory(null));
            var source = new Inventory(TinyCatalog());
            var destination = new Inventory(TinyCatalog());
            source.Add(Apple, 20);
            foreach (var id in new[] { default(ItemTypeId), new ItemTypeId("unknown") })
            {
                Assert.Throws<ArgumentException>(() => source.Count(id));
                Assert.Throws<ArgumentException>(() => source.Add(id, 1));
                Assert.Throws<ArgumentException>(() => source.TryRemove(id, 1));
                Assert.Throws<ArgumentException>(() => source.TransferTo(destination, id, 1));
                Assert.Throws<ArgumentException>(() => source.TransferTo(source, id, 1));
            }
            Assert.Throws<ArgumentNullException>(() => source.TransferTo(null, Apple, 1));
            Assert.That(source.Count(Apple), Is.EqualTo(20));
            Assert.That(destination.Contents, Is.Empty);
        }

        [Test]
        public void AddAndDestinationOverflowLeaveBothCountsUnchanged()
        {
            var source = new Inventory(TinyCatalog());
            var destination = new Inventory(TinyCatalog());
            source.Add(Apple, 2);
            destination.Add(Apple, int.MaxValue - 1);
            Assert.Throws<OverflowException>(() => destination.Add(Apple, 2));
            Assert.Throws<OverflowException>(() => source.TransferTo(destination, Apple, 2));
            Assert.That((source.Count(Apple), destination.Count(Apple)), Is.EqualTo((2, int.MaxValue - 1)));
            Assert.That(source.TransferTo(destination, Apple, 1), Is.True);
            Assert.That((source.Count(Apple), destination.Count(Apple)), Is.EqualTo((1, int.MaxValue)));
            Assert.Throws<OverflowException>(() => source.TransferTo(destination, Apple, 1));
            Assert.That((source.Count(Apple), destination.Count(Apple)), Is.EqualTo((1, int.MaxValue)));
        }

        [Test]
        public void TransfersConserveCountsAndRejectInsufficientStockOrUnsupportedDestination()
        {
            var source = new Inventory(TinyCatalog());
            var destination = new Inventory(TinyCatalog());
            source.Add(Apple, 20);
            destination.Add(Bread, 3);
            Assert.That(source.TransferTo(destination, Apple, 21), Is.False);
            Assert.That((source.Count(Apple), destination.Count(Apple)), Is.EqualTo((20, 0)));
            var unsupported = new Inventory(new ItemCatalog(new[] { Define(Bread) }));
            Assert.Throws<ArgumentException>(() => source.TransferTo(unsupported, Apple, 1));
            Assert.That(source.Count(Apple), Is.EqualTo(20));
            Assert.That(unsupported.Contents, Is.Empty);
            foreach (int quantity in new[] { 6, 5, 7, 2 })
            {
                int before = destination.Count(Apple);
                Assert.That(source.TransferTo(destination, Apple, quantity), Is.True);
                Assert.That(destination.Count(Apple), Is.EqualTo(before + quantity));
                Assert.That(source.Count(Apple) + destination.Count(Apple), Is.EqualTo(20));
            }
            Assert.That(source.Contents, Is.Empty);
            Assert.That(destination.Count(Bread), Is.EqualTo(3));
            Assert.That(destination.TransferTo(source, Apple, 20), Is.True);
            Assert.That((source.Count(Apple), destination.Count(Apple)), Is.EqualTo((20, 0)));
        }

        [Test]
        public void SelfTransferIsValidatedNoOpEvenAtMaximumCount()
        {
            var inventory = new Inventory(TinyCatalog());
            Assert.That(inventory.TransferTo(inventory, Apple, 1), Is.False);
            inventory.Add(Apple, int.MaxValue);
            Assert.That(inventory.TransferTo(inventory, Apple, int.MaxValue), Is.True);
            Assert.That(inventory.Count(Apple), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void IndependentInventoriesExposeOrdinalReadOnlySnapshots()
        {
            var catalog = TinyCatalog();
            var first = new Inventory(catalog);
            var second = new Inventory(catalog);
            first.Add(Bread, 3);
            first.Add(Apple, 20);
            var snapshot = first.Contents;
            Assert.That(snapshot.Select(pair => (pair.Key, pair.Value)), Is.EqualTo(new[] { (Apple, 20), (Bread, 3) }));
            Assert.Throws<NotSupportedException>(() => ((IList<KeyValuePair<ItemTypeId, int>>)snapshot).Clear());
            first.TryRemove(Apple, 20);
            first.Add(Bread, 1);
            Assert.That(snapshot.Select(pair => pair.Value), Is.EqualTo(new[] { 20, 3 }));
            Assert.That(second.Contents, Is.Empty);
            Assert.That(second.Count(Apple), Is.Zero);
        }
    }
}
