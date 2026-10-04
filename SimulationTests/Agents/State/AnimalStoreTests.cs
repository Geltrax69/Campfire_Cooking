using System;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>Exercises AnimalStore queries, removal, and save/load capture.</summary>
    public sealed class AnimalStoreTests
    {
        private static readonly SpeciesId Chicken = new SpeciesId("species_chicken");
        private static readonly SpeciesId Wolf = new SpeciesId("species_wolf");
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly LocationId ForestEdge = new LocationId("loc_forest_edge");

        private static AnimalState Make(string id, SpeciesId species, LocationId location,
            int trust = 0, ActorId? owner = null, AnimalAge age = AnimalAge.Adult, int health = 100) =>
            AnimalState.Create(new AnimalId(id), species, location, trust, owner, age, health);

        private static AnimalStore PopulatedStore()
        {
            var store = new AnimalStore();
            store.Add(Make("animal_chicken_001", Chicken, Farm, trust: 25));
            store.Add(Make("animal_chicken_002", Chicken, Farm, trust: 0, age: AnimalAge.Young));
            store.Add(Make("animal_chicken_003", Chicken, ForestEdge, trust: 30));
            store.Add(Make("animal_wolf_001", Wolf, ForestEdge, trust: 0, owner: ActorId.ForNpc(new NpcId("npc_ralf"))));
            return store;
        }

        [Test]
        public void AnimalStoreQueries()
        {
            var store = PopulatedStore();

            Assert.That(store.Count, Is.EqualTo(4));
            Assert.That(store.PopulationCount(Chicken), Is.EqualTo(3));
            Assert.That(store.PopulationCount(Wolf), Is.EqualTo(1));
            Assert.That(store.PopulationCount(new SpeciesId("species_deer")), Is.EqualTo(0));

            var chickens = store.GetBySpecies(Chicken);
            Assert.That(chickens.Select(a => a.Id.Value),
                Is.EqualTo(new[] { "animal_chicken_001", "animal_chicken_002", "animal_chicken_003" }));
            var wolves = store.GetBySpecies(Wolf);
            Assert.That(wolves.Select(a => a.Id.Value), Is.EqualTo(new[] { "animal_wolf_001" }));

            var atFarm = store.GetByLocation(Farm);
            Assert.That(atFarm.Select(a => a.Id.Value),
                Is.EqualTo(new[] { "animal_chicken_001", "animal_chicken_002" }));
            var atForest = store.GetByLocation(ForestEdge);
            Assert.That(atForest.Select(a => a.Id.Value),
                Is.EqualTo(new[] { "animal_chicken_003", "animal_wolf_001" }));

            var found = store.Get(new AnimalId("animal_wolf_001"));
            Assert.That((found.Species, found.Location, found.Owner), Is.EqualTo(
                (Wolf, ForestEdge, (ActorId?)ActorId.ForNpc(new NpcId("npc_ralf")))));
            Assert.Throws<ArgumentException>(() => store.Get(new AnimalId("animal_wolf_999")));
            Assert.Throws<ArgumentException>(() => store.Get(default));
        }

        [Test]
        public void AddRejectsDuplicatesAndRemoveHandlesDeath()
        {
            var store = PopulatedStore();
            var duplicate = Make("animal_chicken_001", Chicken, Farm);
            Assert.Throws<ArgumentException>(() => store.Add(duplicate));
            Assert.That(store.Count, Is.EqualTo(4));

            Assert.That(store.Remove(new AnimalId("animal_wolf_001")), Is.True);
            Assert.That(store.Count, Is.EqualTo(3));
            Assert.That(store.PopulationCount(Wolf), Is.EqualTo(0));
            Assert.That(store.Remove(new AnimalId("animal_wolf_001")), Is.False);
            Assert.That(store.Remove(default), Is.False);
        }

        [Test]
        public void CaptureRestoreRoundTrip()
        {
            var store = PopulatedStore();
            store.Get(new AnimalId("animal_chicken_001")).AdjustTrust(-5);   // 20
            store.Get(new AnimalId("animal_chicken_002")).AdjustHealth(-30);  // 70
            var snapshot = store.Capture();

            var restored = new AnimalStore();
            restored.Restore(snapshot);

            Assert.That(restored.Count, Is.EqualTo(4));
            foreach (var animal in snapshot)
            {
                var copy = restored.Get(animal.Id);
                Assert.That((copy.Species, copy.Location, copy.Trust, copy.Owner, copy.Age, copy.Health),
                    Is.EqualTo((animal.Species, animal.Location, animal.Trust, animal.Owner, animal.Age, animal.Health)),
                    "Animal " + animal.Id + " must survive the round trip unchanged.");
            }
            // Capture is in deterministic AnimalId order.
            Assert.That(restored.Capture().Select(a => a.Id.Value), Is.EqualTo(
                snapshot.Select(a => a.Id.Value)));
        }

        [Test]
        public void RestoreRejectsBadSnapshotsWithoutChangingTheStore()
        {
            var store = PopulatedStore();
            int before = store.Count;

            var withNull = PopulatedStore().Capture().ToList();
            withNull.Add(null);
            Assert.Throws<ArgumentNullException>(() => store.Restore(withNull));

            var duplicates = PopulatedStore().Capture().ToList();
            duplicates.Add(duplicates[0]);
            Assert.Throws<ArgumentException>(() => store.Restore(duplicates));

            Assert.Throws<ArgumentNullException>(() => store.Restore(null));
            Assert.That(store.Count, Is.EqualTo(before));
        }
    }
}
