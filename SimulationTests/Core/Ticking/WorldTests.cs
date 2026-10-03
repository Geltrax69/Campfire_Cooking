using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Core.Ticking
{
    /// <summary>Verifies deterministic ticks, registration boundaries and fail-stop behavior.</summary>
    public sealed class WorldTests
    {
        [TestCase(0L)]
        [TestCase(4385L)]
        public void EmptyWorldAdvancesOneDayFromItsStartingTime(long start)
        {
            var state = start == 0 ? new WorldState(42) : new WorldState(42, new GameTime(start));
            var world = new World(state);
            Assert.That(world.State, Is.SameAs(state));
            Assert.That(state.Clock.TotalMinutes, Is.EqualTo(start));
            for (int i = 0; i < 1440; i++) world.Tick();
            Assert.That(state.Clock.TotalMinutes, Is.EqualTo(start + 1440));
            Assert.That(state.Clock.Day, Is.EqualTo(new GameTime(start).Day + 1));
            Assert.That(state.Rng.State, Is.EqualTo(42UL));
            Assert.That(world.IsFaulted, Is.False);
            Assert.Throws<InvalidOperationException>(() => world.RegisterSystem(new TestSystem("late")));
        }

        [Test]
        public void ScrambledRegistrationRunsAllPhasesAndOrdinalIdsAfterClockAdvances()
        {
            var state = new WorldState(42, new GameTime(59));
            var rng = state.Rng;
            var world = new World(state);
            var trace = new List<string>();
            SimulationPhase[] phases = { SimulationPhase.Commands, SimulationPhase.Needs,
                SimulationPhase.Decisions, SimulationPhase.Actions, SimulationPhase.Perception,
                SimulationPhase.Social, SimulationPhase.Economy, SimulationPhase.Memory };
            for (int i = phases.Length - 1; i >= 0; i--)
            {
                var phase = phases[i];
                world.RegisterSystem(new TestSystem(phase.ToString(), phase, seen =>
                {
                    Assert.That(seen, Is.SameAs(state));
                    Assert.That(seen.Rng, Is.SameAs(rng));
                    Assert.That(seen.Clock.TotalMinutes, Is.EqualTo(60));
                    trace.Add(phase.ToString());
                }));
            }
            foreach (string id in new[] { "ä", "a", "B", "A" })
                world.RegisterSystem(new TestSystem(id, SimulationPhase.Needs, _ => trace.Add(id)));
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "Commands", "A", "B", "Needs", "a", "ä",
                "Decisions", "Actions", "Perception", "Social", "Economy", "Memory" }));
        }

        [Test]
        public void RejectsNullWorldStateAndSystem()
        {
            Assert.Throws<ArgumentNullException>(() => new World(null));
            var world = new World(new WorldState(0));
            Assert.Throws<ArgumentNullException>(() => world.RegisterSystem(null));
            world.Tick();
            Assert.That(world.State.Clock.TotalMinutes, Is.EqualTo(1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t\r\n")]
        public void InvalidIdsDoNotRegisterOrCloseRegistration(string id)
        {
            var world = new World(new WorldState(0));
            Assert.Throws<ArgumentException>(() => world.RegisterSystem(new TestSystem(id)));
            int calls = 0;
            world.RegisterSystem(new TestSystem("valid", tick: _ => calls++));
            world.Tick();
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(-1)]
        [TestCase(8)]
        public void UnknownPhasesDoNotReserveIds(int phase)
        {
            var world = new World(new WorldState(0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                world.RegisterSystem(new TestSystem("same", (SimulationPhase)phase)));
            world.RegisterSystem(new TestSystem("same"));
            world.Tick();
            Assert.That(world.IsFaulted, Is.False);
        }

        [Test]
        public void CapturedKeysStayFixedAndDuplicateIdsAreGlobalAndOrdinal()
        {
            var world = new World(new WorldState(0));
            var trace = new List<string>();
            int reads = 0;
            var first = new TestSystem("A", tick: _ => trace.Add("first"));
            first.OnKeyRead = () => reads++;
            world.RegisterSystem(new TestSystem("B", tick: _ => trace.Add("second")));
            world.RegisterSystem(first);
            first.Name = "Z";
            first.Stage = SimulationPhase.Memory;
            Assert.Throws<ArgumentException>(() => world.RegisterSystem(new TestSystem("A", SimulationPhase.Actions)));
            world.RegisterSystem(new TestSystem("a", tick: _ => trace.Add("lowercase")));
            world.Tick();
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "first", "second", "lowercase", "first", "second", "lowercase" }));
            Assert.That(reads, Is.EqualTo(2), "Read Id and Phase once at registration only.");
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        public void RunningTickRejectsNestedTicksAndRegistration(bool nestedTick, bool catchInside)
        {
            var world = new World(new WorldState(0));
            int calls = 0;
            TestDelegate attempt = () =>
            {
                if (nestedTick) world.Tick();
                else world.RegisterSystem(new TestSystem("late"));
            };
            world.RegisterSystem(new TestSystem("first", tick: _ =>
            {
                if (catchInside) Assert.Throws<InvalidOperationException>(attempt);
                else attempt();
            }));
            world.RegisterSystem(new TestSystem("second", tick: _ => calls++));
            if (catchInside) world.Tick();
            else Assert.Throws<InvalidOperationException>(() => world.Tick());
            Assert.That(world.State.Clock.TotalMinutes, Is.EqualTo(1));
            Assert.That(world.IsFaulted, Is.EqualTo(!catchInside));
            Assert.That(calls, Is.EqualTo(catchInside ? 1 : 0));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RegistrationGettersCannotReenterWorldAndFailureLeavesRegistrationUsable(bool nestedTick)
        {
            var world = new World(new WorldState(0));
            var candidate = new TestSystem("candidate");
            candidate.OnKeyRead = () =>
            {
                if (nestedTick) world.Tick();
                else world.RegisterSystem(new TestSystem("nested"));
            };
            Assert.Throws<InvalidOperationException>(() => world.RegisterSystem(candidate));
            Assert.That(world.State.Clock.TotalMinutes, Is.Zero);
            Assert.That(world.IsFaulted, Is.False);
            candidate.OnKeyRead = null;
            world.RegisterSystem(candidate);
            world.Tick();
            Assert.That(world.State.Clock.TotalMinutes, Is.EqualTo(1));
        }

        [Test]
        public void SystemFailurePropagatesOriginalExceptionAndPermanentlyStopsPartialWorld()
        {
            var world = new World(new WorldState(42));
            var error = new ApplicationException("system failed");
            int laterCalls = 0;
            world.RegisterSystem(new TestSystem("first", tick: state =>
            {
                state.Rng.NextUInt64();
                throw error;
            }));
            world.RegisterSystem(new TestSystem("second", tick: _ => laterCalls++));
            Assert.That(Assert.Throws<ApplicationException>(() => world.Tick()), Is.SameAs(error));
            var expectedRng = new SimRng(42);
            expectedRng.NextUInt64();
            Assert.That(world.IsFaulted, Is.True);
            Assert.Throws<InvalidOperationException>(() => world.Tick());
            Assert.Throws<InvalidOperationException>(() => world.RegisterSystem(new TestSystem("late")));
            Assert.That(world.State.Clock.TotalMinutes, Is.EqualTo(1));
            Assert.That(world.State.Rng.State, Is.EqualTo(expectedRng.State));
            Assert.That(laterCalls, Is.Zero);
        }

        [Test]
        public void OverflowFaultsBeforeAnySystemOrRngRunsAndClosesRegistration()
        {
            var world = new World(new WorldState(42, new GameTime(long.MaxValue)));
            int calls = 0;
            world.RegisterSystem(new TestSystem("system", tick: state => { calls++; state.Rng.NextUInt64(); }));
            Assert.Throws<OverflowException>(() => world.Tick());
            Assert.That(world.IsFaulted, Is.True);
            Assert.Throws<InvalidOperationException>(() => world.Tick());
            Assert.Throws<InvalidOperationException>(() => world.RegisterSystem(new TestSystem("late")));
            Assert.That(calls, Is.Zero);
            Assert.That(world.State.Clock.TotalMinutes, Is.EqualTo(long.MaxValue));
            Assert.That(world.State.Rng.State, Is.EqualTo(42UL));
        }

        [Test]
        public void SameSeedAndSystemsReproduceTracesDespiteDifferentRegistrationOrder()
        {
            var worlds = new[] { new World(new WorldState(42)), new World(new WorldState(42)) };
            var traces = new[] { new List<(long, string, ulong)>(), new List<(long, string, ulong)>() };
            for (int run = 0; run < worlds.Length; run++)
            {
                var trace = traces[run];
                foreach (string id in run == 0 ? new[] { "A", "B" } : new[] { "B", "A" })
                    worlds[run].RegisterSystem(new TestSystem(id, tick: state =>
                        trace.Add((state.Clock.TotalMinutes, id, state.Rng.NextUInt64()))));
                for (int tick = 0; tick < 2000; tick++) worlds[run].Tick();
            }
            Assert.That(traces[0].Count, Is.EqualTo(4000));
            Assert.That(traces[0], Is.EqualTo(traces[1]));
            Assert.That(worlds[0].State.Rng.State, Is.EqualTo(worlds[1].State.Rng.State));
        }

        private sealed class TestSystem : IWorldSystem
        {
            private readonly Action<WorldState> _tick;
            public string Name;
            public SimulationPhase Stage;
            public Action OnKeyRead;
            public string Id { get { OnKeyRead?.Invoke(); return Name; } }
            public SimulationPhase Phase { get { OnKeyRead?.Invoke(); return Stage; } }
            public TestSystem(string id, SimulationPhase phase = SimulationPhase.Needs, Action<WorldState> tick = null)
            {
                Name = id;
                Stage = phase;
                _tick = tick;
            }
            public void Tick(WorldState state) => _tick?.Invoke(state);
        }
    }
}
