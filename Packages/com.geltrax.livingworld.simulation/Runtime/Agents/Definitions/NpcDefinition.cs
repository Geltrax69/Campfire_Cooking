using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Immutable approved identity and tuning data for one NPC.</summary>
    public sealed class NpcDefinition
    {
        public NpcDefinition(NpcId id, string name, int age, string gender, string occupation,
            LocationId home, LocationId workplace, int startingMoneyCopper,
            IEnumerable<KeyValuePair<string, int>> traits, NeedRates needRates)
        {
            if (!id.IsValid) throw new ArgumentException("NPC ID must be valid.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
            if (age < 0) throw new ArgumentOutOfRangeException(nameof(age));
            if (string.IsNullOrWhiteSpace(gender)) throw new ArgumentException("Gender is required.", nameof(gender));
            if (string.IsNullOrWhiteSpace(occupation)) throw new ArgumentException("Occupation is required.", nameof(occupation));
            if (!home.IsValid) throw new ArgumentException("Home must be valid.", nameof(home));
            if (!workplace.IsValid) throw new ArgumentException("Workplace must be valid.", nameof(workplace));
            if (startingMoneyCopper < 0) throw new ArgumentOutOfRangeException(nameof(startingMoneyCopper));
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            if (needRates == null) throw new ArgumentNullException(nameof(needRates));

            var sortedTraits = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var trait in traits)
            {
                if (string.IsNullOrWhiteSpace(trait.Key))
                    throw new ArgumentException("Trait names must not be empty.", nameof(traits));
                if (trait.Value < 0 || trait.Value > 100)
                    throw new ArgumentOutOfRangeException(nameof(traits), "Trait values must be from 0 through 100.");
                if (sortedTraits.ContainsKey(trait.Key))
                    throw new ArgumentException("Trait names must be unique.", nameof(traits));
                sortedTraits.Add(trait.Key, trait.Value);
            }

            Id = id;
            Name = name;
            Age = age;
            Gender = gender;
            Occupation = occupation;
            Home = home;
            Workplace = workplace;
            StartingMoneyCopper = startingMoneyCopper;
            Traits = new ReadOnlyDictionary<string, int>(sortedTraits);
            NeedRates = needRates;
        }

        public NpcId Id { get; }
        public string Name { get; }
        public int Age { get; }
        public string Gender { get; }
        public string Occupation { get; }
        public LocationId Home { get; }
        public LocationId Workplace { get; }
        public int StartingMoneyCopper { get; }
        public IReadOnlyDictionary<string, int> Traits { get; }
        public NeedRates NeedRates { get; }
    }
}
