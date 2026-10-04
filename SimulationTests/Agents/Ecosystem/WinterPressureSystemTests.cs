using System;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents.Ecosystem
{
    /// <summary>
    /// In winter, deer range to the farm fields and wolves to the forest edge; the
    /// pack takes 2-4 livestock across the season on seeded incident days, and the
    /// deer at the farms cause a small recorded crop loss.
    /// </summary>
    public sealed class WinterPressureSystemTests
    {
        private static (World world, WorldState state) WinterVillage(ulong seed, long startDay)
        {
            var state = EcosystemWorlds.CreateWorld(seed, startDay);
            EcosystemWorlds.AddChickens(state);
            EcosystemWorlds.AddPigs(state);
            EcosystemWorlds.AddDeer(state, 40);
            EcosystemWorlds.AddWolves(state, 6);
            var world = new World(state);
            world.RegisterSystem(new WinterPressureSystem(
                new[] { new WinterPressureConfiguration("test") }));
            state.RestoreWinterPressure(new WinterPressureState(initialized: true,
                lastLossDay: startDay - 1));
            return (world, state);
        }

        [Test]
        public void WinterIncreasesLivestockLosses()
        {
            var (world, state) = WinterVillage(seed: 13, EcosystemWorlds.FirstWinterDay);
            int livestockBefore = state.Animals.PopulationCount(EcosystemWorlds.Chicken)
                + state.Animals.GetBySpecies(EcosystemWorlds.Pig)
                    .Count(p => p.Location != EcosystemWorlds.ForestEdge);

            EcosystemWorlds.TickDays(world, 90);

            int lost = livestockBefore
                - state.Animals.PopulationCount(EcosystemWorlds.Chicken)
                - state.Animals.GetBySpecies(EcosystemWorlds.Pig)
                    .Count(p => p.Location != EcosystemWorlds.ForestEdge);
            Assert.That(lost, Is.InRange(2, 4),
                "The pack takes 2-4 livestock per winter (WORLD.md, load-bearing).");
            Assert.That(state.Animals.GetBySpecies(EcosystemWorlds.Pig)
                    .Count(p => p.Location == EcosystemWorlds.ForestEdge),
                Is.EqualTo(6), "Wild boars are never livestock losses.");
            var killEvents = state.Events.Query(type: WorldEventType.Predation).ToList();
            Assert.That(killEvents.Count, Is.EqualTo(lost));
            Assert.That(killEvents.All(e => e.Copper > 0), Is.True,
                "Each loss records its economic value in copper.");
        }

        [Test]
        public void LivestockLossesAreDistributedAcrossWinter()
        {
            var (world, state) = WinterVillage(seed: 13, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.TickDays(world, 90);

            var days = state.Events.Query(type: WorldEventType.Predation)
                .Select(e => e.Time.Day).OrderBy(d => d).ToList();
            Assert.That(days.Count, Is.InRange(2, 4));
            Assert.That(days.Last() - days.First(), Is.GreaterThanOrEqualTo(20),
                "Incidents are spread across the season, not clustered.");
        }

        [Test]
        public void DeerRangeToFarmInWinter()
        {
            var (world, state) = WinterVillage(seed: 13, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.TickDays(world, 1);

            Assert.That(state.Animals.GetBySpecies(EcosystemWorlds.Deer)
                .All(d => d.Location == EcosystemWorlds.Farm), Is.True,
                "Deep snow pushes the deer to the farm fields.");
            Assert.That(state.Animals.GetBySpecies(EcosystemWorlds.Wolf)
                .All(w => w.Location == EcosystemWorlds.ForestEdge), Is.True,
                "The pack ranges to the forest edge.");
        }

        [Test]
        public void DeerReturnToForestInSpring()
        {
            var (world, state) = WinterVillage(seed: 13, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.TickDays(world, 90); // into the first spring day

            Assert.That(state.Animals.GetBySpecies(EcosystemWorlds.Deer)
                .All(d => d.Location == EcosystemWorlds.ForestEdge), Is.True,
                "When winter ends the deer drift back to the forest.");
        }

        [Test]
        public void CropDamageIsRecordedInWinter()
        {
            var (world, state) = WinterVillage(seed: 13, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.TickDays(world, 90);

            var damage = state.Events.Query(type: WorldEventType.CropDamage).ToList();
            Assert.That(damage.Count, Is.EqualTo(1), "One crop-loss truth event per winter.");
            Assert.That(damage[0].Copper, Is.GreaterThan(0));
            Assert.That(damage[0].Location, Is.EqualTo(EcosystemWorlds.Farm));
        }

        [Test]
        public void WinterPressureIsDeterministic()
        {
            var (firstWorld, firstState) = WinterVillage(77, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.TickDays(firstWorld, 90);
            var (secondWorld, secondState) = WinterVillage(77, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.TickDays(secondWorld, 90);

            Assert.That(
                secondState.Events.Query(type: WorldEventType.Predation)
                    .Select(e => e.Time.Day).ToList(),
                Is.EqualTo(firstState.Events.Query(type: WorldEventType.Predation)
                    .Select(e => e.Time.Day).ToList()));
            Assert.That(secondState.Animals.PopulationCount(EcosystemWorlds.Chicken),
                Is.EqualTo(firstState.Animals.PopulationCount(EcosystemWorlds.Chicken)));
        }

        [Test]
        public void ScarceDeerPushWolvesHarderOntoLivestock()
        {
            // A depleted herd means the pack must eat: incidents skew to the high end.
            var state = EcosystemWorlds.CreateWorld(13, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.AddChickens(state);
            EcosystemWorlds.AddPigs(state);
            EcosystemWorlds.AddDeer(state, 10); // half the herd gone
            EcosystemWorlds.AddWolves(state, 6);
            var world = new World(state);
            world.RegisterSystem(new WinterPressureSystem(
                new[] { new WinterPressureConfiguration("test") }));
            state.RestoreWinterPressure(new WinterPressureState(initialized: true,
                lastLossDay: EcosystemWorlds.FirstWinterDay - 1));
            int livestockBefore = 20 + 6;
            EcosystemWorlds.TickDays(world, 90);

            int lost = livestockBefore
                - state.Animals.PopulationCount(EcosystemWorlds.Chicken)
                - state.Animals.GetBySpecies(EcosystemWorlds.Pig)
                    .Count(p => p.Location != EcosystemWorlds.ForestEdge);
            Assert.That(lost, Is.InRange(3, 4),
                "With few deer left, the pack takes livestock at the high end of the range.");
        }

        [Test]
        public void ConfigurationValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new WinterPressureSystem(null));
            Assert.Throws<ArgumentException>(() => new WinterPressureSystem(
                new WinterPressureConfiguration[] { null }));
            Assert.Throws<ArgumentException>(() => new WinterPressureSystem(new[]
            {
                new WinterPressureConfiguration("dup"), new WinterPressureConfiguration("dup")
            }));
            Assert.Throws<ArgumentException>(() => new WinterPressureConfiguration(" "));
        }
    }
}
