using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Runs inter-village trade (economy.trade_routes, Economy phase; P6-02).
    /// Once per <see cref="TradeRoutePolicy.DepartureIntervalDays"/> a merchant
    /// is drawn from the world's seeded RNG for one route ("which route"), loads
    /// 1..<see cref="TradeRoutePolicy.MaxUnitsPerGood"/> of each good ("how much"),
    /// buys at the origin and sells at the destination after
    /// <see cref="TradeRoute.TravelDays"/> days.
    ///
    /// Money model (deliberate, documented): the origin village <i>sells</i> goods,
    /// so its wealth <i>rises</i> at the origin price; the destination village
    /// <i>buys</i> them, so its wealth <i>falls</i> at the destination price. The
    /// merchant's spread (sold - bought - travel cost) leaves the simulation with
    /// the merchant — a named sink, like the traveling merchant's margin. For
    /// Millbrook (full simulation) the merchant really buys from shops through
    /// <see cref="MerchantPurchaseOffer"/>s and sells imports through
    /// <see cref="MerchantImportOffer"/>s; abstract villages just move wealth.
    ///
    /// The brief's requirement 5 reads inverted relative to this model (it has the
    /// destination's wealth rising); the buy-low/sell-high model above is the
    /// economically coherent reading of requirement 4 and is flagged for the
    /// orchestrator's Decision log.
    ///
    /// In-transit cargo that Millbrook shops cannot absorb (full target stock or
    /// an empty till) leaves the simulation with the merchant, which carries it
    /// onward down the road.
    /// </summary>
    public sealed class TradeRouteSystem : IWorldSystem
    {
        private readonly VillageRegistry _villages;
        private readonly TradeRouteRegistry _routes;
        private readonly VillageTradePrices _prices;
        private readonly TradeRoutePolicy _policy;
        private readonly TradeRouteLedger _ledger;
        private readonly List<MerchantPurchaseOffer> _millbrookPurchaseOffers;
        private readonly List<MerchantImportOffer> _millbrookImportOffers;

        public TradeRouteSystem(
            VillageRegistry villages,
            TradeRouteRegistry routes,
            VillageTradePrices prices,
            TradeRoutePolicy policy,
            TradeRouteLedger ledger,
            IEnumerable<MerchantPurchaseOffer> millbrookPurchaseOffers = null,
            IEnumerable<MerchantImportOffer> millbrookImportOffers = null)
        {
            if (villages == null) throw new ArgumentNullException(nameof(villages));
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            if (prices == null) throw new ArgumentNullException(nameof(prices));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            ValidateRoutes(villages, routes, prices);

            _villages = villages;
            _routes = routes;
            _prices = prices;
            _policy = policy;
            _ledger = ledger;
            _millbrookPurchaseOffers = new List<MerchantPurchaseOffer>();
            if (millbrookPurchaseOffers != null)
                foreach (MerchantPurchaseOffer offer in millbrookPurchaseOffers)
                {
                    if (offer == null) throw new ArgumentException("Purchase offers cannot contain null.", nameof(millbrookPurchaseOffers));
                    _millbrookPurchaseOffers.Add(offer);
                }
            _millbrookImportOffers = new List<MerchantImportOffer>();
            if (millbrookImportOffers != null)
                foreach (MerchantImportOffer offer in millbrookImportOffers)
                {
                    if (offer == null) throw new ArgumentException("Import offers cannot contain null.", nameof(millbrookImportOffers));
                    _millbrookImportOffers.Add(offer);
                }
        }

        public string Id => "economy.trade_routes";
        public SimulationPhase Phase => SimulationPhase.Economy;

        /// <summary>
        /// Advances trade one day at a time up to the current clock day, so a
        /// clock jump (or a reloaded world) processes every missed day in order.
        /// Truth events are appended at the current clock (never backdated: the
        /// event log forbids backwards time).
        /// </summary>
        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            long today = state.Clock.Day;
            for (long day = _ledger.LastProcessedDay + 1; day <= today; day++)
            {
                ProcessArrivals(state, day);
                if (_routes.Count > 0 && day >= _ledger.NextDepartureDay)
                    Depart(state, day);
            }
            _ledger.RecordProcessedDay(today);
        }

        private void ProcessArrivals(WorldState state, long day)
        {
            var due = new List<MerchantJourney>();
            foreach (MerchantJourney journey in _ledger.Journeys)
                if (!journey.IsComplete && journey.ArrivalDay <= day) due.Add(journey);
            due.Sort((left, right) =>
            {
                int comparison = left.RouteId.CompareTo(right.RouteId);
                return comparison != 0 ? comparison : left.DepartureDay.CompareTo(right.DepartureDay);
            });
            foreach (MerchantJourney journey in due) Arrive(state, journey);
        }

        private void Depart(WorldState state, long day)
        {
            IReadOnlyList<TradeRoute> routes = _routes.GetAll();
            TradeRoute route = routes[state.Rng.NextInt(routes.Count)];
            AbstractVillageState from = _villages[route.FromVillage];
            // Validate the anchor before mutating anything: a bad anchor must not
            // leave a journey in the ledger without its departure event.
            LocationId anchor = AnchorOf(from, route.FromVillage);

            var cargo = new List<KeyValuePair<ItemTypeId, int>>();
            int boughtCopper = 0;
            foreach (ItemTypeId good in route.Goods)
            {
                int desired = 1 + state.Rng.NextInt(_policy.MaxUnitsPerGood);
                int units;
                int copper;
                if (from.Lod == VillageLod.Full)
                {
                    (units, copper) = BuyFromMillbrook(state, good, desired);
                }
                else
                {
                    int price = _prices.GetPrice(route.FromVillage, good);
                    units = desired;
                    copper = checked(units * price);
                    // The origin village sold goods to the merchant: its wealth
                    // rises, paid from outside (the merchant gate — the same named
                    // source the traveling merchant's purchases use).
                    from.AdjustWealthCopper(copper);
                }
                if (units > 0)
                {
                    cargo.Add(new KeyValuePair<ItemTypeId, int>(good, units));
                    boughtCopper = checked(boughtCopper + copper);
                }
            }

            // An empty departure still consumes the slot: the schedule stays
            // deterministic instead of retrying every day.
            _ledger.ScheduleNextDeparture(checked(day + _policy.DepartureIntervalDays));
            if (cargo.Count == 0) return;

            var journey = new MerchantJourney(route.Id, cargo, day, checked(day + route.TravelDays), boughtCopper);
            _ledger.AddJourney(journey);
            state.Events.Append(state.Clock, anchor, WorldEventType.MerchantDeparted,
                null, visibility: EventVisibility.Normal, quantity: journey.TotalUnits, copper: boughtCopper);
        }

        private (int units, int copper) BuyFromMillbrook(WorldState state, ItemTypeId good, int desired)
        {
            int units = 0;
            int copper = 0;
            foreach (MerchantPurchaseOffer offer in _millbrookPurchaseOffers)
            {
                if (offer.Item != good) continue;
                if (units >= desired) break;
                int take = Math.Min(offer.Stock.Count(good),
                    Math.Min(offer.MaxUnitsPerVisit, desired - units));
                if (take < 1) continue;
                int cost = checked(take * offer.BuyPriceCopper);
                // Append first: the stock count was just checked and this runs
                // single-threaded, so the removal below cannot fail.
                state.Events.Append(state.Clock, offer.Location, WorldEventType.Purchase,
                    ActorId.ForNpc(offer.Seller), visibility: EventVisibility.Normal,
                    itemType: good, quantity: take, copper: cost);
                // Inter-village trade counts toward the town trade stat, like the
                // traveling merchant's copper (P5-01).
                state.MerchantSchedule.RecordMerchantTrade(cost, VillageCalendar.MonthIndex(state.Clock));
                if (!offer.Stock.TryRemove(good, take))
                    throw new InvalidOperationException("Preflighted stock removal unexpectedly failed.");
                // The copper comes from outside the village: the merchant gate.
                offer.Till.Credit(cost);
                units += take;
                copper = checked(copper + cost);
            }
            return (units, copper);
        }

        private void Arrive(WorldState state, MerchantJourney journey)
        {
            TradeRoute route = _routes.Get(journey.RouteId);
            AbstractVillageState to = _villages[route.ToVillage];
            // Validate the anchor before mutating anything.
            LocationId anchor = AnchorOf(to, route.ToVillage);
            int soldCopper = to.Lod == VillageLod.Abstract
                ? SellToAbstractVillage(route, to, journey)
                : SellToMillbrook(state, journey);
            journey.MarkArrived(soldCopper);
            state.Events.Append(state.Clock, anchor, WorldEventType.MerchantArrived,
                null, visibility: EventVisibility.Normal, quantity: journey.TotalUnits, copper: soldCopper);
        }

        private int SellToAbstractVillage(TradeRoute route, AbstractVillageState to, MerchantJourney journey)
        {
            int soldCopper = 0;
            foreach (KeyValuePair<ItemTypeId, int> lot in journey.Cargo)
            {
                int proceeds = checked(lot.Value * _prices.GetPrice(route.ToVillage, lot.Key));
                // The destination village bought imports from the merchant: its
                // wealth falls, paid out to the road.
                to.AdjustWealthCopper(-proceeds);
                soldCopper = checked(soldCopper + proceeds);
            }
            return soldCopper;
        }

        private int SellToMillbrook(WorldState state, MerchantJourney journey)
        {
            int soldCopper = 0;
            foreach (KeyValuePair<ItemTypeId, int> lot in journey.Cargo)
            {
                int remaining = lot.Value;
                foreach (MerchantImportOffer offer in _millbrookImportOffers)
                {
                    if (offer.Item != lot.Key || remaining < 1) continue;
                    if (offer.RequiresExhaustionOrder && !state.Smithy.IronExhaustionOrdered) continue;
                    int needed = offer.TargetStock - offer.Stock.Count(lot.Key);
                    if (needed < 1) continue;
                    int affordable = offer.Till.Balance / offer.SellPriceCopper;
                    int take = Math.Min(remaining, Math.Min(needed, affordable));
                    if (take < 1) continue;
                    int cost = checked(take * offer.SellPriceCopper);
                    offer.Stock.EnsureCanReceive(lot.Key, take);
                    // Append first: preflighted.
                    state.Events.Append(state.Clock, offer.Location, WorldEventType.Restocked,
                        ActorId.ForNpc(offer.Buyer), visibility: EventVisibility.Normal,
                        itemType: lot.Key, quantity: take, copper: cost);
                    state.MerchantSchedule.RecordMerchantTrade(cost, VillageCalendar.MonthIndex(state.Clock));
                    if (!offer.Till.TryDebit(cost))
                        throw new InvalidOperationException("Preflighted till debit unexpectedly failed.");
                    offer.Stock.Add(lot.Key, take);
                    // The copper leaves the village with the merchant: the import gate.
                    soldCopper = checked(soldCopper + cost);
                    remaining -= take;
                }
                // Any remainder leaves the simulation with the merchant.
            }
            return soldCopper;
        }

        private static LocationId AnchorOf(AbstractVillageState village, VillageId id)
        {
            if (!village.AnchorLocation.IsValid)
                throw new InvalidOperationException(
                    "Trade route endpoint '" + id + "' needs a valid anchor location.");
            return village.AnchorLocation;
        }

        private static void ValidateRoutes(VillageRegistry villages, TradeRouteRegistry routes, VillageTradePrices prices)
        {
            foreach (TradeRoute route in routes.GetAll())
            {
                AbstractVillageState from = GetVillage(villages, route.FromVillage, route.Id, "origin");
                AbstractVillageState to = GetVillage(villages, route.ToVillage, route.Id, "destination");
                foreach (ItemTypeId good in route.Goods)
                {
                    // Abstract villages trade purely on the price list; Millbrook
                    // trades through real shop offers, which carry their own prices.
                    if (from.Lod == VillageLod.Abstract)
                        RequirePrice(prices, route.FromVillage, good, route.Id);
                    if (to.Lod == VillageLod.Abstract)
                        RequirePrice(prices, route.ToVillage, good, route.Id);
                }
            }
        }

        private static AbstractVillageState GetVillage(VillageRegistry villages, VillageId id,
            TradeRouteId routeId, string role)
        {
            try
            {
                return villages[id];
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(
                    "Trade route '" + routeId + "' has an unknown " + role + " village: '" + id + "'.",
                    nameof(villages), exception);
            }
        }

        private static void RequirePrice(VillageTradePrices prices, VillageId village,
            ItemTypeId good, TradeRouteId routeId)
        {
            try
            {
                prices.GetPrice(village, good);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(
                    "Trade route '" + routeId + "' needs a trade price for '" + good +
                    "' at abstract village '" + village + "'.", nameof(prices), exception);
            }
        }
    }
}
