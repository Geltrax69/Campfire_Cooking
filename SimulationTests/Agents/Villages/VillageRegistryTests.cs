using System;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents.Villages
{
    /// <summary>
    /// Proves neighboring villages register as level-of-detail entities (P6-01):
    /// Millbrook is always LOD Full; abstract neighbors carry population, wealth,
    /// food supply and mood; the registry round-trips through save/load snapshots.
    /// </summary>
    public sealed class VillageRegistryTests
    {
        private static VillageRegistry PopulatedRegistry()
        {
            var registry = new VillageRegistry();
            foreach (var village in VillageFactory.CreateInitialVillages().Capture().Villages)
                registry.Register(village);
            return registry;
        }

        [Test]
        public void FactoryCreatesMillbrookAsFullAndTwoAbstractNeighbors()
        {
            var registry = PopulatedRegistry();

            Assert.That(registry.Count, Is.EqualTo(3));
            var millbrook = registry[VillageFactory.Millbrook];
            Assert.That(millbrook.Lod, Is.EqualTo(VillageLod.Full));
            Assert.That(millbrook.Name, Is.EqualTo("Millbrook"));

            var kingsRest = registry[new VillageId("village_kings_rest")];
            var oakhollow = registry[new VillageId("village_oakhollow")];
            Assert.That(kingsRest.Lod, Is.EqualTo(VillageLod.Abstract));
            Assert.That(oakhollow.Lod, Is.EqualTo(VillageLod.Abstract));
            Assert.That(kingsRest.TravelDaysFromMillbrook, Is.EqualTo(2));
            Assert.That(oakhollow.TravelDaysFromMillbrook, Is.EqualTo(1));
        }

        [Test]
        public void GetByLodReturnsVillagesInOrdinalIdOrder()
        {
            var registry = PopulatedRegistry();

            var full = registry.GetByLod(VillageLod.Full);
            Assert.That(full.Select(v => v.Id.Value), Is.EqualTo(new[] { "village_millbrook" }));

            var abstractVillages = registry.GetByLod(VillageLod.Abstract);
            Assert.That(abstractVillages.Select(v => v.Id.Value), Is.EqualTo(
                new[] { "village_kings_rest", "village_oakhollow" }));
        }

        [Test]
        public void GetAllReturnsEveryVillageInOrdinalIdOrder()
        {
            var registry = PopulatedRegistry();
            Assert.That(registry.GetAll().Select(v => v.Id.Value), Is.EqualTo(
                new[] { "village_kings_rest", "village_millbrook", "village_oakhollow" }));
        }

        [Test]
        public void GetUnknownVillageThrows()
        {
            var registry = PopulatedRegistry();
            Assert.That(() => registry[new VillageId("village_nowhere")],
                Throws.ArgumentException);
        }

        [Test]
        public void RegisterRejectsNullAndDuplicates()
        {
            var registry = new VillageRegistry();
            Assert.That(() => registry.Register(null), Throws.ArgumentNullException);

            var village = new AbstractVillageState(
                new VillageId("village_test"), "Test", VillageLod.Abstract,
                new LocationId("loc_square"), 1,
                population: 10, wealthCopper: 100, foodSupply: 50, mood: 50);
            registry.Register(village);
            Assert.That(() => registry.Register(village), Throws.ArgumentException);
        }

        [Test]
        public void VillageStateRejectsInvalidValues()
        {
            var id = new VillageId("village_test");
            var square = new LocationId("loc_square");
            Assert.That(() => new AbstractVillageState(
                default, "Test", VillageLod.Abstract, square, 1, 10, 100, 50, 50),
                Throws.ArgumentException);
            Assert.That(() => new AbstractVillageState(
                id, "", VillageLod.Abstract, square, 1, 10, 100, 50, 50),
                Throws.ArgumentException);
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbstractVillageState(
                id, "Test", VillageLod.Abstract, square, -1, 10, 100, 50, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbstractVillageState(
                id, "Test", VillageLod.Abstract, square, 1, -1, 100, 50, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbstractVillageState(
                id, "Test", VillageLod.Abstract, square, 1, 10, -5, 50, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbstractVillageState(
                id, "Test", VillageLod.Abstract, square, 1, 10, 100, 101, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbstractVillageState(
                id, "Test", VillageLod.Abstract, square, 1, 10, 100, 50, -1));
        }

        [Test]
        public void CaptureRestoreRoundTrip()
        {
            var registry = PopulatedRegistry();
            var kingsRest = registry[new VillageId("village_kings_rest")];
            kingsRest.AdjustPopulation(-5);
            kingsRest.AdjustWealthCopper(250);
            kingsRest.SetFoodSupply(42);
            kingsRest.SetMood(61);
            var snapshot = registry.Capture();

            var restored = new VillageRegistry();
            restored.Restore(snapshot);

            Assert.That(restored.Count, Is.EqualTo(3));
            Assert.That(restored.Capture().LastDriftDay, Is.EqualTo(snapshot.LastDriftDay));
            foreach (var village in snapshot.Villages)
            {
                var copy = restored[village.Id];
                Assert.That(
                    (copy.Name, copy.Lod, copy.TravelDaysFromMillbrook, copy.Population,
                        copy.WealthCopper, copy.FoodSupply, copy.Mood),
                    Is.EqualTo(
                    (village.Name, village.Lod, village.TravelDaysFromMillbrook, village.Population,
                        village.WealthCopper, village.FoodSupply, village.Mood)),
                    "Village " + village.Id + " must survive the round trip unchanged.");
            }
            // Capture is in deterministic VillageId order.
            Assert.That(restored.Capture().Villages.Select(v => v.Id.Value), Is.EqualTo(
                snapshot.Villages.Select(v => v.Id.Value)));
        }

        [Test]
        public void RestoreRejectsBadSnapshotsWithoutChangingTheRegistry()
        {
            var registry = PopulatedRegistry();
            int before = registry.Count;

            Assert.That(() => registry.Restore(null), Throws.ArgumentNullException);

            var withNull = PopulatedRegistry().Capture().Villages.ToList();
            withNull.Add(null);
            Assert.That(() => registry.Restore(new VillageRegistrySnapshot(withNull, 0)),
                Throws.ArgumentException);

            var duplicated = PopulatedRegistry().Capture().Villages.ToList();
            duplicated.Add(duplicated[0]);
            Assert.That(() => registry.Restore(new VillageRegistrySnapshot(duplicated, 0)),
                Throws.ArgumentException);

            Assert.That(registry.Count, Is.EqualTo(before));
            Assert.That(registry.GetAll().Select(v => v.Id.Value), Is.EqualTo(
                new[] { "village_kings_rest", "village_millbrook", "village_oakhollow" }));
        }

        [Test]
        public void AdjustmentsClampAtBounds()
        {
            var village = new AbstractVillageState(
                new VillageId("village_test"), "Test", VillageLod.Abstract,
                new LocationId("loc_square"), 1,
                population: 2, wealthCopper: 5, foodSupply: 50, mood: 50);

            village.AdjustPopulation(-10);
            Assert.That(village.Population, Is.EqualTo(0));
            village.AdjustWealthCopper(-100);
            Assert.That(village.WealthCopper, Is.EqualTo(0));
            village.SetFoodSupply(150);
            Assert.That(village.FoodSupply, Is.EqualTo(100));
            village.SetFoodSupply(-20);
            Assert.That(village.FoodSupply, Is.EqualTo(0));
            village.SetMood(120);
            Assert.That(village.Mood, Is.EqualTo(100));
        }
    }
}
