using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable caller tuning for one shop item price.</summary>
    public sealed class PriceAdjustmentConfiguration
    {
        public PriceAdjustmentConfiguration(string id, Shop shop, ItemTypeId item,
            GameTime firstAdjustmentAt, int intervalMinutes, int lowStockThreshold,
            int highStockThreshold, int priceStep, int minimumPrice, int maximumPrice,
            EventVisibility visibility)
        {
            RequireId(id, nameof(id));
            if (shop == null) throw new ArgumentNullException(nameof(shop));
            if (!item.IsValid) throw new ArgumentException("A priced item is required.", nameof(item));
            int currentPrice = shop.UnitPrice(item);
            if (intervalMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMinutes));
            if (lowStockThreshold < 0) throw new ArgumentOutOfRangeException(nameof(lowStockThreshold));
            if (highStockThreshold <= lowStockThreshold)
                throw new ArgumentException("The high-stock threshold must exceed the low-stock threshold.",
                    nameof(highStockThreshold));
            if (priceStep <= 0) throw new ArgumentOutOfRangeException(nameof(priceStep));
            if (minimumPrice <= 0) throw new ArgumentOutOfRangeException(nameof(minimumPrice));
            if (maximumPrice < minimumPrice)
                throw new ArgumentException("The maximum price must not be below the minimum.", nameof(maximumPrice));
            if (currentPrice < minimumPrice || currentPrice > maximumPrice)
                throw new ArgumentException("The current shop price must be within configured bounds.", nameof(shop));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            Shop = shop;
            Item = item;
            FirstAdjustmentAt = firstAdjustmentAt;
            IntervalMinutes = intervalMinutes;
            LowStockThreshold = lowStockThreshold;
            HighStockThreshold = highStockThreshold;
            PriceStep = priceStep;
            MinimumPrice = minimumPrice;
            MaximumPrice = maximumPrice;
            Visibility = visibility;
        }

        public string Id { get; }
        public Shop Shop { get; }
        public ItemTypeId Item { get; }
        public GameTime FirstAdjustmentAt { get; }
        public int IntervalMinutes { get; }
        public int LowStockThreshold { get; }
        public int HighStockThreshold { get; }
        public int PriceStep { get; }
        public int MinimumPrice { get; }
        public int MaximumPrice { get; }
        public EventVisibility Visibility { get; }

        internal static void RequireId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A stable price configuration ID is required.", parameterName);
        }
    }

    /// <summary>Immutable persistence snapshot for one price configuration.</summary>
    public sealed class PriceAdjustmentProgress
    {
        public PriceAdjustmentProgress(string configurationId, long lastProcessedEventId,
            long completedInterval, bool missedSalePending)
        {
            PriceAdjustmentConfiguration.RequireId(configurationId, nameof(configurationId));
            if (lastProcessedEventId < 0) throw new ArgumentOutOfRangeException(nameof(lastProcessedEventId));
            if (completedInterval < 0) throw new ArgumentOutOfRangeException(nameof(completedInterval));
            ConfigurationId = configurationId;
            LastProcessedEventId = lastProcessedEventId;
            CompletedInterval = completedInterval;
            MissedSalePending = missedSalePending;
        }

        public string ConfigurationId { get; }
        public long LastProcessedEventId { get; }
        public long CompletedInterval { get; }
        public bool MissedSalePending { get; }
    }

    /// <summary>Caller-owned, restorable event cursors and completed price intervals.</summary>
    public sealed class PriceAdjustmentState
    {
        private readonly SortedDictionary<string, PriceAdjustmentProgress> _progress;

        public PriceAdjustmentState(IEnumerable<PriceAdjustmentProgress> progress = null)
        {
            _progress = new SortedDictionary<string, PriceAdjustmentProgress>(StringComparer.Ordinal);
            if (progress == null) return;
            foreach (PriceAdjustmentProgress entry in progress)
            {
                if (entry == null) throw new ArgumentException("Price progress cannot contain null.", nameof(progress));
                if (_progress.ContainsKey(entry.ConfigurationId))
                    throw new ArgumentException("Price progress IDs must be unique.", nameof(progress));
                _progress.Add(entry.ConfigurationId, entry);
            }
        }

        public IReadOnlyList<PriceAdjustmentProgress> Progress =>
            new ReadOnlyCollection<PriceAdjustmentProgress>(new List<PriceAdjustmentProgress>(_progress.Values));

        internal PriceAdjustmentProgress Get(string id)
        {
            return _progress.TryGetValue(id, out PriceAdjustmentProgress progress)
                ? progress : new PriceAdjustmentProgress(id, 0, 0, false);
        }

        internal void Set(PriceAdjustmentProgress progress) => _progress[progress.ConfigurationId] = progress;
    }

    /// <summary>Adjusts bounded shop prices from stock and purchase truth at configured intervals.</summary>
    public sealed class PriceAdjustmentSystem : IWorldSystem
    {
        private readonly List<PriceAdjustmentConfiguration> _configurations;
        private readonly PriceAdjustmentState _state;

        public PriceAdjustmentSystem(IEnumerable<PriceAdjustmentConfiguration> configurations,
            PriceAdjustmentState state)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _configurations = new List<PriceAdjustmentConfiguration>();
            foreach (PriceAdjustmentConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Price configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort(Compare);
            ValidateConfigurations();
            var known = new HashSet<string>(_configurations.ConvertAll(configuration => configuration.Id),
                StringComparer.Ordinal);
            foreach (PriceAdjustmentProgress progress in _state.Progress)
                if (!known.Contains(progress.ConfigurationId))
                    throw new ArgumentException("Price state references an unknown configuration.");
        }

        public string Id => "economy.prices";
        public SimulationPhase Phase => SimulationPhase.Economy;
        public PriceAdjustmentState State => _state;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (PriceAdjustmentConfiguration configuration in _configurations)
                Process(configuration, state);
        }

        private void Process(PriceAdjustmentConfiguration configuration, WorldState world)
        {
            PriceAdjustmentProgress prior = _state.Get(configuration.Id);
            long cursor = prior.LastProcessedEventId;
            bool missedSale = prior.MissedSalePending;
            foreach (WorldEvent worldEvent in world.Events.Query())
            {
                if (worldEvent.Id.Value <= prior.LastProcessedEventId) continue;
                cursor = worldEvent.Id.Value;
                if ((worldEvent.Type == WorldEventType.FailedPurchase
                    || worldEvent.Type == WorldEventType.PartialPurchase)
                    && worldEvent.Location == configuration.Shop.Location
                    && worldEvent.ItemType == configuration.Item)
                    missedSale = true;
            }

            long interval = CurrentInterval(configuration, world.Clock);
            if (interval == 0 || interval <= prior.CompletedInterval)
            {
                _state.Set(new PriceAdjustmentProgress(configuration.Id, cursor,
                    prior.CompletedInterval, missedSale));
                return;
            }

            int currentPrice = configuration.Shop.UnitPrice(configuration.Item);
            int stock = configuration.Shop.Stock.Count(configuration.Item);
            int adjustedPrice = currentPrice;
            if (missedSale || stock < configuration.LowStockThreshold)
                adjustedPrice = (int)Math.Min(configuration.MaximumPrice,
                    (long)currentPrice + configuration.PriceStep);
            else if (stock > configuration.HighStockThreshold)
                adjustedPrice = (int)Math.Max(configuration.MinimumPrice,
                    (long)currentPrice - configuration.PriceStep);

            if (adjustedPrice != currentPrice)
            {
                WorldEvent changed = world.Events.Append(world.Clock, configuration.Shop.Location,
                    WorldEventType.PriceChanged, ActorId.ForNpc(configuration.Shop.Owner),
                    visibility: configuration.Visibility, itemType: configuration.Item, copper: adjustedPrice);
                configuration.Shop.SetUnitPrice(configuration.Item, adjustedPrice);
                cursor = changed.Id.Value;
            }
            _state.Set(new PriceAdjustmentProgress(configuration.Id, cursor, interval, false));
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PriceAdjustmentConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Price configuration IDs must be unique.", "configurations");
            for (int index = 1; index < _configurations.Count; index++)
                if (_configurations[index - 1].Shop.Location == _configurations[index].Shop.Location
                    && _configurations[index - 1].Item == _configurations[index].Item)
                    throw new ArgumentException("Each shop item can have only one price configuration.",
                        "configurations");
        }

        private static long CurrentInterval(PriceAdjustmentConfiguration configuration, GameTime time)
        {
            if (time < configuration.FirstAdjustmentAt) return 0;
            return (time.TotalMinutes - configuration.FirstAdjustmentAt.TotalMinutes)
                / configuration.IntervalMinutes + 1;
        }

        private static int Compare(PriceAdjustmentConfiguration left, PriceAdjustmentConfiguration right)
        {
            int comparison = left.Shop.Location.CompareTo(right.Shop.Location);
            if (comparison != 0) return comparison;
            comparison = left.Item.CompareTo(right.Item);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.Id, right.Id);
        }
    }
}
