using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// One good the traveling merchant buys: taken from a real village stock, paid in real
    /// copper into the seller's till. The merchant's buy price sits below village retail —
    /// the merchant needs a margin for the road.
    /// </summary>
    public sealed class MerchantPurchaseOffer
    {
        public MerchantPurchaseOffer(NpcId seller, Inventory stock, Wallet till, ItemTypeId item,
            int buyPriceCopper, int maxUnitsPerVisit, LocationId location)
        {
            if (!seller.IsValid) throw new ArgumentException("A seller is required.", nameof(seller));
            if (stock == null) throw new ArgumentNullException(nameof(stock));
            if (till == null) throw new ArgumentNullException(nameof(till));
            if (!item.IsValid) throw new ArgumentException("An item is required.", nameof(item));
            if (buyPriceCopper < 1) throw new ArgumentOutOfRangeException(nameof(buyPriceCopper));
            if (maxUnitsPerVisit < 1) throw new ArgumentOutOfRangeException(nameof(maxUnitsPerVisit));
            if (!location.IsValid) throw new ArgumentException("A location is required.", nameof(location));
            stock.Count(item);
            Seller = seller;
            Stock = stock;
            Till = till;
            Item = item;
            BuyPriceCopper = buyPriceCopper;
            MaxUnitsPerVisit = maxUnitsPerVisit;
            Location = location;
        }

        public NpcId Seller { get; }
        public Inventory Stock { get; }
        public Wallet Till { get; }
        public ItemTypeId Item { get; }
        public int BuyPriceCopper { get; }
        public int MaxUnitsPerVisit { get; }
        public LocationId Location { get; }
    }

    /// <summary>
    /// One good the traveling merchant sells: delivered into the buyer's stock, paid from the
    /// buyer's till at the merchant's price. When <see cref="RequiresExhaustionOrder"/> is set,
    /// the offer only fires while the matching exhaustion flag is raised (Doran's iron).
    /// </summary>
    public sealed class MerchantImportOffer
    {
        public MerchantImportOffer(NpcId buyer, Inventory stock, Wallet till, ItemTypeId item,
            int targetStock, int sellPriceCopper, LocationId location,
            bool requiresExhaustionOrder = false)
        {
            if (!buyer.IsValid) throw new ArgumentException("A buyer is required.", nameof(buyer));
            if (stock == null) throw new ArgumentNullException(nameof(stock));
            if (till == null) throw new ArgumentNullException(nameof(till));
            if (!item.IsValid) throw new ArgumentException("An item is required.", nameof(item));
            if (targetStock < 0) throw new ArgumentOutOfRangeException(nameof(targetStock));
            if (sellPriceCopper < 1) throw new ArgumentOutOfRangeException(nameof(sellPriceCopper));
            if (!location.IsValid) throw new ArgumentException("A location is required.", nameof(location));
            stock.Count(item);
            Buyer = buyer;
            Stock = stock;
            Till = till;
            Item = item;
            TargetStock = targetStock;
            SellPriceCopper = sellPriceCopper;
            Location = location;
            RequiresExhaustionOrder = requiresExhaustionOrder;
        }

        public NpcId Buyer { get; }
        public Inventory Stock { get; }
        public Wallet Till { get; }
        public ItemTypeId Item { get; }
        public int TargetStock { get; }
        public int SellPriceCopper { get; }
        public LocationId Location { get; }
        public bool RequiresExhaustionOrder { get; }
    }

    /// <summary>Immutable caller configuration for the traveling merchant's visits.</summary>
    public sealed class MerchantConfiguration
    {
        public MerchantConfiguration(string id, LocationId market,
            IReadOnlyList<MerchantPurchaseOffer> purchaseOffers,
            IReadOnlyList<MerchantImportOffer> importOffers,
            int budgetPerVisitCopper, EventVisibility visibility)
        {
            ProductionConfiguration.RequireId(id, nameof(id));
            if (!market.IsValid) throw new ArgumentException("A market location is required.", nameof(market));
            if (purchaseOffers == null) throw new ArgumentNullException(nameof(purchaseOffers));
            if (importOffers == null) throw new ArgumentNullException(nameof(importOffers));
            if (budgetPerVisitCopper < 1) throw new ArgumentOutOfRangeException(nameof(budgetPerVisitCopper));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            Market = market;
            PurchaseOffers = purchaseOffers;
            ImportOffers = importOffers;
            BudgetPerVisitCopper = budgetPerVisitCopper;
            Visibility = visibility;
        }

        public string Id { get; }
        public LocationId Market { get; }
        public IReadOnlyList<MerchantPurchaseOffer> PurchaseOffers { get; }
        public IReadOnlyList<MerchantImportOffer> ImportOffers { get; }
        public int BudgetPerVisitCopper { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of the merchant's schedule: the next visit day. Starts
    /// uninitialized — the world-build step schedules the first visit, and the system stays
    /// quiet until then (so a reloaded world without P2-12's saver wiring gains no visits).
    /// P2-12 extends the saver to write this state; until then use RestoreMerchantSchedule.
    /// </summary>
    public sealed class MerchantScheduleState
    {
        public MerchantScheduleState(bool initialized = false, long nextVisitDay = -1)
        {
            if (initialized && nextVisitDay < 1)
                throw new ArgumentOutOfRangeException(nameof(nextVisitDay));
            IsInitialized = initialized;
            NextVisitDay = nextVisitDay;
        }

        public bool IsInitialized { get; }
        public long NextVisitDay { get; private set; }

        internal void ScheduleNext(long day)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            NextVisitDay = day;
        }
    }

    /// <summary>
    /// Runs the traveling merchant's visits (economy.merchant, Economy phase). Every ~3 weeks
    /// in warm months the merchant's cart rolls into the market square: it buys village goods
    /// (apples, timber, honey, smoked fish, pelts) up to its budget, paying real copper from
    /// outside the village — the first named gate money enters through — and sells imports:
    /// iron for Doran (fulfilling P2-07's exhaustion order) and Tilda's order list (salt,
    /// cloth, lamp oil, dye). No visits in winter: the ford runs high and cold.
    /// Offer order follows the configuration lists, so the run is deterministic.
    /// </summary>
    public sealed class MerchantSystem : IWorldSystem
    {
        private readonly List<MerchantConfiguration> _configurations;

        public MerchantSystem(IEnumerable<MerchantConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<MerchantConfiguration>();
            foreach (MerchantConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Merchant configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "economy.merchant";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.MerchantSchedule.IsInitialized) return;
            foreach (MerchantConfiguration configuration in _configurations)
            {
                if (state.Clock.Day < state.MerchantSchedule.NextVisitDay) continue;
                // A visit day that lands in winter (only possible across save/load before P2-12)
                // is skipped, not banked: the merchant simply doesn't come.
                if (VillageCalendar.IsWarm(VillageCalendar.SeasonAt(state.Clock)))
                    RunVisit(state, configuration);
                ScheduleNext(state);
            }
        }

        private static void RunVisit(WorldState state, MerchantConfiguration configuration)
        {
            BuyGoods(state, configuration);
            SellImports(state, configuration);
            state.Events.Append(state.Clock, configuration.Market, WorldEventType.Arrival,
                null, visibility: configuration.Visibility);
        }

        private static void BuyGoods(WorldState state, MerchantConfiguration configuration)
        {
            int remaining = configuration.BudgetPerVisitCopper;
            foreach (MerchantPurchaseOffer offer in configuration.PurchaseOffers)
            {
                if (remaining < offer.BuyPriceCopper) break;
                int affordable = remaining / offer.BuyPriceCopper;
                int units = Math.Min(offer.Stock.Count(offer.Item),
                    Math.Min(offer.MaxUnitsPerVisit, affordable));
                if (units < 1) continue;
                int copper = checked(units * offer.BuyPriceCopper);
                // Append first: the stock count was just checked and this runs single-threaded,
                // so the removal below cannot fail.
                state.Events.Append(state.Clock, offer.Location, WorldEventType.Purchase,
                    ActorId.ForNpc(offer.Seller), visibility: configuration.Visibility,
                    itemType: offer.Item, quantity: units, copper: copper);
                if (!offer.Stock.TryRemove(offer.Item, units))
                    throw new InvalidOperationException("Preflighted stock removal unexpectedly failed.");
                // The copper comes from outside the village: this is the merchant gate, where
                // new money enters the simulation.
                offer.Till.Credit(copper);
                remaining -= copper;
            }
        }

        private static void SellImports(WorldState state, MerchantConfiguration configuration)
        {
            foreach (MerchantImportOffer offer in configuration.ImportOffers)
            {
                if (offer.RequiresExhaustionOrder && !state.Smithy.IronExhaustionOrdered) continue;
                int needed = offer.TargetStock - offer.Stock.Count(offer.Item);
                if (needed < 1) continue;
                int affordable = offer.Till.Balance / offer.SellPriceCopper;
                int units = Math.Min(needed, affordable);
                if (units < 1) continue;
                int copper = checked(units * offer.SellPriceCopper);
                offer.Stock.EnsureCanReceive(offer.Item, units);
                // Append first: preflighted.
                state.Events.Append(state.Clock, offer.Location, WorldEventType.Restocked,
                    ActorId.ForNpc(offer.Buyer), visibility: configuration.Visibility,
                    itemType: offer.Item, quantity: units, copper: copper);
                if (!offer.Till.TryDebit(copper))
                    throw new InvalidOperationException("Preflighted till debit unexpectedly failed.");
                offer.Stock.Add(offer.Item, units);
                // The copper leaves the village with the merchant: the import gate.
                if (offer.RequiresExhaustionOrder)
                    state.Smithy.ResetIronExhaustion();
            }
        }

        private static void ScheduleNext(WorldState state)
        {
            // ~3 weeks: 18-24 days, drawn from the shared seeded RNG.
            long next = state.Clock.Day + 18 + state.Rng.NextInt(7);
            if (!VillageCalendar.IsWarm(VillageCalendar.SeasonAt(new GameTime((next - 1) * 1440))))
                next = VillageCalendar.FirstSpringDayOnOrAfter(next);
            state.MerchantSchedule.ScheduleNext(next);
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MerchantConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Merchant configuration IDs must be unique.", "configurations");
        }
    }
}
