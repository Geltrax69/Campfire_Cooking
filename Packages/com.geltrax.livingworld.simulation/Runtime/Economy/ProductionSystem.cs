using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable caller configuration for one production occurrence.</summary>
    public sealed class ProductionConfiguration
    {
        public ProductionConfiguration(string id, LocationId location, NpcId producer, Inventory inventory,
            ItemTypeId item, int quantity, GameTime eligibleAt, EventVisibility visibility)
        {
            RequireId(id, nameof(id));
            if (!location.IsValid) throw new ArgumentException("A production location is required.", nameof(location));
            if (!producer.IsValid) throw new ArgumentException("A producer is required.", nameof(producer));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (!item.IsValid) throw new ArgumentException("A produced item is required.", nameof(item));
            inventory.Count(item);
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            Location = location;
            Producer = producer;
            Inventory = inventory;
            Item = item;
            Quantity = quantity;
            EligibleAt = eligibleAt;
            Visibility = visibility;
        }

        public string Id { get; }
        public LocationId Location { get; }
        public NpcId Producer { get; }
        public Inventory Inventory { get; }
        public ItemTypeId Item { get; }
        public int Quantity { get; }
        public GameTime EligibleAt { get; }
        public EventVisibility Visibility { get; }

        internal static void RequireId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A stable production configuration ID is required.", parameterName);
        }
    }

    /// <summary>Caller-owned, restorable record of completed production configurations.</summary>
    public sealed class ProductionState
    {
        private readonly SortedSet<string> _completedIds;

        public ProductionState(IEnumerable<string> completedIds = null)
        {
            _completedIds = new SortedSet<string>(StringComparer.Ordinal);
            if (completedIds == null) return;
            foreach (string id in completedIds)
            {
                ProductionConfiguration.RequireId(id, nameof(completedIds));
                if (!_completedIds.Add(id))
                    throw new ArgumentException("Completed production IDs must be unique.", nameof(completedIds));
            }
        }

        public IReadOnlyList<string> CompletedIds =>
            new ReadOnlyCollection<string>(new List<string>(_completedIds));

        internal bool IsCompleted(string id) => _completedIds.Contains(id);
        internal void Complete(string id) => _completedIds.Add(id);
    }

    /// <summary>Applies configured production once while storing completion in world-owned state.</summary>
    public sealed class ProductionSystem : IWorldSystem
    {
        private readonly List<ProductionConfiguration> _configurations;

        public ProductionSystem(IEnumerable<ProductionConfiguration> configurations, WorldState world)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            if (world == null) throw new ArgumentNullException(nameof(world));
            _configurations = new List<ProductionConfiguration>();
            foreach (ProductionConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Production configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort(Compare);
            ValidateConfigurations();
            var known = new HashSet<string>(_configurations.ConvertAll(configuration => configuration.Id),
                StringComparer.Ordinal);
            foreach (string id in world.Production.CompletedIds)
                if (!known.Contains(id)) throw new ArgumentException("Production state references unknown configuration.");
        }

        public string Id => "economy.production";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            ProductionState progress = state.Production;
            foreach (ProductionConfiguration configuration in _configurations)
            {
                if (progress.IsCompleted(configuration.Id) || state.Clock < configuration.EligibleAt) continue;
                configuration.Inventory.EnsureCanReceive(configuration.Item, configuration.Quantity);
                state.Events.Append(state.Clock, configuration.Location, WorldEventType.Produced,
                    ActorId.ForNpc(configuration.Producer), visibility: configuration.Visibility,
                    itemType: configuration.Item, quantity: configuration.Quantity);
                configuration.Inventory.Add(configuration.Item, configuration.Quantity);
                progress.Complete(configuration.Id);
            }
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProductionConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Production configuration IDs must be unique.", "configurations");
            for (int index = 1; index < _configurations.Count; index++)
                if (SameOccurrence(_configurations[index - 1], _configurations[index]))
                    throw new ArgumentException("Production occurrences must be unique.", "configurations");
        }

        private static int Compare(ProductionConfiguration left, ProductionConfiguration right)
        {
            int comparison = left.Location.CompareTo(right.Location);
            if (comparison != 0) return comparison;
            comparison = left.Item.CompareTo(right.Item);
            if (comparison != 0) return comparison;
            comparison = left.Producer.CompareTo(right.Producer);
            if (comparison != 0) return comparison;
            comparison = left.EligibleAt.CompareTo(right.EligibleAt);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.Id, right.Id);
        }

        private static bool SameOccurrence(ProductionConfiguration left, ProductionConfiguration right) =>
            left.Location == right.Location && left.Item == right.Item && left.Producer == right.Producer
            && left.EligibleAt == right.EligibleAt;
    }
}
