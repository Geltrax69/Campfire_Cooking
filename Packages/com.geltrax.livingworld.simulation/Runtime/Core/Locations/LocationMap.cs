using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Immutable ordinal map with precomputed shortest durations; null denotes no route.</summary>
    public sealed class LocationMap
    {
        private readonly SortedList<LocationId, LocationDefinition> _locations = new SortedList<LocationId, LocationDefinition>();
        private readonly long[,] _minutes;
        public IReadOnlyList<LocationDefinition> Locations { get; }
        public LocationDefinition this[LocationId id] => _locations.Values[Index(id)];

        public LocationMap(IEnumerable<LocationDefinition> locations, IEnumerable<TravelLink> links)
        {
            if (locations == null) throw new ArgumentNullException(nameof(locations));
            if (links == null) throw new ArgumentNullException(nameof(links));
            foreach (var location in locations)
            {
                if (location == null) throw new ArgumentException("Locations must not contain null.", nameof(locations));
                _locations.Add(location.Id, location);
            }
            Locations = new ReadOnlyCollection<LocationDefinition>(_locations.Values);
            int count = _locations.Count;
            _minutes = new long[count, count];
            for (int i = 0; i < count; i++)
                for (int j = 0; j < count; j++) _minutes[i, j] = i == j ? 0 : -1;
            foreach (var link in links)
            {
                if (link == null) throw new ArgumentException("Links must not contain null.", nameof(links));
                int from = Index(link.From), to = Index(link.To);
                if (_minutes[from, to] >= 0) throw new ArgumentException("Duplicate undirected link.", nameof(links));
                _minutes[from, to] = _minutes[to, from] = link.Minutes;
            }
            // Floyd-Warshall runs only at setup. Widen before adding: routes may exceed int.MaxValue.
            // Positive int links and an int-sized node count bound every shortest path below long.MaxValue.
            for (int k = 0; k < count; k++)
                for (int i = 0; i < count; i++)
                    for (int j = 0; j < count; j++)
                    {
                        if (_minutes[i, k] < 0 || _minutes[k, j] < 0) continue;
                        long candidate = checked(_minutes[i, k] + _minutes[k, j]);
                        if (_minutes[i, j] < 0 || candidate < _minutes[i, j]) _minutes[i, j] = candidate;
                    }
        }

        public long? GetTravelMinutes(LocationId from, LocationId to)
        {
            long minutes = _minutes[Index(from), Index(to)];
            return minutes < 0 ? (long?)null : minutes;
        }

        private int Index(LocationId id)
        {
            if (!id.IsValid) throw new ArgumentException("A location ID must be valid.", nameof(id));
            int index = _locations.IndexOfKey(id);
            if (index < 0) throw new ArgumentException("Unknown location ID.", nameof(id));
            return index;
        }
    }
}
