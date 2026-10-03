using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Core.Primitives
{
    /// <summary>Verifies content IDs preserve type, ordinal identity and safe defaults.</summary>
    public class IdTests
    {
        [Test]
        [SetCulture("tr-TR")]
        public void IdsHaveOrdinalValueSemanticsAndStableHashes()
        {
            CheckId(value => new NpcId(value), id => id.Value, id => id.IsValid);
            CheckId(value => new LocationId(value), id => id.Value, id => id.IsValid);
            CheckId(value => new ItemTypeId(value), id => id.Value, id => id.IsValid);
        }

        private static void CheckId<T>(Func<string, T> create, Func<T, string> value, Func<T, bool> valid)
            where T : struct, IEquatable<T>, IComparable<T>
        {
            T first = create("hello"), same = create("hello"), other = create("Hello");
            Assert.That(value(first), Is.EqualTo("hello"));
            Assert.That(valid(first), Is.True);
            Assert.That(first.ToString(), Is.EqualTo("hello"));
            Assert.That(first.Equals(same), Is.True);
            Assert.That(first.Equals((object)same), Is.True);
            Assert.That(first.Equals(other), Is.False);
            Assert.That(first.Equals(null), Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(0x4f9f2cab)); // FNV-1a reference for "hello".
            Assert.That(same.GetHashCode(), Is.EqualTo(first.GetHashCode()));
            var dictionary = new Dictionary<T, int> { [first] = 7 };
            Assert.That(dictionary[same], Is.EqualTo(7));
            var sorted = new[] { create("ä"), create("a"), create("Z") };
            Array.Sort(sorted);
            Assert.That(Array.ConvertAll(sorted, id => value(id)), Is.EqualTo(new[] { "Z", "a", "ä" }));
            Assert.That(first.CompareTo(same), Is.Zero);
            Assert.That(valid(default), Is.False);
            Assert.That(default(T).Equals(default(T)), Is.True);
            Assert.That(default(T).Equals(first), Is.False);
            Assert.That(default(T).CompareTo(first), Is.LessThan(0));
            Assert.That(default(T).GetHashCode(), Is.Zero);
            Assert.That(default(T).ToString(), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t\r\n")]
        public void RejectsInvalidContentIds(string value)
        {
            Assert.That(() => new NpcId(value), Throws.ArgumentException);
            Assert.That(() => new LocationId(value), Throws.ArgumentException);
            Assert.That(() => new ItemTypeId(value), Throws.ArgumentException);
        }

        [Test]
        public void IdTypesStayDistinctAndOperatorsUseOrdinalValues()
        {
            Assert.That(new NpcId("same").Equals((object)new LocationId("same")), Is.False);
            Assert.That(new LocationId("same").Equals((object)new ItemTypeId("same")), Is.False);
            Assert.That(new ItemTypeId("same").Equals((object)new NpcId("same")), Is.False);
            Assert.That(new NpcId("a") == new NpcId("a") && new NpcId("a") != new NpcId("A"), Is.True);
            Assert.That(new LocationId("a") == new LocationId("a") && new LocationId("a") != new LocationId("A"), Is.True);
            Assert.That(new ItemTypeId("a") == new ItemTypeId("a") && new ItemTypeId("a") != new ItemTypeId("A"), Is.True);
        }
    }
}
