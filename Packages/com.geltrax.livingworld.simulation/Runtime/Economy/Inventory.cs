using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// One lot of a single item type received together: units share an age, so perishables
    /// ripen and spoil lot by lot. Lots leave oldest-first (the shop sells yesterday's
    /// bread before today's), which is also what makes stale pricing honest.
    /// </summary>
    public sealed class StockLotRecord
    {
        public StockLotRecord(ItemTypeId item, int quantity, int ageDays)
        {
            if (!item.IsValid) throw new ArgumentException("An item type is required.", nameof(item));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (ageDays < 0) throw new ArgumentOutOfRangeException(nameof(ageDays));
            Item = item;
            Quantity = quantity;
            AgeDays = ageDays;
        }

        public ItemTypeId Item { get; }
        public int Quantity { get; }
        public int AgeDays { get; }
    }

    /// <summary>Nonnegative aggregate item counts constrained to a validated catalog.</summary>
    public sealed class Inventory
    {
        private sealed class StockLot
        {
            public StockLot(int quantity, int ageDays)
            {
                Quantity = quantity;
                AgeDays = ageDays;
            }

            public int Quantity;
            public int AgeDays;
        }

        private readonly ItemCatalog _catalog;
        private readonly SortedDictionary<ItemTypeId, int> _counts;
        private readonly SortedDictionary<ItemTypeId, Queue<StockLot>> _lots;

        public Inventory(ItemCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _counts = new SortedDictionary<ItemTypeId, int>();
            _lots = new SortedDictionary<ItemTypeId, Queue<StockLot>>();
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
            // New stock arrives fresh: age 0 until the next daily aging.
            EnqueueLot(item, quantity, 0);
        }

        public bool TryRemove(ItemTypeId item, int quantity)
        {
            _catalog.Require(item);
            RequirePositive(quantity);

            int current = CountKnown(item);
            if (current < quantity) return false;
            RemoveOldest(item, quantity);
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

            // The buyer receives the actual goods — oldest lots first, ages intact — so a
            // mushy apple bought at the stall is still a mushy apple in the basket.
            int destinationCount = destination.CountKnown(item);
            _ = checked(destinationCount + quantity);
            MoveOldestTo(destination, item, quantity);
            SetCount(item, sourceCount - quantity);
            destination._counts[item] = destinationCount + quantity;
            return true;
        }

        internal void EnsureCanReceive(ItemTypeId item, int quantity)
        {
            _catalog.Require(item);
            RequirePositive(quantity);
            _ = checked(CountKnown(item) + quantity);
        }

        /// <summary>
        /// The unit price a shop should charge for this item at the given list price: the
        /// shop always sells its oldest lot first, so a stale oldest lot marks the whole
        /// sale down by the item's stale price factor (never below 1 copper).
        /// </summary>
        internal int EffectiveUnitPrice(ItemTypeId item, int listPrice)
        {
            _catalog.Require(item);
            if (listPrice < 1) throw new ArgumentOutOfRangeException(nameof(listPrice));
            PerishableInfo perishable = _catalog[item].Perishable;
            if (perishable == null) return listPrice;
            if (!_lots.TryGetValue(item, out Queue<StockLot> lots) || lots.Count == 0) return listPrice;
            if (perishable.FreshnessAtAge(lots.Peek().AgeDays) == Freshness.Fresh) return listPrice;
            return Math.Max(1, listPrice * perishable.StalePricePercent / 100);
        }

        /// <summary>
        /// Freshness of the oldest lot (the one a removal or sale would take): non-perishable
        /// goods and empty stocks report fresh. The eat system uses this to apply the right
        /// effects for the food actually consumed.
        /// </summary>
        internal Freshness FreshnessOfOldestLot(ItemTypeId item)
        {
            _catalog.Require(item);
            PerishableInfo perishable = _catalog[item].Perishable;
            if (perishable == null) return Freshness.Fresh;
            if (!_lots.TryGetValue(item, out Queue<StockLot> lots) || lots.Count == 0) return Freshness.Fresh;
            return perishable.FreshnessAtAge(lots.Peek().AgeDays);
        }

        /// <summary>
        /// Ages every lot by one day, transitioning lots that cross their fresh and stale
        /// limits. Returns what turned stale and what spoiled (and was removed) so the
        /// caller can log the quiet events. Deterministic: items in sorted ID order.
        /// </summary>
        internal void AgeOneDay(out List<KeyValuePair<ItemTypeId, int>> turnedStale,
            out List<KeyValuePair<ItemTypeId, int>> spoiled)
        {
            turnedStale = new List<KeyValuePair<ItemTypeId, int>>();
            spoiled = new List<KeyValuePair<ItemTypeId, int>>();
            // Snapshot: lots are replaced or removed below, so the live map is not iterated.
            foreach (KeyValuePair<ItemTypeId, Queue<StockLot>> row in
                new List<KeyValuePair<ItemTypeId, Queue<StockLot>>>(_lots))
            {
                PerishableInfo perishable = _catalog[row.Key].Perishable;
                if (perishable == null) continue;
                int staleCount = 0;
                int spoiledCount = 0;
                var kept = new Queue<StockLot>();
                foreach (StockLot lot in row.Value)
                {
                    lot.AgeDays++;
                    if (perishable.IsSpoiledAtAge(lot.AgeDays))
                    {
                        spoiledCount += lot.Quantity;
                        continue;
                    }
                    if (perishable.FreshnessAtAge(lot.AgeDays) == Freshness.Stale &&
                        lot.AgeDays == perishable.FreshDays + 1)
                        staleCount += lot.Quantity;
                    kept.Enqueue(lot);
                }
                if (kept.Count == 0) _lots.Remove(row.Key);
                else
                {
                    _lots[row.Key] = kept;
                }
                if (staleCount > 0)
                    turnedStale.Add(new KeyValuePair<ItemTypeId, int>(row.Key, staleCount));
                if (spoiledCount > 0)
                {
                    spoiled.Add(new KeyValuePair<ItemTypeId, int>(row.Key, spoiledCount));
                    SetCount(row.Key, CountKnown(row.Key) - spoiledCount);
                }
            }
        }

        /// <summary>
        /// Captures every lot for Persistence (P2-12 writes these into the save file).
        /// </summary>
        internal IReadOnlyList<StockLotRecord> CaptureLots()
        {
            var records = new List<StockLotRecord>();
            foreach (KeyValuePair<ItemTypeId, Queue<StockLot>> row in _lots)
                foreach (StockLot lot in row.Value)
                    records.Add(new StockLotRecord(row.Key, lot.Quantity, lot.AgeDays));
            return records.AsReadOnly();
        }

        /// <summary>Installs validated lots for Persistence; replaces all current lots.</summary>
        internal void RestoreLots(IEnumerable<StockLotRecord> lots)
        {
            if (lots == null) throw new ArgumentNullException(nameof(lots));
            var rebuilt = new SortedDictionary<ItemTypeId, Queue<StockLot>>();
            var counts = new SortedDictionary<ItemTypeId, int>();
            foreach (StockLotRecord record in lots)
            {
                if (record == null) throw new ArgumentException("Lot records cannot be null.", nameof(lots));
                _catalog.Require(record.Item);
                if (!rebuilt.TryGetValue(record.Item, out Queue<StockLot> queue))
                {
                    queue = new Queue<StockLot>();
                    rebuilt[record.Item] = queue;
                }
                queue.Enqueue(new StockLot(record.Quantity, record.AgeDays));
                counts[record.Item] = checked(counts.TryGetValue(record.Item, out int c) ? c + record.Quantity : record.Quantity);
            }
            _lots.Clear();
            foreach (KeyValuePair<ItemTypeId, Queue<StockLot>> row in rebuilt)
                _lots[row.Key] = row.Value;
            _counts.Clear();
            foreach (KeyValuePair<ItemTypeId, int> row in counts)
                _counts[row.Key] = row.Value;
        }

        private void EnqueueLot(ItemTypeId item, int quantity, int ageDays)
        {
            if (!_lots.TryGetValue(item, out Queue<StockLot> queue))
            {
                queue = new Queue<StockLot>();
                _lots[item] = queue;
            }
            queue.Enqueue(new StockLot(quantity, ageDays));
        }

        private void RemoveOldest(ItemTypeId item, int quantity)
        {
            Queue<StockLot> queue = _lots[item];
            int remaining = quantity;
            while (remaining > 0)
            {
                StockLot lot = queue.Peek();
                if (lot.Quantity > remaining)
                {
                    lot.Quantity -= remaining;
                    remaining = 0;
                }
                else
                {
                    remaining -= lot.Quantity;
                    queue.Dequeue();
                }
            }
            if (queue.Count == 0) _lots.Remove(item);
        }

        private void MoveOldestTo(Inventory destination, ItemTypeId item, int quantity)
        {
            Queue<StockLot> queue = _lots[item];
            int remaining = quantity;
            while (remaining > 0)
            {
                StockLot lot = queue.Peek();
                if (lot.Quantity > remaining)
                {
                    destination.EnqueueLot(item, remaining, lot.AgeDays);
                    lot.Quantity -= remaining;
                    remaining = 0;
                }
                else
                {
                    remaining -= lot.Quantity;
                    queue.Dequeue();
                    destination.EnqueueLot(item, lot.Quantity, lot.AgeDays);
                }
            }
            if (queue.Count == 0) _lots.Remove(item);
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
