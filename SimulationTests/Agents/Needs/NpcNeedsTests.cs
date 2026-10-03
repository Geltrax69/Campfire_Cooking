using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>Proves exact need accumulation, sleeping behavior and deterministic registration.</summary>
    public sealed class NpcNeedsTests
    {
        private static readonly LocationId Home = new LocationId("loc_home");

        [Test]
        public void AwakeHourAppliesExactRatesIncludingPartialMinutes()
        {
            var npc = State("npc_test", hunger: 10, energy: 80, social: 50, rates: new NeedRates(7, 5, 2));

            for (int minute = 0; minute < 59; minute++) npc.AdvanceNeedsOneMinute();

            Assert.That(npc.Needs.Hunger, Is.EqualTo(16));
            Assert.That(npc.Needs.HungerRemainderSixtieths, Is.EqualTo(53));
            Assert.That(npc.Needs.Energy, Is.EqualTo(75));
            Assert.That(npc.Needs.EnergyRemainderSixtieths, Is.EqualTo(5));
            Assert.That(npc.Needs.Social, Is.EqualTo(48));
            Assert.That(npc.Needs.SocialRemainderSixtieths, Is.EqualTo(2));

            npc.AdvanceNeedsOneMinute();

            Assert.That((npc.Needs.Hunger, npc.Needs.Energy, npc.Needs.Social), Is.EqualTo((17, 75, 48)));
            Assert.That((npc.Needs.HungerSixtieths, npc.Needs.EnergySixtieths, npc.Needs.SocialSixtieths),
                Is.EqualTo((17 * 60, 75 * 60, 48 * 60)));
        }

        [Test]
        public void NeedsSaturateAndSleepingPausesAllAwakeChanges()
        {
            var npc = State("npc_test", 100, 0, 0, new NeedRates(int.MaxValue, int.MaxValue, int.MaxValue));
            npc.AdvanceNeedsOneMinute();
            Assert.That((npc.Needs.HungerSixtieths, npc.Needs.EnergySixtieths, npc.Needs.SocialSixtieths),
                Is.EqualTo((6000, 0, 0)));

            var asleep = State("npc_sleeping", 10, 80, 50, new NeedRates(7, 5, 2));
            asleep.IsSleeping = true;
            asleep.AdvanceNeedsOneMinute();
            Assert.That((asleep.Needs.HungerSixtieths, asleep.Needs.EnergySixtieths, asleep.Needs.SocialSixtieths),
                Is.EqualTo((600, 4800, 3000)));
        }

        [Test]
        public void ConstructorsRejectInvalidValuesBeforeCreatingState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeedRates(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeedRates(0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeedRates(0, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeedState(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeedState(0, 101, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeedState(0, 0, 101));
            Assert.Throws<ArgumentOutOfRangeException>(() => NeedState.FromSixtieths(6001, 0, 0));
            Assert.Throws<ArgumentNullException>(() => new NpcState(null, 0, 0, 0));
        }

        [Test]
        public void RegistryRejectsInvalidEntriesAndEnumeratesByOrdinalId()
        {
            var registry = new NpcRegistry();
            var lower = State("npc_z", 0, 100, 100, new NeedRates(1, 1, 1));
            var upper = State("npc_A", 0, 100, 100, new NeedRates(1, 1, 1));
            registry.Register(lower);
            registry.Register(upper);

            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.Npcs.Select(n => n.Definition.Id.Value), Is.EqualTo(new[] { "npc_A", "npc_z" }));
            Assert.That(registry[new NpcId("npc_z")], Is.SameAs(lower));
            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<ArgumentException>(() => registry.Register(State("npc_z", 0, 0, 0, new NeedRates(0, 0, 0))));
            Assert.Throws<ArgumentException>(() => { var ignored = registry[default(NpcId)]; });
            Assert.Throws<NotSupportedException>(() => ((IList<NpcState>)registry.Npcs).Clear());
        }

        [Test]
        public void NeedsSystemRunsInNeedsPhaseAndTicksInDeterministicOrder()
        {
            var first = Run(new[] { "npc_z", "npc_A" });
            var second = Run(new[] { "npc_A", "npc_z" });
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.EqualTo(new[] { "npc_A:1:99:99", "npc_z:1:99:99" }));
            Assert.That(new NeedsSystem().Phase, Is.EqualTo(SimulationPhase.Needs));
            Assert.That(new NeedsSystem().Id, Is.EqualTo("agents.needs"));
            Assert.Throws<ArgumentNullException>(() => new NeedsSystem().Tick(null));
        }

        private static string[] Run(IEnumerable<string> ids)
        {
            var state = new WorldState(42);
            foreach (var id in ids) state.Npcs.Register(State(id, 0, 100, 100, new NeedRates(60, 60, 60)));
            var world = new World(state);
            world.RegisterSystem(new NeedsSystem());
            world.Tick();
            return state.Npcs.Npcs.Select(n => n.Definition.Id.Value + ":" + n.Needs.Hunger + ":"
                + n.Needs.Energy + ":" + n.Needs.Social).ToArray();
        }

        private static NpcState State(string id, int hunger, int energy, int social, NeedRates rates)
        {
            var definition = new NpcDefinition(new NpcId(id), id, 30, "test", "tester", Home, Home, 0,
                new Dictionary<string, int> { ["honest"] = 50 }, rates);
            return new NpcState(definition, hunger, energy, social);
        }
    }
}
