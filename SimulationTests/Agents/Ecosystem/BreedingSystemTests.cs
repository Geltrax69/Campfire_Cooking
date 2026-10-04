using System;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents.Ecosystem
{
    /// <summary>
    /// Spring breeding adds young animals for every species; population caps hold
    /// (deer ~40, wolves 5-7) and excess young are removed with an AnimalCulled
    /// truth event.
    /// </summary>
    public sealed class BreedingSystemTests
    {
        private static (World world, WorldState state) FullVillage(ulong seed, long startDay)
        {
            var state = EcosystemWorlds.CreateWorld(seed, startDay);
            EcosystemWorlds.AddChickens(state);
            EcosystemWorlds.AddPigs(state);
            EcosystemWorlds.AddDeer(state, 40);
            EcosystemWorlds.AddWolves(state, 6);
            EcosystemWorlds.AddBramblebacks(state);
            var world = new World(state);
            world.RegisterSystem(new BreedingSystem(new[] { new BreedingConfiguration("test") }));
            state.RestoreBreeding(new BreedingState(initialized: true,
                lastBreedingDay: startDay - 1));
            return (world, state);
        }

        [Test]
        public void SpringBreedingIncreasesPopulations()
        {
            var (world, state) = FullVillage(seed: 5, EcosystemWorlds.FirstSpringDay);
            int chickensBefore = state.Animals.PopulationCount(EcosystemWorlds.Chicken);
            int pigsBefore = state.Animals.PopulationCount(EcosystemWorlds.Pig);

            EcosystemWorlds.TickDays(world, 1);

            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Chicken),
                Is.GreaterThan(chickensBefore), "Hens hatched chicks.");
            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Pig),
                Is.GreaterThan(pigsBefore), "Sows farrowed piglets.");
            Assert.That(EcosystemWorlds.CountEvents(state, WorldEventType.AnimalBirth),
                Is.GreaterThan(0), "Births are recorded as truth.");
        }

        [Test]
        public void NewYoungHaveCorrectTrustAndAge()
        {
            // Deer below their cap so the fawns survive the cull.
            var state = EcosystemWorlds.CreateWorld(5, EcosystemWorlds.FirstSpringDay);
            EcosystemWorlds.AddChickens(state);
            EcosystemWorlds.AddDeer(state, 30);
            EcosystemWorlds.AddWolves(state, 6);
            var world = new World(state);
            world.RegisterSystem(new BreedingSystem(new[] { new BreedingConfiguration("test") }));
            state.RestoreBreeding(new BreedingState(initialized: true,
                lastBreedingDay: EcosystemWorlds.FirstSpringDay - 1));
            EcosystemWorlds.TickDays(world, 1);

            var chicks = state.Animals.GetBySpecies(EcosystemWorlds.Chicken)
                .Where(a => a.Age == AnimalAge.Young).ToList();
            Assert.That(chicks, Is.Not.Empty);
            Assert.That(chicks.All(c => c.Trust == 20 && c.Owner == null), Is.True,
                "Domestic young start at trust 20, unbonded.");

            var fawns = state.Animals.GetBySpecies(EcosystemWorlds.Deer)
                .Where(a => a.Age == AnimalAge.Young).ToList();
            Assert.That(fawns, Is.Not.Empty, "Below the cap, fawns survive.");
            Assert.That(fawns.All(f => f.Trust == 0 && f.Owner == null), Is.True,
                "Wild young start at trust 0, unbonded.");
        }

        [Test]
        public void PopulationCapsEnforced()
        {
            var (world, state) = FullVillage(seed: 9, EcosystemWorlds.FirstSpringDay);
            EcosystemWorlds.TickDays(world, 1);

            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Deer),
                Is.LessThanOrEqualTo(40), "Deer never exceed ~40.");
            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Wolf),
                Is.LessThanOrEqualTo(7), "The pack never exceeds 5-7.");
            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Chicken),
                Is.LessThanOrEqualTo(30));
            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Pig),
                Is.LessThanOrEqualTo(16));
            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Brambleback),
                Is.LessThanOrEqualTo(35));
            Assert.That(EcosystemWorlds.CountEvents(state, WorldEventType.AnimalCulled),
                Is.GreaterThan(0), "Excess young are removed with a truth event.");
        }

        [Test]
        public void BreedingRunsOncePerSpring()
        {
            var (world, state) = FullVillage(seed: 5, EcosystemWorlds.FirstSpringDay);
            EcosystemWorlds.TickDays(world, 1);
            int birthsAfterFirstDay = EcosystemWorlds.CountEvents(state, WorldEventType.AnimalBirth);

            EcosystemWorlds.TickDays(world, 89);
            Assert.That(EcosystemWorlds.CountEvents(state, WorldEventType.AnimalBirth),
                Is.EqualTo(birthsAfterFirstDay), "Breeding happens once per spring.");
        }

        [Test]
        public void NoBreedingOutsideSpring()
        {
            var (world, state) = FullVillage(seed: 5, startDay: 1); // autumn
            EcosystemWorlds.TickDays(world, 90);

            Assert.That(EcosystemWorlds.CountEvents(state, WorldEventType.AnimalBirth), Is.Zero);
            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Chicken), Is.EqualTo(20));
            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Deer), Is.EqualTo(40));
        }

        [Test]
        public void BreedingIsDeterministic()
        {
            var (firstWorld, firstState) = FullVillage(21, EcosystemWorlds.FirstSpringDay);
            EcosystemWorlds.TickDays(firstWorld, 5);
            var (secondWorld, secondState) = FullVillage(21, EcosystemWorlds.FirstSpringDay);
            EcosystemWorlds.TickDays(secondWorld, 5);

            foreach (var species in new[]
                { EcosystemWorlds.Chicken, EcosystemWorlds.Pig, EcosystemWorlds.Deer,
                  EcosystemWorlds.Wolf, EcosystemWorlds.Brambleback })
                Assert.That(secondState.Animals.PopulationCount(species),
                    Is.EqualTo(firstState.Animals.PopulationCount(species)),
                    "Same seed gives the same " + species.Value + " population.");
            Assert.That(secondState.Animals.Animals.Select(a => a.Id.Value).ToList(),
                Is.EqualTo(firstState.Animals.Animals.Select(a => a.Id.Value).ToList()));
        }

        [Test]
        public void ConfigurationValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new BreedingSystem(null));
            Assert.Throws<ArgumentException>(() => new BreedingSystem(new BreedingConfiguration[] { null }));
            Assert.Throws<ArgumentException>(() => new BreedingSystem(new[]
            {
                new BreedingConfiguration("dup"), new BreedingConfiguration("dup")
            }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BreedingConfiguration("x", deerCap: 0));
        }
    }
}
