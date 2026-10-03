using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Classifies whether a requested purchase was fully, partly or not fulfilled.</summary>
    public enum PurchaseOutcome { Full, Partial, Failed }

    /// <summary>Immutable buyer state and intent supplied to one shop purchase.</summary>
    public sealed class PurchaseRequest
    {
        public PurchaseRequest(ActorId buyer, Inventory inventory, Wallet wallet,
            ItemTypeId item, int requestedQuantity, bool allowPartial)
        {
            if (!buyer.IsValid) throw new ArgumentException("A valid buyer is required.", nameof(buyer));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (!item.IsValid) throw new ArgumentException("A valid item type is required.", nameof(item));
            if (requestedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(requestedQuantity));
            Buyer = buyer;
            Inventory = inventory;
            Wallet = wallet;
            Item = item;
            RequestedQuantity = requestedQuantity;
            AllowPartial = allowPartial;
        }

        public ActorId Buyer { get; }
        public Inventory Inventory { get; }
        public Wallet Wallet { get; }
        public ItemTypeId Item { get; }
        public int RequestedQuantity { get; }
        public bool AllowPartial { get; }
    }

    /// <summary>Immutable quantities and copper committed by a purchase attempt.</summary>
    public sealed class PurchaseResult
    {
        internal PurchaseResult(PurchaseOutcome outcome, int requestedQuantity, int actualQuantity, int paidCopper)
        {
            Outcome = outcome;
            RequestedQuantity = requestedQuantity;
            ActualQuantity = actualQuantity;
            PaidCopper = paidCopper;
        }

        public PurchaseOutcome Outcome { get; }
        public int RequestedQuantity { get; }
        public int ActualQuantity { get; }
        public int PaidCopper { get; }
    }

    /// <summary>Caller-owned shop stock, wallet and immutable ordinal unit-price list.</summary>
    public sealed class Shop
    {
        private readonly Dictionary<ItemTypeId, int> _prices;

        public Shop(LocationId location, NpcId owner, Inventory stock, Wallet ownerWallet,
            IEnumerable<KeyValuePair<ItemTypeId, int>> prices)
        {
            if (!location.IsValid) throw new ArgumentException("A valid shop location is required.", nameof(location));
            if (!owner.IsValid) throw new ArgumentException("A valid NPC owner is required.", nameof(owner));
            if (stock == null) throw new ArgumentNullException(nameof(stock));
            if (ownerWallet == null) throw new ArgumentNullException(nameof(ownerWallet));
            if (prices == null) throw new ArgumentNullException(nameof(prices));

            var ordered = new SortedDictionary<ItemTypeId, int>();
            foreach (KeyValuePair<ItemTypeId, int> price in prices)
            {
                stock.Count(price.Key);
                if (price.Value <= 0) throw new ArgumentOutOfRangeException(nameof(prices), "Unit prices must be positive.");
                if (ordered.ContainsKey(price.Key))
                    throw new ArgumentException("Each shop item must have one unit price.", nameof(prices));
                ordered.Add(price.Key, price.Value);
            }

            Location = location;
            Owner = owner;
            Stock = stock;
            OwnerWallet = ownerWallet;
            _prices = new Dictionary<ItemTypeId, int>(ordered);
        }

        public LocationId Location { get; }
        public NpcId Owner { get; }
        public Inventory Stock { get; }
        public Wallet OwnerWallet { get; }

        /// <summary>
        /// Optional per-buyer discount policy (e.g. friend prices from the owner's
        /// relationships). Null (the default) means every buyer pays the list price.
        /// </summary>
        public IShopDiscountPolicy DiscountPolicy { get; set; }
        public IReadOnlyList<KeyValuePair<ItemTypeId, int>> Prices
        {
            get
            {
                var ordered = new SortedDictionary<ItemTypeId, int>(_prices);
                return new ReadOnlyCollection<KeyValuePair<ItemTypeId, int>>(
                    new List<KeyValuePair<ItemTypeId, int>>(ordered));
            }
        }

        public int UnitPrice(ItemTypeId item)
        {
            if (!item.IsValid) throw new ArgumentException("A valid item type is required.", nameof(item));
            if (!_prices.TryGetValue(item, out int price))
                throw new ArgumentException("The shop does not sell this item.", nameof(item));
            return price;
        }

        internal void SetUnitPrice(ItemTypeId item, int price)
        {
            _ = UnitPrice(item);
            if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
            _prices[item] = price;
        }

        public PurchaseResult Purchase(WorldState world, PurchaseRequest request)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (request == null) throw new ArgumentNullException(nameof(request));

            // The shop sells its oldest lot first: a stale oldest lot marks the sale down by
            // the item's approved stale price factor, before any friend discount.
            int unitPrice = ShopDiscount.DiscountedPrice(
                Stock.EffectiveUnitPrice(request.Item, UnitPrice(request.Item)),
                DiscountPolicy != null ? DiscountPolicy.DiscountPercentFor(request.Buyer) : 0);
            int available = Stock.Count(request.Item);
            _ = request.Inventory.Count(request.Item);
            int affordable = request.Wallet.Balance / unitPrice;
            int possible = Math.Min(request.RequestedQuantity, Math.Min(available, affordable));
            int actual = possible == request.RequestedQuantity || request.AllowPartial ? possible : 0;
            int paid = checked(actual * unitPrice);

            if (actual > 0)
            {
                if (!ReferenceEquals(Stock, request.Inventory))
                    request.Inventory.EnsureCanReceive(request.Item, actual);
                if (!ReferenceEquals(request.Wallet, OwnerWallet)) OwnerWallet.EnsureCanReceive(paid);
            }

            PurchaseOutcome outcome = actual == request.RequestedQuantity
                ? PurchaseOutcome.Full : actual > 0 ? PurchaseOutcome.Partial : PurchaseOutcome.Failed;
            WorldEventType eventType = outcome == PurchaseOutcome.Full
                ? WorldEventType.Purchase : outcome == PurchaseOutcome.Partial
                    ? WorldEventType.PartialPurchase : WorldEventType.FailedPurchase;

            // Append first: all following single-threaded transfers were preflighted and cannot fail.
            world.Events.Append(world.Clock, Location, eventType, request.Buyer,
                new[] { ActorId.ForNpc(Owner) }, EventVisibility.Normal, request.Item, actual, paid);
            if (actual > 0)
            {
                if (!Stock.TransferTo(request.Inventory, request.Item, actual))
                    throw new InvalidOperationException("Preflighted item transfer unexpectedly failed.");
                if (!request.Wallet.TransferTo(OwnerWallet, paid))
                    throw new InvalidOperationException("Preflighted copper transfer unexpectedly failed.");
            }
            return new PurchaseResult(outcome, request.RequestedQuantity, actual, paid);
        }
    }
}
