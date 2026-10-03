using System;
using System.Collections.Generic;

namespace LivingWorld.Simulation.Core
{
    /// <summary>
    /// Immutable capture of an event log for persistence: the retained events plus the issued-ID
    /// and timestamp high-water marks, so restore continues IDs and append time rules exactly,
    /// including when every old event was pruned.
    /// </summary>
    internal sealed class EventLogState
    {
        private readonly IReadOnlyList<WorldEvent> _events;

        internal EventLogState(IReadOnlyList<WorldEvent> events, long lastIssuedId, GameTime lastIssuedTime)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (lastIssuedId < 0) throw new ArgumentOutOfRangeException(nameof(lastIssuedId));
            // Copy before validating: the caller's list may keep changing.
            var copied = new List<WorldEvent>(events.Count);
            foreach (var entry in events)
            {
                if (entry == null) throw new ArgumentException("Snapshot events cannot contain null.", nameof(events));
                copied.Add(entry);
            }
            // Retained events are a subsequence of one log's history: IDs strictly increasing,
            // times non-decreasing, both covered by the high-water marks.
            for (int i = 1; i < copied.Count; i++)
            {
                if (copied[i].Id <= copied[i - 1].Id)
                    throw new ArgumentException("Snapshot events must be in strictly increasing ID order.", nameof(events));
                if (copied[i].Time < copied[i - 1].Time)
                    throw new ArgumentException("Snapshot event times cannot go backwards.", nameof(events));
            }
            if (copied.Count > 0)
            {
                var last = copied[copied.Count - 1];
                if (lastIssuedId < last.Id.Value)
                    throw new ArgumentException("The issued-ID high-water mark must cover every retained event.", nameof(lastIssuedId));
                if (lastIssuedTime < last.Time)
                    throw new ArgumentException("The timestamp high-water mark must cover every retained event.", nameof(lastIssuedTime));
            }
            _events = copied.AsReadOnly();
            LastIssuedId = lastIssuedId;
            LastIssuedTime = lastIssuedTime;
        }

        public IReadOnlyList<WorldEvent> Events => _events;

        /// <summary>Highest event ID ever issued by the captured log; 0 when none was issued.</summary>
        public long LastIssuedId { get; }

        /// <summary>Time of the latest event ever appended by the captured log.</summary>
        public GameTime LastIssuedTime { get; }
    }
}
