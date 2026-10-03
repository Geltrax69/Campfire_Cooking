using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Owns every shop in the world, keyed by shop location in ordinal order.</summary>
    public sealed class ShopRegistry
    {
        private readonly SortedDictionary<LocationId, Shop> _shops =
            new SortedDictionary<LocationId, Shop>();

        /// <summary>All registered shops in ordinal location order.</summary>
        public IReadOnlyList<Shop> Shops => new List<Shop>(_shops.Values).AsReadOnly();

        /// <summary>
        /// Registers a shop exactly once. A location hosts at most one shop in the prototype;
        /// a second registration at the same location is rejected.
        /// </summary>
        public void Register(Shop shop)
        {
            if (shop == null) throw new ArgumentNullException(nameof(shop));
            if (_shops.ContainsKey(shop.Location))
                throw new ArgumentException("A shop is already registered at this location.", nameof(shop));
            _shops.Add(shop.Location, shop);
        }

        public Shop this[LocationId location]
        {
            get
            {
                if (!location.IsValid)
                    throw new ArgumentException("A shop lookup needs a valid location.", nameof(location));
                if (!_shops.TryGetValue(location, out Shop shop))
                    throw new ArgumentException("No shop is registered at this location.", nameof(location));
                return shop;
            }
        }

        public bool TryGet(LocationId location, out Shop shop)
        {
            if (!location.IsValid)
                throw new ArgumentException("A shop lookup needs a valid location.", nameof(location));
            return _shops.TryGetValue(location, out shop);
        }

        /// <summary>
        /// Replaces every shop with a validated batch for Persistence. Everything is validated
        /// before anything changes; a failed restore leaves the registry untouched.
        /// </summary>
        internal void Restore(IEnumerable<Shop> shops)
        {
            if (shops == null) throw new ArgumentNullException(nameof(shops));
            var validated = new SortedDictionary<LocationId, Shop>();
            foreach (Shop shop in shops)
            {
                if (shop == null) throw new ArgumentException("Restored shops cannot contain null.", nameof(shops));
                if (validated.ContainsKey(shop.Location))
                    throw new ArgumentException("Restored shop locations must be unique.", nameof(shops));
                validated.Add(shop.Location, shop);
            }
            _shops.Clear();
            foreach (var pair in validated) _shops.Add(pair.Key, pair.Value);
        }
    }
}
