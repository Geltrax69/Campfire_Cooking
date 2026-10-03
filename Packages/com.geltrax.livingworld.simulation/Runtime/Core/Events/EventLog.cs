using System;
using System.Collections.Generic;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Owns chronological truth with stable IDs, snapshot queries and explicit reference-safe pruning.</summary>
    public sealed class EventLog
    {
        private readonly List<WorldEvent> _events = new List<WorldEvent>();
        // These high-water marks survive pruning, including an entirely emptied log.
        private long _lastId;
        private GameTime _lastTime;
        private bool _isMutating;

        public int Count => _events.Count;

        public WorldEvent Append(GameTime time, LocationId location, WorldEventType type,
            ActorId? actor = null, IEnumerable<ActorId> targets = null, EventVisibility visibility = EventVisibility.Normal,
            ItemTypeId? itemType = null, int? quantity = null, int? copper = null,
            ReputationGroupId? reputationGroup = null, int? reputationDelta = null)
        {
            BeginMutation();
            try
            {
                if (time < _lastTime) throw new ArgumentException("Event time cannot go backwards.", nameof(time));
                var entry = new WorldEvent(new WorldEventId(checked(_lastId + 1)), time, location,
                    type, actor, targets, visibility, itemType, quantity, copper, reputationGroup, reputationDelta);
                _events.Add(entry);
                _lastId = entry.Id.Value;
                _lastTime = time;
                return entry;
            }
            finally { _isMutating = false; }
        }

        /// <summary>Returns an immutable snapshot in ID order; both time bounds are inclusive.</summary>
        public IReadOnlyList<WorldEvent> Query(GameTime? from = null, GameTime? to = null,
            LocationId? location = null, WorldEventType? type = null)
        {
            if (from.HasValue && to.HasValue && from.Value > to.Value)
                throw new ArgumentException("The lower time bound exceeds the upper bound.", nameof(from));
            if (location.HasValue && !location.Value.IsValid) throw new ArgumentException("Invalid location ID.", nameof(location));
            if (type.HasValue) WorldEvent.ValidateType(type.Value);
            var results = new List<WorldEvent>();
            foreach (var entry in _events)
                if ((!from.HasValue || entry.Time >= from.Value) && (!to.HasValue || entry.Time <= to.Value)
                    && (!location.HasValue || entry.Location == location.Value) && (!type.HasValue || entry.Type == type.Value))
                    results.Add(entry);
            return results.AsReadOnly();
        }

        /// <summary>Removes events strictly before cutoff unless retained; unknown valid IDs are harmless.</summary>
        public int PruneBefore(GameTime cutoff, IEnumerable<WorldEventId> retainedIds)
        {
            BeginMutation();
            try
            {
                if (retainedIds == null) throw new ArgumentNullException(nameof(retainedIds));
                var retained = new HashSet<WorldEventId>();
                foreach (var id in retainedIds)
                {
                    if (!id.IsValid) throw new ArgumentException("Invalid retained event ID.", nameof(retainedIds));
                    retained.Add(id);
                }
                return _events.RemoveAll(entry => entry.Time < cutoff && !retained.Contains(entry.Id));
            }
            finally { _isMutating = false; }
        }

        private void BeginMutation()
        {
            // Input enumerators are caller code; nested writes would break validation atomicity.
            if (_isMutating) throw new InvalidOperationException("Cannot mutate the event log while enumerating its inputs.");
            _isMutating = true;
        }
    }
}
