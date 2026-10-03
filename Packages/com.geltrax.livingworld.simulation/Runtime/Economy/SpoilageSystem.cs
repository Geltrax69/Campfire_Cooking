using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable caller configuration for the spoilage system.</summary>
    public sealed class SpoilageConfiguration
    {
        public SpoilageConfiguration(string id, ItemCatalog catalog, LocationId defaultLocation,
            EventVisibility visibility)
        {
            ProductionConfiguration.RequireId(id, nameof(id));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!defaultLocation.IsValid) throw new ArgumentException("A default location is required.", nameof(defaultLocation));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            Catalog = catalog;
            DefaultLocation = defaultLocation;
            Visibility = visibility;
        }

        public string Id { get; }
        public ItemCatalog Catalog { get; }
        public LocationId DefaultLocation { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of spoilage progress: the last day every lot was
    /// aged. Starts uninitialized; the system stays quiet until the world-build step
    /// installs it. P2-12 extends the saver to write this state; until then use
    /// RestoreSpoilage. Lot ages themselves live inside each Inventory (see
    /// Inventory.CaptureLots) and are restored alongside it.
    /// </summary>
    public sealed class SpoilageState
    {
        public SpoilageState(bool initialized = false, long lastAgedDay = 0)
        {
            if (initialized && lastAgedDay < 1)
                throw new ArgumentOutOfRangeException(nameof(lastAgedDay));
            IsInitialized = initialized;
            LastAgedDay = lastAgedDay;
        }

        public bool IsInitialized { get; }
        public long LastAgedDay { get; internal set; }
    }

    /// <summary>
    /// Ages every perishable lot in the village once per day (economy.spoilage, Economy
    /// phase): fresh → stale → spoiled, per the approved `perishable` block in
    /// Content/items/items.json. Transitions are quiet log entries — the shopkeeper
    /// discovering mushy apples — and spoiled lots are removed to compost or feed, their
    /// last realizable value recorded on the event for the money audit. Inventories are
    /// visited in deterministic order (belongings by actor, then shops by location) and
    /// deduplicated by reference, so a stock reachable two ways ages exactly once.
    /// </summary>
    public sealed class SpoilageSystem : IWorldSystem
    {
        private readonly List<SpoilageConfiguration> _configurations;

        public SpoilageSystem(IEnumerable<SpoilageConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<SpoilageConfiguration>();
            foreach (SpoilageConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Spoilage configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "economy.spoilage";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Spoilage.IsInitialized) return;
            foreach (SpoilageConfiguration configuration in _configurations)
            {
                while (state.Spoilage.LastAgedDay < state.Clock.Day)
                {
                    state.Spoilage.LastAgedDay++;
                    AgeAll(state, configuration);
                }
            }
        }

        private static void AgeAll(WorldState state, SpoilageConfiguration configuration)
        {
            var seen = new HashSet<Inventory>();
            foreach (NpcBelongingsEntry entry in state.Belongings.Entries)
            {
                if (seen.Add(entry.Inventory))
                    AgeInventory(state, configuration, entry.Inventory,
                        entry.Owner, configuration.DefaultLocation);
            }
            foreach (Shop shop in state.Shops.Shops)
            {
                if (seen.Add(shop.Stock))
                    AgeInventory(state, configuration, shop.Stock,
                        ActorId.ForNpc(shop.Owner), shop.Location);
            }
        }

        private static void AgeInventory(WorldState state, SpoilageConfiguration configuration,
            Inventory inventory, ActorId owner, LocationId location)
        {
            inventory.AgeOneDay(out List<KeyValuePair<ItemTypeId, int>> turnedStale,
                out List<KeyValuePair<ItemTypeId, int>> spoiled);
            foreach (KeyValuePair<ItemTypeId, int> row in turnedStale)
                state.Events.Append(state.Clock, location, WorldEventType.TurnedStale, owner,
                    visibility: configuration.Visibility, itemType: row.Key, quantity: row.Value);
            foreach (KeyValuePair<ItemTypeId, int> row in spoiled)
                state.Events.Append(state.Clock, location, WorldEventType.Spoiled, owner,
                    visibility: configuration.Visibility, itemType: row.Key, quantity: row.Value,
                    copper: DestroyedValue(configuration.Catalog, row.Key, row.Value));
        }

        private static int DestroyedValue(ItemCatalog catalog, ItemTypeId item, int quantity)
        {
            // The value destroyed is the last realizable one: the stale price, the same
            // markdown a shop would have offered yesterday.
            ItemDefinition definition = catalog[item];
            PerishableInfo perishable = definition.Perishable;
            int staleUnit = perishable == null ? definition.BaseValue :
                Math.Max(1, definition.BaseValue * perishable.StalePricePercent / 100);
            return checked(quantity * staleUnit);
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpoilageConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Spoilage configuration IDs must be unique.", "configurations");
        }
    }
}
