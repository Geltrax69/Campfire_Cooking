using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Core.Commands
{
    /// <summary>Proves pending-command snapshots keep FIFO order and exact eligible minutes.</summary>
    public sealed class CommandRestoreTests
    {
        [Test]
        public void PendingCommandsRoundTripPreservesOrderAndEligibleMinutes()
        {
            var original = new WorldState(7);
            var trace = new List<string>();
            original.EnqueueCommand(new TestCommand(s => trace.Add("a")));
            original.EnqueueCommand(new TestCommand(s => trace.Add("b")));
            original.Clock = new GameTime(5);
            original.EnqueueCommand(new TestCommand(s => trace.Add("c")));

            PendingCommandsState snapshot = original.CapturePendingCommands();
            Assert.That(snapshot.Entries.Select(entry => entry.EligibleMinute),
                Is.EqualTo(new[] { new GameTime(1), new GameTime(1), new GameTime(6) }));

            // Later enqueues must not leak into the captured snapshot.
            original.EnqueueCommand(new TestCommand(s => trace.Add("late")));
            Assert.That(snapshot.Entries.Count, Is.EqualTo(3));

            var restored = new WorldState(7);
            restored.RestorePendingCommands(snapshot);
            Assert.That(restored.PendingCommandCount, Is.EqualTo(3));

            // Exact eligibility, minute by minute: a and b at minute 1, c only at minute 6.
            var world = new World(restored);
            world.RegisterSystem(new CommandSystem());
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(restored.PendingCommandCount, Is.EqualTo(1));
            restored.Clock = new GameTime(5);
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(restored.PendingCommandCount, Is.Zero);
        }

        [Test]
        public void EmptyQueueRoundTripStaysEmpty()
        {
            var restored = new WorldState(7);
            restored.RestorePendingCommands(new WorldState(7).CapturePendingCommands());
            Assert.That(restored.PendingCommandCount, Is.Zero);
            var trace = new List<string>();
            restored.EnqueueCommand(new TestCommand(s => trace.Add("after")));
            var world = new World(restored);
            world.RegisterSystem(new CommandSystem());
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "after" }));
        }

        [Test]
        public void RestoreReplacesExistingQueue()
        {
            var state = new WorldState(7);
            state.EnqueueCommand(new TestCommand(_ => Assert.Fail("Replaced commands must not run.")));

            var donor = new WorldState(7);
            var trace = new List<string>();
            donor.EnqueueCommand(new TestCommand(s => trace.Add("kept")));

            state.RestorePendingCommands(donor.CapturePendingCommands());
            Assert.That(state.PendingCommandCount, Is.EqualTo(1));
            var world = new World(state);
            world.RegisterSystem(new CommandSystem());
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "kept" }));
        }

        [Test]
        public void InvalidSnapshotsAreRejectedAtomically()
        {
            var state = new WorldState(7);
            var trace = new List<string>();
            state.EnqueueCommand(new TestCommand(s => trace.Add("kept")));

            Assert.Throws<ArgumentNullException>(() => new PendingCommandsState(null));
            Assert.Throws<ArgumentNullException>(() =>
                new PendingCommandsState(new[] { new PendingCommandEntry(null, new GameTime(1)) }));
            Assert.Throws<ArgumentException>(() =>
                new PendingCommandsState(new[] { default(PendingCommandEntry) }));
            var first = new PendingCommandEntry(new TestCommand(_ => { }), new GameTime(6));
            var second = new PendingCommandEntry(new TestCommand(_ => { }), new GameTime(5));
            Assert.Throws<ArgumentException>(() => new PendingCommandsState(new[] { first, second }));
            Assert.Throws<ArgumentNullException>(() => state.RestorePendingCommands(null));

            // Nothing above may have touched the queue.
            Assert.That(state.PendingCommandCount, Is.EqualTo(1));
            var world = new World(state);
            world.RegisterSystem(new CommandSystem());
            world.Tick();
            Assert.That(trace, Is.EqualTo(new[] { "kept" }));
            Assert.That(state.PendingCommandCount, Is.Zero);
        }

        [Test]
        public void SnapshotEntriesAreImmutable()
        {
            var state = new WorldState(7);
            state.EnqueueCommand(new TestCommand(_ => { }));
            PendingCommandsState snapshot = state.CapturePendingCommands();
            Assert.Throws<NotSupportedException>(() =>
                ((IList<PendingCommandEntry>)snapshot.Entries).Add(
                    new PendingCommandEntry(new TestCommand(_ => { }), new GameTime(2))));
            Assert.That(snapshot.Entries.Count, Is.EqualTo(1));
        }

        private sealed class TestCommand : IWorldCommand
        {
            private readonly Action<WorldState> _execute;
            public TestCommand(Action<WorldState> execute) { _execute = execute; }
            public void Execute(WorldState state) => _execute(state);
        }
    }
}
