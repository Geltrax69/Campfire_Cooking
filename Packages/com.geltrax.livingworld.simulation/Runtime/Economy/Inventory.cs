using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Nonnegative aggregate item counts constrained to a validated catalog.</summary>
    public sealed class Inventory
    {
        private readonly ItemCatalog _catalog;
        private readonly SortedDictionary<ItemTypeId, int> _counts;

        public Inventory(ItemCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _counts = new SortedDictionary<ItemTypeId, int>();
        }

        public IReadOnlyList<KeyValuePair<ItemTypeId, int>> Contents
        {
            get
            {
                var snapshot = new List<KeyValuePair<ItemTypeId, int>>(_counts);
                return new ReadOnlyCollection<KeyValuePair<ItemTypeId, int>>(snapshot);
            }
        }

        public int Count(ItemTypeId item)
        {
            _catalog.Require(item);
            return _counts.TryGetValue(item, out int count) ? count : 0;
        }

        public void Add(ItemTypeId item, int quantity)
        {
            _catalog.Require(item);
            RequirePositive(quantity);

            int updated = checked(CountKnown(item) + quantity);
            _counts[item] = updated;
        }

        public bool TryRemove(ItemTypeId item, int quantity)
        {
            _catalog.Require(item);
            RequirePositive(quantity);

            int current = CountKnown(item);
            if (current < quantity) return false;
            SetCount(item, current - quantity);
            return true;
        }

        public bool TransferTo(Inventory destination, ItemTypeId item, int quantity)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            _catalog.Require(item);
            destination._catalog.Require(item);
            RequirePositive(quantity);

            int sourceCount = CountKnown(item);
            if (sourceCount < quantity) return false;
            if (ReferenceEquals(this, destination)) return true;

            int destinationCount = destination.CountKnown(item);
            int updatedDestination = checked(destinationCount + quantity);
            SetCount(item, sourceCount - quantity);
            destination._counts[item] = updatedDestination;
            return true;
        }

        internal void EnsureCanReceive(ItemTypeId item, int quantity)
        {
            _catalog.Require(item);
            RequirePositive(quantity);
            _ = checked(CountKnown(item) + quantity);
        }

        private int CountKnown(ItemTypeId item)
        {
            return _counts.TryGetValue(item, out int count) ? count : 0;
        }

        private void SetCount(ItemTypeId item, int count)
        {
            if (count == 0) _counts.Remove(item);
            else _counts[item] = count;
        }

        private static void RequirePositive(int quantity)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        }
    }
}
