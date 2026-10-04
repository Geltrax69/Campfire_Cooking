using System;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents.Villages
{
    /// <summary>
    /// Proves abstract villages tick at low fidelity once per day (P6-01): small
    /// seeded drift on population/wealth, seasonal food drift, mood drifting toward
    /// a food-dependent target. Full-LOD Millbrook is never touched by the drift.
    /// </summary>
    public sealed class VillageDriftSystemTests
    {
        private static WorldState StateAtDay(long day, ulong seed = 20261004) =>
            new WorldState(seed, new GameTime((day - 1) * 1440));

        private static void RegisterFactoryVillages(WorldState state)
        {
            state.RestoreVillages(VillageFactory.CreateInitialVillages().Capture());
        }

        private static void TickDays(WorldState state, VillageDriftSystem system, int days)
        {
            for (int i = 0; i < days; i++)
            {
                state.Clock = state.Clock.Advance(1440);
                system.Tick(state);
            }
        }

        private static AbstractVillageState KingsRest(WorldState state) =>
            state.Villages[new VillageId("village_kings_rest")];

        [Test]
        public void DriftHappensOncePerDay()
        {
            var state = StateAtDay(10);
            RegisterFactoryVillages(state);
            var system = new VillageDriftSystem();

            system.Tick(state);
            var afterFirst = state.Villages.Capture();
            system.Tick(state); // Same day: must not drift again.
            var afterSecond = state.Villages.Capture();

            Assert.That(state.Villages.LastDriftDay, Is.EqualTo(10));
            Assert.That(
                afterSecond.Villages.Select(v => v.Population),
                Is.EqualTo(afterFirst.Villages.Select(v => v.Population)));
            Assert.That(
                afterSecond.Villages.Select(v => v.WealthCopper),
                Is.EqualTo(afterFirst.Villages.Select(v => v.WealthCopper)));
        }

        [Test]
        public void AbstractVillagesStayInValidRangesOverLongRuns()
        {
            var state = StateAtDay(10);
            RegisterFactoryVillages(state);
            var system = new VillageDriftSystem();

            TickDays(state, system, 90);

            foreach (var village in state.Villages.GetByLod(VillageLod.Abstract))
            {
                Assert.That(village.Population, Is.GreaterThanOrEqualTo(0));
                Assert.That(village.WealthCopper, Is.GreaterThanOrEqualTo(0));
                Assert.That(village.FoodSupply, Is.InRange(0, 100));
                Assert.That(village.Mood, Is.InRange(0, 100));
            }
            Assert.That(state.Villages.LastDriftDay, Is.EqualTo(100));
        }

        [Test]
        public void FullVillagesAreUntouchedByDrift()
        {
            var state = StateAtDay(10);
            RegisterFactoryVillages(state);
            var system = new VillageDriftSystem();

            TickDays(state, system, 60);

            var millbrook = state.Villages[VillageFactory.Millbrook];
            Assert.That(millbrook.Lod, Is.EqualTo(VillageLod.Full));
            Assert.That(
                (millbrook.Population, millbrook.WealthCopper, millbrook.FoodSupply, millbrook.Mood),
                Is.EqualTo((0, 0, 0, 0)),
                "Full-LOD villages carry no abstract stats; drift must not invent any.");
        }

        [Test]
        public void WinterAlwaysLowersFoodSupply()
        {
            // Days 91-180 are winter (VillageCalendar: 90-day seasons from autumn).
            var state = StateAtDay(91);
            RegisterFactoryVillages(state);
            var system = new VillageDriftSystem();
            int before = KingsRest(state).FoodSupply;

            TickDays(state, system, 30); // All winter days.

            Assert.That(KingsRest(state).FoodSupply, Is.LessThan(before),
                "Every winter day removes 1-2 food supply, so 30 winter days must lower it.");
        }

        [Test]
        public void MoodDriftsTowardFiftyWhenFoodIsPlentiful()
        {
            // Autumn: food drifts 0..+2/day, so a full granary stays full.
            var state = StateAtDay(10);
            RegisterFactoryVillages(state);
            var system = new VillageDriftSystem();
            var village = KingsRest(state);
            village.SetFoodSupply(100);
            village.SetMood(80);

            TickDays(state, system, 40);

            Assert.That(village.Mood, Is.EqualTo(50),
                "Mood moves exactly 1/day toward 50 while food stays plentiful.");
        }

        [Test]
        public void LowFoodSupplyDampsMood()
        {
            // Winter: food drifts -2..-1/day, so a near-empty granary stays near-empty.
            var state = StateAtDay(91);
            RegisterFactoryVillages(state);
            var system = new VillageDriftSystem();
            var village = KingsRest(state);
            village.SetFoodSupply(10);
            village.SetMood(50);

            TickDays(state, system, 40);

            Assert.That(village.Mood, Is.EqualTo(40),
                "Below 25 food supply, mood targets 40 instead of 50.");
        }

        [Test]
        public void DriftIsDeterministicForSameSeed()
        {
            var first = StateAtDay(10, seed: 99);
            RegisterFactoryVillages(first);
            var second = StateAtDay(10, seed: 99);
            RegisterFactoryVillages(second);
            var system = new VillageDriftSystem();

            TickDays(first, system, 30);
            TickDays(second, system, 30);

            var firstCapture = first.Villages.Capture();
            var secondCapture = second.Villages.Capture();
            Assert.That(firstCapture.LastDriftDay, Is.EqualTo(secondCapture.LastDriftDay));
            Assert.That(
                firstCapture.Villages.Select(v => (v.Population, v.WealthCopper, v.FoodSupply, v.Mood)),
                Is.EqualTo(secondCapture.Villages.Select(v => (v.Population, v.WealthCopper, v.FoodSupply, v.Mood))));
        }

        [Test]
        public void TickRejectsNullState()
        {
            var system = new VillageDriftSystem();
            Assert.That(() => system.Tick(null), Throws.ArgumentNullException);
        }

        [Test]
        public void SystemIdentity()
        {
            var system = new VillageDriftSystem();
            Assert.That(system.Id, Is.EqualTo("agents.village-drift"));
            Assert.That(system.Phase, Is.EqualTo(SimulationPhase.Memory));
        }
    }
}
