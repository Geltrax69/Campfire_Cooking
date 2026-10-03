using System;
using System.Collections.Generic;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Owns this world's single-threaded FIFO of next-minute commands.</summary>
    public sealed partial class WorldState
    {
        private readonly Queue<PendingCommand> _pendingCommands = new Queue<PendingCommand>();
        internal bool IsProcessingCommands { get; set; }
        public int PendingCommandCount => _pendingCommands.Count;

        /// <summary>Queues input for no earlier than the minute after the current clock.</summary>
        public void EnqueueCommand(IWorldCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            // Check overflow before mutation, including input from an earlier Commands system.
            GameTime eligibleMinute = Clock.Advance(1);
            _pendingCommands.Enqueue(new PendingCommand(command, eligibleMinute));
        }

        internal bool TryDequeueEligibleCommand(out IWorldCommand command)
        {
            command = null;
            if (_pendingCommands.Count == 0 || _pendingCommands.Peek().EligibleMinute > Clock) return false;
            command = _pendingCommands.Dequeue().Command;
            return true;
        }

        /// <summary>Captures pending commands in FIFO order with exact eligible minutes.</summary>
        internal PendingCommandsState CapturePendingCommands()
        {
            var entries = new List<PendingCommandEntry>(_pendingCommands.Count);
            foreach (var pending in _pendingCommands)
                entries.Add(new PendingCommandEntry(pending.Command, pending.EligibleMinute));
            return new PendingCommandsState(entries);
        }

        /// <summary>Replaces the queue with a validated snapshot; FIFO order and eligibility are unchanged.</summary>
        internal void RestorePendingCommands(PendingCommandsState snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            // The snapshot was validated at construction, so installation cannot fail partway.
            _pendingCommands.Clear();
            foreach (var entry in snapshot.Entries)
                _pendingCommands.Enqueue(new PendingCommand(entry.Command, entry.EligibleMinute));
        }

        private readonly struct PendingCommand
        {
            public IWorldCommand Command { get; }
            public GameTime EligibleMinute { get; }
            public PendingCommand(IWorldCommand command, GameTime eligibleMinute)
            { Command = command; EligibleMinute = eligibleMinute; }
        }
    }
}
