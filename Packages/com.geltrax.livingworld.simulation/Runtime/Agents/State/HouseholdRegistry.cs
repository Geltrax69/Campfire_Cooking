using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Owns households with unique IDs and deterministic ordinal iteration.</summary>
    public sealed class HouseholdRegistry
    {
        private readonly SortedList<HouseholdId, Household> _households = new SortedList<HouseholdId, Household>();
        private readonly IReadOnlyList<Household> _readOnlyHouseholds;

        public HouseholdRegistry()
        {
            _readOnlyHouseholds = new ReadOnlyCollection<Household>(_households.Values);
        }

        public int Count => _households.Count;
        public IReadOnlyList<Household> Households => _readOnlyHouseholds;

        public bool Contains(HouseholdId id) => id.IsValid && _households.ContainsKey(id);

        public Household this[HouseholdId id]
        {
            get
            {
                if (!id.IsValid) throw new ArgumentException("Household ID must be valid.", nameof(id));
                if (!_households.TryGetValue(id, out var household))
                    throw new ArgumentException("Unknown household ID.", nameof(id));
                return household;
            }
        }

        public void Register(Household household)
        {
            if (household == null) throw new ArgumentNullException(nameof(household));
            var id = household.Id;
            if (_households.ContainsKey(id)) throw new ArgumentException("Household ID is already registered.", nameof(household));
            _households.Add(id, household);
        }
    }
}
