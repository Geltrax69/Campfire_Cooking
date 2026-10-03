using System;
using System.Collections.Generic;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Immutable FIFO capture of pending commands with exact eligible minutes for restore.</summary>
    internal sealed class PendingCommandsState
    {
        private readonly IReadOnlyList<PendingCommandEntry> _entries;

        internal PendingCommandsState(IReadOnlyList<PendingCommandEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            // Copy before validating: the caller's list may keep changing.
            var copied = new List<PendingCommandEntry>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry.Command == null)
                    throw new ArgumentException("Snapshot entries need a command.", nameof(entries));
                copied.Add(entry);
            }
            // Eligible minutes derive from the monotonic clock, so a live queue never runs backwards.
            for (int i = 1; i < copied.Count; i++)
                if (copied[i].EligibleMinute < copied[i - 1].EligibleMinute)
                    throw new ArgumentException("Snapshot eligible minutes cannot go backwards.", nameof(entries));
            _entries = copied.AsReadOnly();
        }

        public IReadOnlyList<PendingCommandEntry> Entries => _entries;
    }
}
