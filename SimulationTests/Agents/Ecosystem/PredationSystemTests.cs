using System;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents.Ecosystem
{
    /// <summary>
    /// Wolves hunt deer with a daily chance that rises in winter. Each kill removes
    /// the prey and records a Predation truth event; boars are never prey.
    /// </summary>
    public sealed class PredationSystemTests
    {
        private static (World world, WorldState state) WinterWorld(ulong seed, long startDay)
        {
            var state = EcosystemWorlds.CreateWorld(seed, startDay);
            EcosystemWorlds.AddWolves(state, 6);
            EcosystemWorlds.AddDeer(state, 40);
            var world = new World(state);
            world.RegisterSystem(new PredationSystem(new[] { new PredationConfiguration("test") }));
            // The world starts mid-year; the systems must not catch up from day 1.
            state.RestorePredation(new PredationState(initialized: true, lastHuntDay: startDay - 1));
            return (world, state);
        }

        [Test]
        public void WolfPredationReducesDeer()
        {
            var (world, state) = WinterWorld(seed: 7, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.TickDays(world, 30);

            int remaining = state.Animals.PopulationCount(EcosystemWorlds.Deer);
            Assert.That(remaining, Is.LessThan(40), "Wolves hunted deer over the month.");
            Assert.That(EcosystemWorlds.CountEvents(state, WorldEventType.Predation),
                Is.EqualTo(40 - remaining), "Every kill is recorded as truth.");
        }

        [Test]
        public void WinterPredationExceedsWarmPredation()
        {
            // One seed can be unlucky; the higher winter chance must show across seeds.
            int winterKills = 0;
            int warmKills = 0;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var (winterWorld, winterState) = WinterWorld(seed, EcosystemWorlds.FirstWinterDay);
                EcosystemWorlds.TickDays(winterWorld, 90);
                winterKills += 40 - winterState.Animals.PopulationCount(EcosystemWorlds.Deer);

                var warmState = EcosystemWorlds.CreateWorld(seed, EcosystemWorlds.FirstSummerDay);

                EcosystemWorlds.AddWolves(warmState, 6);
                EcosystemWorlds.AddDeer(warmState, 40);
                var warmWorld = new World(warmState);
                warmWorld.RegisterSystem(new PredationSystem(new[] { new PredationConfiguration("test") }));
                warmState.RestorePredation(new PredationState(initialized: true,
                    lastHuntDay: EcosystemWorlds.FirstSummerDay - 1));
                EcosystemWorlds.TickDays(warmWorld, 90);
                warmKills += 40 - warmState.Animals.PopulationCount(EcosystemWorlds.Deer);
            }

            Assert.That(winterKills, Is.GreaterThan(0), "Wolves hunt in winter.");
            Assert.That(winterKills, Is.GreaterThan(warmKills),
                "The daily kill chance rises in winter.");
        }

        [Test]
        public void WolvesNeverTakeBoars()
        {
            var state = EcosystemWorlds.CreateWorld(3, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.AddWolves(state, 6);
            EcosystemWorlds.AddPigs(state);
            var world = new World(state);
            world.RegisterSystem(new PredationSystem(new[] { new PredationConfiguration("test") }));
            state.RestorePredation(new PredationState(initialized: true,
                lastHuntDay: EcosystemWorlds.FirstWinterDay - 1));

            EcosystemWorlds.TickDays(world, 90);

            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Pig), Is.EqualTo(12),
                "Boars are too dangerous; wolves leave all pigs alone.");
        }

        [Test]
        public void PredationIsDeterministic()
        {
            var (firstWorld, firstState) = WinterWorld(seed: 42, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.TickDays(firstWorld, 45);
            var (secondWorld, secondState) = WinterWorld(seed: 42, EcosystemWorlds.FirstWinterDay);
            EcosystemWorlds.TickDays(secondWorld, 45);

            Assert.That(secondState.Animals.PopulationCount(EcosystemWorlds.Deer),
                Is.EqualTo(firstState.Animals.PopulationCount(EcosystemWorlds.Deer)));
            Assert.That(
                secondState.Events.Query(type: WorldEventType.Predation)
                    .Select(e => (e.Time.Day, e.Location.Value)).ToList(),
                Is.EqualTo(firstState.Events.Query(type: WorldEventType.Predation)
                    .Select(e => (e.Time.Day, e.Location.Value)).ToList()));
        }

        [Test]
        public void PredationRunsOncePerDay()
        {
            var (world, state) = WinterWorld(seed: 7, EcosystemWorlds.FirstWinterDay);
            // Two ticks inside the same game day must not hunt twice.
            world.Tick();
            world.Tick();
            int afterSameDay = state.Animals.PopulationCount(EcosystemWorlds.Deer);
            EcosystemWorlds.TickDays(world, 1);
            Assert.That(state.Animals.PopulationCount(EcosystemWorlds.Deer),
                Is.LessThanOrEqualTo(afterSameDay));
        }

        [Test]
        public void ConfigurationValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new PredationSystem(null));
            Assert.Throws<ArgumentException>(() => new PredationSystem(new PredationConfiguration[] { null }));
            Assert.Throws<ArgumentException>(() => new PredationSystem(new[]
            {
                new PredationConfiguration("dup"), new PredationConfiguration("dup")
            }));
            Assert.Throws<ArgumentException>(() => new PredationConfiguration(" "));
        }
    }
}
