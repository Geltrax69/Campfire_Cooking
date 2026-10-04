using System;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>Exercises per-animal state: trust/health bounds and ownership.</summary>
    public sealed class AnimalStateTests
    {
        private static AnimalState Chicken(string ordinal, int trust = 25, int health = 100) =>
            AnimalState.Create(new AnimalId("animal_chicken_" + ordinal),
                new SpeciesId("species_chicken"), new LocationId("loc_farm"), trust, null, AnimalAge.Adult, health);

        [Test]
        public void TrustBounds()
        {
            // Construction clamps into 0-100 rather than throwing: callers hand in
            // raw deltas and the state keeps the invariant.
            Assert.That(Chicken("001", trust: -10).Trust, Is.EqualTo(0));
            Assert.That(Chicken("001", trust: 150).Trust, Is.EqualTo(100));

            var animal = Chicken("001", trust: 50);
            animal.AdjustTrust(-1000);
            Assert.That(animal.Trust, Is.EqualTo(0));
            animal.AdjustTrust(1000);
            Assert.That(animal.Trust, Is.EqualTo(100));
            animal.AdjustTrust(-30);
            Assert.That(animal.Trust, Is.EqualTo(70));
        }

        [Test]
        public void HealthBounds()
        {
            Assert.That(Chicken("001", health: -5).Health, Is.EqualTo(0));
            Assert.That(Chicken("001", health: 200).Health, Is.EqualTo(100));

            var animal = Chicken("001", health: 100);
            animal.AdjustHealth(-40);
            Assert.That(animal.Health, Is.EqualTo(60));
            animal.AdjustHealth(-1000);
            Assert.That(animal.Health, Is.EqualTo(0));
            animal.AdjustHealth(1000);
            Assert.That(animal.Health, Is.EqualTo(100));
        }

        [Test]
        public void OwnerStartsNullAndCanBeBonded()
        {
            var animal = Chicken("001");
            Assert.That(animal.Owner, Is.Null);
            var owner = ActorId.ForNpc(new NpcId("npc_lida"));
            animal.SetOwner(owner);
            Assert.That(animal.Owner, Is.EqualTo((ActorId?)owner));
            animal.SetOwner(ActorId.Player);
            Assert.That(animal.Owner, Is.EqualTo((ActorId?)ActorId.Player),
                "The player is a valid bonded owner too (P4-03).");
            animal.SetOwner(null);
            Assert.That(animal.Owner, Is.Null);
        }

        [Test]
        public void ConstructionRejectsInvalidIds()
        {
            Assert.Throws<ArgumentException>(() => AnimalState.Create(default,
                new SpeciesId("species_chicken"), new LocationId("loc_farm"), 25, null, AnimalAge.Adult, 100));
            Assert.Throws<ArgumentException>(() => AnimalState.Create(new AnimalId("animal_chicken_001"),
                default, new LocationId("loc_farm"), 25, null, AnimalAge.Adult, 100));
            Assert.Throws<ArgumentException>(() => AnimalState.Create(new AnimalId("animal_chicken_001"),
                new SpeciesId("species_chicken"), default, 25, null, AnimalAge.Adult, 100));
        }
    }
}
