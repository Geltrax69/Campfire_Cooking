using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Tracks which emergent events are currently active (P5-03). An event is active
    /// from when its conditions are met until they clear. The state is caller-owned
    /// and restorable for Persistence.
    /// </summary>
    public sealed class EmergentEventState
    {
        private readonly SortedDictionary<EmergentEventId, long> _activeEvents;

        public EmergentEventState()
        {
            _activeEvents = new SortedDictionary<EmergentEventId, long>();
            LastMerchantDay = -1;
        }

        /// <summary>Day number of the last merchant arrival; -1 if none yet.</summary>
        public long LastMerchantDay { get; private set; }

        /// <summary>Currently active events in ordinal ID order.</summary>
        public IReadOnlyList<EmergentEventId> ActiveEvents =>
            new ReadOnlyCollection<EmergentEventId>(new List<EmergentEventId>(_activeEvents.Keys));

        public bool IsActive(EmergentEventId id) => _activeEvents.ContainsKey(id);

        /// <summary>Day the event became active; -1 if not active.</summary>
        public long StartedDay(EmergentEventId id) =>
            _activeEvents.TryGetValue(id, out long day) ? day : -1;

        internal void Activate(EmergentEventId id, long day)
        {
            if (!id.IsValid) throw new ArgumentException("Event ID must be valid.", nameof(id));
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            _activeEvents[id] = day;
            if (id == EmergentEventId.MerchantArrival)
                LastMerchantDay = day;
        }

        internal void Deactivate(EmergentEventId id)
        {
            if (!id.IsValid) throw new ArgumentException("Event ID must be valid.", nameof(id));
            _activeEvents.Remove(id);
        }

        /// <summary>Captures the state for save/load.</summary>
        public EmergentEventSnapshot Capture()
        {
            var active = new List<KeyValuePair<string, long>>();
            foreach (var kvp in _activeEvents)
                active.Add(new KeyValuePair<string, long>(kvp.Key.Value, kvp.Value));
            return new EmergentEventSnapshot(active, LastMerchantDay);
        }

        /// <summary>Restores from a snapshot; validates before mutating.</summary>
        public void Restore(EmergentEventSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var restored = new SortedDictionary<EmergentEventId, long>();
            foreach (var kvp in snapshot.ActiveEvents)
            {
                var id = new EmergentEventId(kvp.Key);
                if (!id.IsValid) throw new ArgumentException("Invalid event ID in snapshot.");
                if (!EmergentEventId.IsKnown(id))
                    throw new ArgumentException("Unknown emergent event ID in snapshot: " + kvp.Key + ".");
                if (kvp.Value < 1) throw new ArgumentOutOfRangeException("Event start day must be positive.");
                if (restored.ContainsKey(id)) throw new ArgumentException("Duplicate event in snapshot.");
                restored.Add(id, kvp.Value);
            }
            if (snapshot.LastMerchantDay < -1)
                throw new ArgumentOutOfRangeException("Last merchant day must be -1 or positive.");
            _activeEvents.Clear();
            foreach (var kvp in restored)
                _activeEvents.Add(kvp.Key, kvp.Value);
            LastMerchantDay = snapshot.LastMerchantDay;
        }
    }

    /// <summary>Immutable snapshot of emergent event state for Persistence.</summary>
    public sealed class EmergentEventSnapshot
    {
        public EmergentEventSnapshot(IReadOnlyList<KeyValuePair<string, long>> activeEvents, long lastMerchantDay)
        {
            ActiveEvents = activeEvents ?? throw new ArgumentNullException(nameof(activeEvents));
            LastMerchantDay = lastMerchantDay;
        }

        public IReadOnlyList<KeyValuePair<string, long>> ActiveEvents { get; }
        public long LastMerchantDay { get; }
    }
}
