using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Owns every animal's state with unique IDs and deterministic ordinal
    /// iteration. Queries are by species and by location; population counts come
    /// from a live scan (only ~100 animals in the prototype, so a scan is cheap).
    /// Capture/Restore carry the whole store across save/load.
    /// </summary>
    public sealed class AnimalStore
    {
        private readonly SortedList<AnimalId, AnimalState> _animals =
            new SortedList<AnimalId, AnimalState>();
        private readonly IReadOnlyList<AnimalState> _readOnlyAnimals;

        public AnimalStore()
        {
            _readOnlyAnimals = new ReadOnlyCollection<AnimalState>(_animals.Values);
        }

        public int Count => _animals.Count;
        /// <summary>All animals in deterministic AnimalId order.</summary>
        public IReadOnlyList<AnimalState> Animals => _readOnlyAnimals;

        public AnimalState Get(AnimalId id)
        {
            if (!id.IsValid) throw new ArgumentException("An animal lookup needs a valid ID.", nameof(id));
            if (!_animals.TryGetValue(id, out AnimalState animal))
                throw new ArgumentException("Unknown animal ID.", nameof(id));
            return animal;
        }

        public void Add(AnimalState animal)
        {
            if (animal == null) throw new ArgumentNullException(nameof(animal));
            if (_animals.ContainsKey(animal.Id))
                throw new ArgumentException("Animal ID is already registered.", nameof(animal));
            _animals.Add(animal.Id, animal);
        }

        /// <summary>Removes an animal (death/slaughter); false when it was not present.</summary>
        public bool Remove(AnimalId id)
        {
            if (!id.IsValid) return false;
            return _animals.Remove(id);
        }

        /// <summary>Animals of one species, in deterministic AnimalId order.</summary>
        public IReadOnlyList<AnimalState> GetBySpecies(SpeciesId species)
        {
            if (!species.IsValid) throw new ArgumentException("A species lookup needs a valid ID.", nameof(species));
            var result = new List<AnimalState>();
            foreach (AnimalState animal in _animals.Values)
                if (animal.Species == species) result.Add(animal);
            return result.AsReadOnly();
        }

        /// <summary>Animals living at one location, in deterministic AnimalId order.</summary>
        public IReadOnlyList<AnimalState> GetByLocation(LocationId location)
        {
            if (!location.IsValid) throw new ArgumentException("A location lookup needs a valid ID.", nameof(location));
            var result = new List<AnimalState>();
            foreach (AnimalState animal in _animals.Values)
                if (animal.Location == location) result.Add(animal);
            return result.AsReadOnly();
        }

        /// <summary>Live population count for one species.</summary>
        public int PopulationCount(SpeciesId species) => GetBySpecies(species).Count;

        /// <summary>Captures every animal state for Persistence, in deterministic AnimalId order.</summary>
        public IReadOnlyList<AnimalState> Capture() =>
            new List<AnimalState>(_animals.Values).AsReadOnly();

        /// <summary>
        /// Atomically replaces all animal states with a validated snapshot for
        /// Persistence. A rejected snapshot (null, null entry, duplicate ID)
        /// leaves the store unchanged.
        /// </summary>
        public void Restore(IEnumerable<AnimalState> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var staged = new SortedList<AnimalId, AnimalState>();
            foreach (AnimalState animal in snapshot)
            {
                if (animal == null) throw new ArgumentNullException(nameof(snapshot),
                    "Restored animals cannot contain null.");
                if (staged.ContainsKey(animal.Id))
                    throw new ArgumentException(
                        "Restored animal '" + animal.Id + "' appears twice.", nameof(snapshot));
                staged.Add(animal.Id, animal);
            }
            _animals.Clear();
            foreach (KeyValuePair<AnimalId, AnimalState> pair in staged)
                _animals.Add(pair.Key, pair.Value);
        }
    }
}
