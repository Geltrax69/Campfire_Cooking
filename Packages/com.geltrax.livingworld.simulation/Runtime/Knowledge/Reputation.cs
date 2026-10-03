using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Immutable saved standing for one strongly typed reputation group.</summary>
    public sealed class ReputationStanding
    {
        public ReputationStanding(ReputationGroupId group, int value)
        {
            if (!group.IsValid) throw new ArgumentException("Standing needs a valid group.", nameof(group));
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(nameof(value));
            Group = group;
            Value = value;
        }

        public ReputationGroupId Group { get; }
        public int Value { get; }
    }

    /// <summary>Caller-owned player standing reconstructed and enumerated in deterministic group order.</summary>
    public sealed class ReputationState
    {
        private readonly SortedDictionary<ReputationGroupId, int> _values =
            new SortedDictionary<ReputationGroupId, int>();

        public ReputationState(IEnumerable<ReputationStanding> standings)
        {
            if (standings == null) throw new ArgumentNullException(nameof(standings));
            foreach (ReputationStanding standing in standings)
            {
                if (standing == null) throw new ArgumentException("Standings cannot contain null.", nameof(standings));
                if (_values.ContainsKey(standing.Group))
                    throw new ArgumentException("Each reputation group must appear once.", nameof(standings));
                _values.Add(standing.Group, standing.Value);
            }
        }

        public IReadOnlyList<ReputationStanding> Standings
        {
            get
            {
                var snapshot = new List<ReputationStanding>(_values.Count);
                foreach (var pair in _values) snapshot.Add(new ReputationStanding(pair.Key, pair.Value));
                return snapshot.AsReadOnly();
            }
        }

        public int Get(ReputationGroupId group)
        {
            if (!group.IsValid) throw new ArgumentException("Invalid reputation group.", nameof(group));
            if (!_values.TryGetValue(group, out int value))
                throw new KeyNotFoundException("The reputation group is not registered.");
            return value;
        }

        internal void Set(ReputationGroupId group, int value)
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(nameof(value));
            _values[group] = value;
        }
    }

    /// <summary>Applies bounded player standing changes after their truth event is recorded.</summary>
    public static class ReputationAdjuster
    {
        public static WorldEvent Apply(WorldState world, ReputationState reputation, LocationId location,
            ReputationGroupId group, int requestedDelta)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (reputation == null) throw new ArgumentNullException(nameof(reputation));
            if (!location.IsValid) throw new ArgumentException("A reputation change needs a location.", nameof(location));
            if (!group.IsValid) throw new ArgumentException("A reputation change needs a valid group.", nameof(group));

            int current = reputation.Get(group);
            long requestedValue = (long)current + requestedDelta;
            int finalValue = requestedValue < 0 ? 0 : requestedValue > 100 ? 100 : (int)requestedValue;
            int actualDelta = finalValue - current;
            if (actualDelta == 0) return null;

            WorldEvent worldEvent = world.Events.Append(world.Clock, location, WorldEventType.ReputationChanged,
                ActorId.Player, reputationGroup: group, reputationDelta: actualDelta);
            reputation.Set(group, finalValue);
            return worldEvent;
        }
    }
}
