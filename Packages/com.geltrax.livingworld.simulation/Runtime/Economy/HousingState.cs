using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Roof conditions per household home (0-100) plus abstracted background households
    /// (P5-01, TOWN.md). A roof is sound at 50 or above. Background villagers are not
    /// simulated, so their households are counts, not locations.
    /// </summary>
    public sealed class HousingState
    {
        private readonly SortedDictionary<LocationId, int> _roofs =
            new SortedDictionary<LocationId, int>();

        public HousingState(int backgroundHouseholds = 0, int backgroundSoundRoofs = 0)
        {
            if (backgroundHouseholds < 0)
                throw new ArgumentOutOfRangeException(nameof(backgroundHouseholds));
            if (backgroundSoundRoofs < 0 || backgroundSoundRoofs > backgroundHouseholds)
                throw new ArgumentException("Sound background roofs cannot exceed background households.",
                    nameof(backgroundSoundRoofs));
            BackgroundHouseholds = backgroundHouseholds;
            BackgroundSoundRoofs = backgroundSoundRoofs;
        }

        public int BackgroundHouseholds { get; }
        public int BackgroundSoundRoofs { get; }

        public IReadOnlyDictionary<LocationId, int> RoofConditions =>
            new ReadOnlyDictionary<LocationId, int>(_roofs);

        /// <summary>Records a simulated household's roof condition (0-100, e.g. after repairs).</summary>
        public void SetRoofCondition(LocationId home, int condition)
        {
            if (!home.IsValid) throw new ArgumentException("A home location is required.", nameof(home));
            if (condition < 0 || condition > 100)
                throw new ArgumentOutOfRangeException(nameof(condition));
            _roofs[home] = condition;
        }

        /// <summary>
        /// A home with no recorded condition counts as sound: the stat must not fail on
        /// unregistered homes, and an unrecorded roof has never been reported damaged.
        /// </summary>
        public int RoofConditionOrDefault(LocationId home)
        {
            if (!home.IsValid) throw new ArgumentException("A home location is required.", nameof(home));
            return _roofs.TryGetValue(home, out int condition) ? condition : 100;
        }
    }
}
