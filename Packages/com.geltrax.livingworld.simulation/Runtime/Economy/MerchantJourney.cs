using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable persistence snapshot for one merchant journey.</summary>
    public sealed class MerchantJourneySnapshot
    {
        public MerchantJourneySnapshot(TradeRouteId routeId, IReadOnlyList<KeyValuePair<ItemTypeId, int>> cargo,
            long departureDay, long arrivalDay, int boughtCopper, int soldCopper, bool isComplete)
        {
            if (!routeId.IsValid) throw new ArgumentException("A route ID must be valid.", nameof(routeId));
            if (cargo == null || cargo.Count == 0)
                throw new ArgumentException("A journey snapshot needs cargo.", nameof(cargo));
            if (departureDay < 1) throw new ArgumentOutOfRangeException(nameof(departureDay));
            if (arrivalDay <= departureDay) throw new ArgumentOutOfRangeException(nameof(arrivalDay));
            if (boughtCopper < 0) throw new ArgumentOutOfRangeException(nameof(boughtCopper));
            if (soldCopper < 0) throw new ArgumentOutOfRangeException(nameof(soldCopper));
            RouteId = routeId;
            Cargo = cargo;
            DepartureDay = departureDay;
            ArrivalDay = arrivalDay;
            BoughtCopper = boughtCopper;
            SoldCopper = soldCopper;
            IsComplete = isComplete;
        }

        public TradeRouteId RouteId { get; }
        public IReadOnlyList<KeyValuePair<ItemTypeId, int>> Cargo { get; }
        public long DepartureDay { get; }
        public long ArrivalDay { get; }
        public int BoughtCopper { get; }
        public int SoldCopper { get; }
        public bool IsComplete { get; }
    }

    /// <summary>
    /// One abstract merchant's journey along a trade route (P6-02). The merchant
    /// is not an NPC: it buys cargo at the origin on <see cref="DepartureDay"/>,
    /// travels, and sells at the destination on <see cref="ArrivalDay"/>. Profit
    /// is (sold - bought - travel cost); the spread leaves the simulation with
    /// the merchant — a named sink, like the traveling merchant's margin.
    /// </summary>
    public sealed class MerchantJourney
    {
        private readonly List<KeyValuePair<ItemTypeId, int>> _cargo;

        public MerchantJourney(TradeRouteId routeId, IEnumerable<KeyValuePair<ItemTypeId, int>> cargo,
            long departureDay, long arrivalDay, int boughtCopper)
        {
            if (!routeId.IsValid) throw new ArgumentException("A journey needs a valid route ID.", nameof(routeId));
            if (departureDay < 1) throw new ArgumentOutOfRangeException(nameof(departureDay));
            if (arrivalDay <= departureDay)
                throw new ArgumentException("Arrival must be after departure.", nameof(arrivalDay));
            if (boughtCopper < 0) throw new ArgumentOutOfRangeException(nameof(boughtCopper));
            _cargo = new List<KeyValuePair<ItemTypeId, int>>();
            if (cargo != null)
            {
                var seen = new HashSet<ItemTypeId>();
                foreach (var lot in cargo)
                {
                    if (!lot.Key.IsValid) throw new ArgumentException("Cargo item types must be valid.", nameof(cargo));
                    if (lot.Value < 1) throw new ArgumentOutOfRangeException(nameof(cargo), "Cargo quantities must be positive.");
                    if (!seen.Add(lot.Key)) throw new ArgumentException("Cargo must not repeat goods.", nameof(cargo));
                    _cargo.Add(lot);
                }
            }
            if (_cargo.Count == 0) throw new ArgumentException("A journey needs at least one cargo lot.", nameof(cargo));
            _cargo.Sort((left, right) => left.Key.CompareTo(right.Key));

            RouteId = routeId;
            DepartureDay = departureDay;
            ArrivalDay = arrivalDay;
            BoughtCopper = boughtCopper;
        }

        public TradeRouteId RouteId { get; }
        public IReadOnlyList<KeyValuePair<ItemTypeId, int>> Cargo =>
            new ReadOnlyCollection<KeyValuePair<ItemTypeId, int>>(_cargo);
        public long DepartureDay { get; }
        public long ArrivalDay { get; }
        public int BoughtCopper { get; }
        public int SoldCopper { get; private set; }
        public bool IsComplete { get; private set; }

        /// <summary>Total cargo units across all goods.</summary>
        public int TotalUnits
        {
            get
            {
                int total = 0;
                foreach (var lot in _cargo) total = checked(total + lot.Value);
                return total;
            }
        }

        /// <summary>Records the sale at the destination; a journey arrives exactly once.</summary>
        public void MarkArrived(int soldCopper)
        {
            if (IsComplete) throw new InvalidOperationException("This journey already arrived.");
            if (soldCopper < 0) throw new ArgumentOutOfRangeException(nameof(soldCopper));
            SoldCopper = soldCopper;
            IsComplete = true;
        }

        /// <summary>Merchant profit: sold - bought - travel cost. Requires arrival.</summary>
        public int ProfitCopper(int travelCostCopper)
        {
            if (!IsComplete) throw new InvalidOperationException("Profit is only known after arrival.");
            if (travelCostCopper < 0) throw new ArgumentOutOfRangeException(nameof(travelCostCopper));
            return checked(SoldCopper - BoughtCopper - travelCostCopper);
        }

        /// <summary>Captures this journey for Persistence.</summary>
        public MerchantJourneySnapshot Capture() => new MerchantJourneySnapshot(
            RouteId, Cargo, DepartureDay, ArrivalDay, BoughtCopper, SoldCopper, IsComplete);

        /// <summary>Rebuilds a journey from a validated snapshot.</summary>
        public static MerchantJourney Restore(MerchantJourneySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var journey = new MerchantJourney(snapshot.RouteId, snapshot.Cargo,
                snapshot.DepartureDay, snapshot.ArrivalDay, snapshot.BoughtCopper);
            if (snapshot.IsComplete) journey.MarkArrived(snapshot.SoldCopper);
            return journey;
        }
    }
}
