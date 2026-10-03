using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Core.Commands
{
    /// <summary>Verifies next-minute FIFO commands, queue isolation and fail-stop execution.</summary>
    public sealed class CommandTests
    {
        [TestCase(0L)]
        [TestCase(59L)]
        public void CommandsWaitForNextTickAndRunFifoBeforeNeedsAndDecisions(long start)
        {
            var state = new WorldState(42, new GameTime(start));
            var world = new World(state);
            var trace = new List<string>();
            var system = new CommandSystem();
            Assert.That(system.Id, Is.EqualTo("core.commands"));
            Assert.That(system.Phase, Is.EqualTo(SimulationPhase.Commands));
            world.RegisterSystem(new TestSystem("decisions", SimulationPhase.Decisions, _ => trace.Add("decisions")));
            world.RegisterSystem(new TestSystem("needs", SimulationPhase.Needs, _ => trace.Add("needs")));
            world.RegisterSystem(system);
            foreach (string name in new[] { "first", "second" })
                state.EnqueueCommand(new TestCommand(seen =>
                {
                    Assert.That(seen, Is.SameAs(state));
                    trace.Add(name + ":" + seen.Clock.TotalMinutes);
                    seen.Rng.NextUInt64();
                }));
            system.Tick(state);
            Assert.That(trace, Is.Empty);
            Assert.That(state.PendingCommandCount, Is.EqualTo(2));
            Assert.That(state.Clock.TotalMinutes, Is.EqualTo(start));
            Assert.That(state.Rng.State, Is.EqualTo(42UL));
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "first:" + (start + 1), "second:" + (start + 1), "needs", "decisions" }));
            Assert.That(state.PendingCommandCount, Is.Zero);
            ulong rngAfterCommands = state.Rng.State;
            world.Tick();
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "first:" + (start + 1), "second:" + (start + 1),
                "needs", "decisions", "needs", "decisions", "needs", "decisions" }));
            Assert.That(state.Rng.State, Is.EqualTo(rngAfterCommands));
        }

        [TestCase(SimulationPhase.Commands, "aaa.producer")]
        [TestCase(SimulationPhase.Commands, "zzz.producer")]
        [TestCase(SimulationPhase.Needs, "producer")]
        [TestCase(SimulationPhase.Decisions, "producer")]
        [TestCase(SimulationPhase.Actions, "producer")]
        [TestCase(SimulationPhase.Perception, "producer")]
        [TestCase(SimulationPhase.Social, "producer")]
        [TestCase(SimulationPhase.Economy, "producer")]
        [TestCase(SimulationPhase.Memory, "producer")]
        public void CommandsSubmittedInAnySystemWaitUntilFollowingMinute(SimulationPhase phase, string id)
        {
            var state = new WorldState(42);
            var world = new World(state);
            var trace = new List<string>();
            world.RegisterSystem(new CommandSystem());
            world.RegisterSystem(new TestSystem(id, phase, seen =>
            {
                if (seen.Clock.TotalMinutes != 1) return;
                foreach (string name in new[] { "new1", "new2" })
                    seen.EnqueueCommand(new TestCommand(s => trace.Add(name + ":" + s.Clock.TotalMinutes)));
            }));
            state.EnqueueCommand(new TestCommand(s => trace.Add("ready:" + s.Clock.TotalMinutes)));
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "ready:1" }));
            Assert.That(state.PendingCommandCount, Is.EqualTo(2));
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "ready:1", "new1:2", "new2:2" }));
            Assert.That(state.PendingCommandCount, Is.Zero);
        }

        [Test]
        public void CommandSubmittedByCommandWaitsAndKeepsOrderWithLaterExternalInput()
        {
            var state = new WorldState(42);
            var world = new World(state);
            var trace = new List<string>();
            world.RegisterSystem(new CommandSystem());
            state.EnqueueCommand(new TestCommand(s =>
            {
                trace.Add("parent:" + s.Clock.TotalMinutes);
                s.EnqueueCommand(new TestCommand(child => trace.Add("child:" + child.Clock.TotalMinutes)));
            }));
            state.EnqueueCommand(new TestCommand(s => trace.Add("sibling:" + s.Clock.TotalMinutes)));
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "parent:1", "sibling:1" }));
            Assert.That(state.PendingCommandCount, Is.EqualTo(1));
            state.EnqueueCommand(new TestCommand(s => trace.Add("external:" + s.Clock.TotalMinutes)));
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "parent:1", "sibling:1", "child:2", "external:2" }));
            Assert.That(state.PendingCommandCount, Is.Zero);
        }

        [Test]
        public void SharedSystemKeepsWorldQueuesIsolated()
        {
            var first = new World(new WorldState(42));
            var second = new World(new WorldState(42));
            var trace = new List<WorldState>();
            var system = new CommandSystem();
            first.RegisterSystem(system);
            second.RegisterSystem(system);
            first.State.EnqueueCommand(new TestCommand(trace.Add));
            second.State.EnqueueCommand(new TestCommand(trace.Add));
            first.Tick();
            Assert.That(trace, Is.EqualTo(new[] { first.State }));
            Assert.That(first.State.PendingCommandCount, Is.Zero);
            Assert.That(second.State.PendingCommandCount, Is.EqualTo(1));
            Assert.That(second.State.Clock.TotalMinutes, Is.Zero);
            second.Tick();
            Assert.That(trace, Is.EqualTo(new[] { first.State, second.State }));
            Assert.That(second.State.PendingCommandCount, Is.Zero);
        }

        [Test]
        public void RegistrationIsExplicitAndInvalidEnqueuesLeavePendingCommandsIntact()
        {
            var state = new WorldState(42, new GameTime(long.MaxValue - 1));
            var trace = new List<long>();
            state.EnqueueCommand(new TestCommand(s => trace.Add(s.Clock.TotalMinutes)));
            Assert.Throws<ArgumentNullException>(() => state.EnqueueCommand(null));
            Assert.That(state.PendingCommandCount, Is.EqualTo(1));
            new World(state).Tick();
            Assert.That(trace, Is.Empty, "World must not install a CommandSystem implicitly.");
            Assert.That(state.PendingCommandCount, Is.EqualTo(1));
            Assert.Throws<OverflowException>(() => state.EnqueueCommand(new TestCommand(_ => trace.Add(-1))));
            Assert.That(state.PendingCommandCount, Is.EqualTo(1));
            Assert.That(state.Clock.TotalMinutes, Is.EqualTo(long.MaxValue));
            Assert.That(state.Rng.State, Is.EqualTo(42UL));
            new CommandSystem().Tick(state);
            Assert.That(trace, Is.EqualTo(new[] { long.MaxValue }));
            Assert.That(state.PendingCommandCount, Is.Zero);
        }

        [Test]
        public void FailureRemovesAttemptedCommandPreservesRemainderAndFaultsWorldWithoutRetry()
        {
            var state = new WorldState(42);
            var world = new World(state);
            var trace = new List<string>();
            var error = new ApplicationException("command failed");
            world.RegisterSystem(new CommandSystem());
            world.RegisterSystem(new TestSystem("needs", SimulationPhase.Needs, _ => trace.Add("needs")));
            state.EnqueueCommand(new TestCommand(_ => trace.Add("success")));
            state.EnqueueCommand(new TestCommand(s =>
            {
                trace.Add("failure");
                s.Rng.NextUInt64();
                Assert.That(s.PendingCommandCount, Is.EqualTo(1), "Remove the command before invoking it.");
                throw error;
            }));
            state.EnqueueCommand(new TestCommand(_ => trace.Add("later")));
            Assert.That(Assert.Throws<ApplicationException>(() => world.Tick()), Is.SameAs(error));
            Assert.That(world.IsFaulted, Is.True);
            Assert.That(state.PendingCommandCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => world.Tick());
            Assert.That(trace, Is.EqualTo(new[] { "success", "failure" }));
            Assert.That(state.Clock.TotalMinutes, Is.EqualTo(1));
            var expectedRng = new SimRng(42);
            expectedRng.NextUInt64();
            Assert.That(state.Rng.State, Is.EqualTo(expectedRng.State));
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ReentrantProcessingIsRejectedAcrossSystemInstances(bool sameSystem, bool catchInside)
        {
            var state = new WorldState(42);
            var world = new World(state);
            var system = new CommandSystem();
            int laterCalls = 0;
            world.RegisterSystem(system);
            state.EnqueueCommand(new TestCommand(s =>
            {
                TestDelegate reenter = () => (sameSystem ? system : new CommandSystem()).Tick(s);
                if (catchInside) Assert.Throws<InvalidOperationException>(reenter);
                else reenter();
            }));
            state.EnqueueCommand(new TestCommand(_ => laterCalls++));
            if (catchInside) world.Tick();
            else Assert.Throws<InvalidOperationException>(() => world.Tick());
            Assert.That(world.IsFaulted, Is.EqualTo(!catchInside));
            Assert.That(laterCalls, Is.EqualTo(catchInside ? 1 : 0));
            Assert.That(state.PendingCommandCount, Is.EqualTo(catchInside ? 0 : 1));
            if (catchInside)
            {
                state.EnqueueCommand(new TestCommand(_ => laterCalls++));
                world.Tick();
                Assert.That(laterCalls, Is.EqualTo(2));
            }
        }

        [Test]
        public void SystemRejectsNullState() => Assert.Throws<ArgumentNullException>(() => new CommandSystem().Tick(null));

        [Test]
        public void SameSeedAndInputsProduceIdenticalCommandTraces()
        {
            var traces = new[] { new List<(long, string, ulong)>(), new List<(long, string, ulong)>() };
            for (int run = 0; run < traces.Length; run++)
            {
                var trace = traces[run];
                var world = new World(new WorldState(42));
                world.RegisterSystem(new CommandSystem());
                world.RegisterSystem(new TestSystem("producer", SimulationPhase.Decisions, s =>
                {
                    if (s.Clock.TotalMinutes <= 100)
                        s.EnqueueCommand(new TestCommand(c => trace.Add((c.Clock.TotalMinutes, "npc", c.Rng.NextUInt64()))));
                }));
                for (int tick = 0; tick < 100; tick++)
                {
                    world.State.EnqueueCommand(new TestCommand(s => trace.Add((s.Clock.TotalMinutes, "player", s.Rng.NextUInt64()))));
                    world.Tick();
                }
                world.Tick();
                Assert.That(world.State.PendingCommandCount, Is.Zero);
            }
            Assert.That(traces[0].Count, Is.EqualTo(200));
            Assert.That(traces[0], Is.EqualTo(traces[1]));
        }

        private sealed class TestCommand : IWorldCommand
        {
            private readonly Action<WorldState> _execute;
            public TestCommand(Action<WorldState> execute) { _execute = execute; }
            public void Execute(WorldState state) => _execute(state);
        }

        private sealed class TestSystem : IWorldSystem
        {
            private readonly Action<WorldState> _tick;
            public string Id { get; }
            public SimulationPhase Phase { get; }
            public TestSystem(string id, SimulationPhase phase, Action<WorldState> tick)
            { Id = id; Phase = phase; _tick = tick; }
            public void Tick(WorldState state) => _tick(state);
        }
    }
}
