using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Immutable snapshot of the village registry for save/load (P6-01). Carries
    /// every village state plus the drift day cursor, so a save/load round trip
    /// never double-drifts a day (the determinism invariant).
    /// </summary>
    public sealed class VillageRegistrySnapshot
    {
        public VillageRegistrySnapshot(IEnumerable<AbstractVillageState> villages, long lastDriftDay)
        {
            if (villages == null) throw new ArgumentNullException(nameof(villages));
            if (lastDriftDay < 0) throw new ArgumentOutOfRangeException(nameof(lastDriftDay));
            var ordered = new SortedList<VillageId, AbstractVillageState>();
            foreach (var village in villages)
            {
                if (village == null)
                    throw new ArgumentException("Village snapshots cannot contain null.", nameof(villages));
                if (ordered.ContainsKey(village.Id))
                    throw new ArgumentException(
                        "Village snapshot IDs must be unique: '" + village.Id + "'.", nameof(villages));
                ordered.Add(village.Id, village);
            }
            Villages = new ReadOnlyCollection<AbstractVillageState>(
                new List<AbstractVillageState>(ordered.Values));
            LastDriftDay = lastDriftDay;
        }

        /// <summary>Every village, in deterministic ordinal VillageId order.</summary>
        public IReadOnlyList<AbstractVillageState> Villages { get; }

        /// <summary>Absolute day number of the last completed drift; 0 when never drifted.</summary>
        public long LastDriftDay { get; }
    }
}
