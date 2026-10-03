using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Controls whether a restock order requires full delivery or permits one partial delivery.</summary>
    public enum RestockFulfillmentPolicy { FullOnly, AllowPartial }

    /// <summary>Immutable caller configuration for one shop-to-producer wholesale route.</summary>
    public sealed class RestockConfiguration
    {
        public RestockConfiguration(string id, Shop shop, LocationId producerLocation, NpcId producer,
            Inventory producerStock, Wallet producerWallet, ItemTypeId item, int lowStockThreshold,
            int orderQuantity, int wholesaleUnitPrice, RestockFulfillmentPolicy policy,
            EventVisibility visibility)
        {
            RequireId(id, nameof(id));
            if (shop == null) throw new ArgumentNullException(nameof(shop));
            if (!producerLocation.IsValid)
                throw new ArgumentException("A producer location is required.", nameof(producerLocation));
            if (!producer.IsValid) throw new ArgumentException("A producer is required.", nameof(producer));
            if (producerStock == null) throw new ArgumentNullException(nameof(producerStock));
            if (producerWallet == null) throw new ArgumentNullException(nameof(producerWallet));
            if (!item.IsValid) throw new ArgumentException("A restock item is required.", nameof(item));
            shop.UnitPrice(item);
            producerStock.Count(item);
            if (ReferenceEquals(shop.Stock, producerStock))
                throw new ArgumentException("Producer and shop inventories must be distinct.", nameof(producerStock));
            if (ReferenceEquals(shop.OwnerWallet, producerWallet))
                throw new ArgumentException("Producer and shop wallets must be distinct.", nameof(producerWallet));
            if (lowStockThreshold < 0) throw new ArgumentOutOfRangeException(nameof(lowStockThreshold));
            if (orderQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(orderQuantity));
            if (wholesaleUnitPrice <= 0) throw new ArgumentOutOfRangeException(nameof(wholesaleUnitPrice));
            _ = checked(orderQuantity * wholesaleUnitPrice);
            if (policy < RestockFulfillmentPolicy.FullOnly || policy > RestockFulfillmentPolicy.AllowPartial)
                throw new ArgumentOutOfRangeException(nameof(policy));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            Shop = shop;
            ProducerLocation = producerLocation;
            Producer = producer;
            ProducerStock = producerStock;
            ProducerWallet = producerWallet;
            Item = item;
            LowStockThreshold = lowStockThreshold;
            OrderQuantity = orderQuantity;
            WholesaleUnitPrice = wholesaleUnitPrice;
            Policy = policy;
            Visibility = visibility;
        }

        public string Id { get; }
        public Shop Shop { get; }
        public LocationId ProducerLocation { get; }
        public NpcId Producer { get; }
        public Inventory ProducerStock { get; }
        public Wallet ProducerWallet { get; }
        public ItemTypeId Item { get; }
        public int LowStockThreshold { get; }
        public int OrderQuantity { get; }
        public int WholesaleUnitPrice { get; }
        public RestockFulfillmentPolicy Policy { get; }
        public EventVisibility Visibility { get; }

        internal static void RequireId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A stable restock configuration ID is required.", parameterName);
        }
    }

    /// <summary>Immutable persistence data for an unfulfilled restock request.</summary>
    public sealed class PendingRestockOrder
    {
        public PendingRestockOrder(string configurationId, GameTime requestedAt)
        {
            RestockConfiguration.RequireId(configurationId, nameof(configurationId));
            ConfigurationId = configurationId;
            RequestedAt = requestedAt;
        }

        public string ConfigurationId { get; }
        public GameTime RequestedAt { get; }
    }

    /// <summary>Caller-owned, restorable low-stock triggers and pending restock orders.</summary>
    public sealed class RestockState
    {
        private readonly SortedSet<string> _triggeredIds;
        private readonly SortedDictionary<string, PendingRestockOrder> _pending;

        public RestockState(IEnumerable<string> triggeredIds = null,
            IEnumerable<PendingRestockOrder> pendingOrders = null)
        {
            _triggeredIds = CopyIds(triggeredIds);
            _pending = new SortedDictionary<string, PendingRestockOrder>(StringComparer.Ordinal);
            if (pendingOrders == null) return;
            foreach (PendingRestockOrder order in pendingOrders)
            {
                if (order == null) throw new ArgumentException("Pending orders cannot contain null.", nameof(pendingOrders));
                if (_pending.ContainsKey(order.ConfigurationId))
                    throw new ArgumentException("Only one pending order is allowed per configuration.", nameof(pendingOrders));
                _pending.Add(order.ConfigurationId, order);
            }
        }

        public IReadOnlyList<string> TriggeredIds =>
            new ReadOnlyCollection<string>(new List<string>(_triggeredIds));
        public IReadOnlyList<PendingRestockOrder> PendingOrders =>
            new ReadOnlyCollection<PendingRestockOrder>(new List<PendingRestockOrder>(_pending.Values));

        internal bool IsTriggered(string id) => _triggeredIds.Contains(id);
        internal void Trigger(string id) => _triggeredIds.Add(id);
        internal void ClearTrigger(string id) => _triggeredIds.Remove(id);
        internal bool HasPending(string id) => _pending.ContainsKey(id);
        internal void AddPending(PendingRestockOrder order) => _pending.Add(order.ConfigurationId, order);
        internal bool TryGetPending(string id) => _pending.ContainsKey(id);
        internal void RemovePending(string id) => _pending.Remove(id);

        private static SortedSet<string> CopyIds(IEnumerable<string> ids)
        {
            var copied = new SortedSet<string>(StringComparer.Ordinal);
            if (ids == null) return copied;
            foreach (string id in ids)
            {
                RestockConfiguration.RequireId(id, nameof(ids));
                if (!copied.Add(id)) throw new ArgumentException("Triggered IDs must be unique.", nameof(ids));
            }
            return copied;
        }
    }

    /// <summary>Creates and atomically fulfills wholesale orders using world-owned restock state.</summary>
    public sealed class RestockSystem : IWorldSystem
    {
        private readonly List<RestockConfiguration> _configurations;

        public RestockSystem(IEnumerable<RestockConfiguration> configurations, WorldState world)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            if (world == null) throw new ArgumentNullException(nameof(world));
            _configurations = new List<RestockConfiguration>();
            foreach (RestockConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Restock configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort(Compare);
            ValidateConfigurations();
            var known = new HashSet<string>(_configurations.ConvertAll(configuration => configuration.Id),
                StringComparer.Ordinal);
            foreach (string id in world.Restock.TriggeredIds)
                if (!known.Contains(id)) throw new ArgumentException("Restock state references unknown trigger.");
            foreach (PendingRestockOrder order in world.Restock.PendingOrders)
                if (!known.Contains(order.ConfigurationId))
                    throw new ArgumentException("Restock state references unknown pending order.");
        }

        public string Id => "economy.restocking";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            CreateOrders(state);
            FulfillOrders(state);
        }

        private void CreateOrders(WorldState state)
        {
            RestockState progress = state.Restock;
            foreach (RestockConfiguration configuration in _configurations)
            {
                if (configuration.Shop.Stock.Count(configuration.Item) >= configuration.LowStockThreshold)
                {
                    if (!progress.HasPending(configuration.Id)) progress.ClearTrigger(configuration.Id);
                    continue;
                }
                if (progress.IsTriggered(configuration.Id) || progress.HasPending(configuration.Id)) continue;
                int copper = checked(configuration.OrderQuantity * configuration.WholesaleUnitPrice);
                state.Events.Append(state.Clock, configuration.Shop.Location, WorldEventType.RestockOrdered,
                    ActorId.ForNpc(configuration.Shop.Owner), new[] { ActorId.ForNpc(configuration.Producer) },
                    configuration.Visibility, configuration.Item, configuration.OrderQuantity, copper);
                progress.AddPending(new PendingRestockOrder(configuration.Id, state.Clock));
                progress.Trigger(configuration.Id);
            }
        }

        private void FulfillOrders(WorldState state)
        {
            RestockState progress = state.Restock;
            foreach (RestockConfiguration configuration in _configurations)
            {
                if (!progress.TryGetPending(configuration.Id)) continue;
                int available = configuration.ProducerStock.Count(configuration.Item);
                int affordable = configuration.Shop.OwnerWallet.Balance / configuration.WholesaleUnitPrice;
                int actual = Math.Min(configuration.OrderQuantity, Math.Min(available, affordable));
                if (configuration.Policy == RestockFulfillmentPolicy.FullOnly
                    && actual != configuration.OrderQuantity) continue;
                if (actual == 0) continue;
                int copper = checked(actual * configuration.WholesaleUnitPrice);
                configuration.Shop.Stock.EnsureCanReceive(configuration.Item, actual);
                configuration.ProducerWallet.EnsureCanReceive(copper);
                state.Events.Append(state.Clock, configuration.Shop.Location, WorldEventType.Restocked,
                    ActorId.ForNpc(configuration.Producer), new[] { ActorId.ForNpc(configuration.Shop.Owner) },
                    configuration.Visibility, configuration.Item, actual, copper);
                if (!configuration.ProducerStock.TransferTo(configuration.Shop.Stock, configuration.Item, actual))
                    throw new InvalidOperationException("Preflighted restock inventory transfer unexpectedly failed.");
                if (!configuration.Shop.OwnerWallet.TransferTo(configuration.ProducerWallet, copper))
                    throw new InvalidOperationException("Preflighted restock copper transfer unexpectedly failed.");
                progress.RemovePending(configuration.Id);
            }
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (RestockConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Restock configuration IDs must be unique.", "configurations");
            for (int index = 1; index < _configurations.Count; index++)
                if (SameRoute(_configurations[index - 1], _configurations[index]))
                    throw new ArgumentException("Restock routes must be unique.", "configurations");
        }

        private static int Compare(RestockConfiguration left, RestockConfiguration right)
        {
            int comparison = left.Shop.Location.CompareTo(right.Shop.Location);
            if (comparison != 0) return comparison;
            comparison = left.Item.CompareTo(right.Item);
            if (comparison != 0) return comparison;
            comparison = left.ProducerLocation.CompareTo(right.ProducerLocation);
            if (comparison != 0) return comparison;
            comparison = left.Producer.CompareTo(right.Producer);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.Id, right.Id);
        }

        private static bool SameRoute(RestockConfiguration left, RestockConfiguration right) =>
            left.Shop.Location == right.Shop.Location && left.Item == right.Item
            && left.ProducerLocation == right.ProducerLocation && left.Producer == right.Producer;
    }
}
