using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Owns every known village by VillageId in deterministic ordinal order (P6-01).
    /// Millbrook registers with Lod = Full; abstract neighbors tick through
    /// VillageDriftSystem. Capture/Restore carry the whole registry plus the drift
    /// day cursor across save/load (validated atomically: a bad snapshot leaves
    /// the registry untouched).
    /// </summary>
    public sealed class VillageRegistry
    {
        private readonly SortedList<VillageId, AbstractVillageState> _villages =
            new SortedList<VillageId, AbstractVillageState>();

        /// <summary>Absolute day number of the last completed drift; 0 when never drifted.</summary>
        public long LastDriftDay { get; private set; }

        public int Count => _villages.Count;

        public AbstractVillageState this[VillageId id]
        {
            get
            {
                if (!id.IsValid) throw new ArgumentException("A village ID must be valid.", nameof(id));
                if (!_villages.TryGetValue(id, out var village))
                    throw new ArgumentException("Unknown village ID: '" + id + "'.", nameof(id));
                return village;
            }
        }

        /// <summary>Registers a village; IDs must be unique.</summary>
        public void Register(AbstractVillageState village)
        {
            if (village == null) throw new ArgumentNullException(nameof(village));
            if (_villages.ContainsKey(village.Id))
                throw new ArgumentException(
                    "Village ID is already registered: '" + village.Id + "'.", nameof(village));
            _villages.Add(village.Id, village);
        }

        /// <summary>Every village in deterministic ordinal VillageId order.</summary>
        public IReadOnlyList<AbstractVillageState> GetAll() =>
            new List<AbstractVillageState>(_villages.Values);

        /// <summary>Villages at the given fidelity, in ordinal VillageId order.</summary>
        public IReadOnlyList<AbstractVillageState> GetByLod(VillageLod lod)
        {
            var matching = new List<AbstractVillageState>();
            foreach (var village in _villages.Values)
                if (village.Lod == lod) matching.Add(village);
            return matching;
        }

        /// <summary>Records that the drift ran for the given absolute day.</summary>
        public void RecordDriftDay(long day)
        {
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            if (day > LastDriftDay) LastDriftDay = day;
        }

        /// <summary>Captures every village plus the drift cursor for Persistence.</summary>
        public VillageRegistrySnapshot Capture() =>
            new VillageRegistrySnapshot(_villages.Values, LastDriftDay);

        /// <summary>
        /// Installs a validated snapshot atomically: validation runs first, so a
        /// rejected snapshot leaves the registry unchanged.
        /// </summary>
        public void Restore(VillageRegistrySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            // The snapshot constructor already validated uniqueness, nulls and the day cursor.
            _villages.Clear();
            foreach (var village in snapshot.Villages)
                _villages.Add(village.Id, village);
            LastDriftDay = snapshot.LastDriftDay;
        }
    }
}
