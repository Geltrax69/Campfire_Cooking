using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Owns every trade route by TradeRouteId in deterministic ordinal order (P6-02).
    /// </summary>
    public sealed class TradeRouteRegistry
    {
        private readonly SortedList<TradeRouteId, TradeRoute> _routes =
            new SortedList<TradeRouteId, TradeRoute>();

        public int Count => _routes.Count;

        /// <summary>Registers a route; IDs must be unique.</summary>
        public void Register(TradeRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (_routes.ContainsKey(route.Id))
                throw new ArgumentException("Trade route ID is already registered: '" + route.Id + "'.", nameof(route));
            _routes.Add(route.Id, route);
        }

        public TradeRoute Get(TradeRouteId id)
        {
            if (!id.IsValid) throw new ArgumentException("A trade route ID must be valid.", nameof(id));
            if (!_routes.TryGetValue(id, out var route))
                throw new ArgumentException("Unknown trade route ID: '" + id + "'.", nameof(id));
            return route;
        }

        /// <summary>Every route in deterministic ordinal TradeRouteId order.</summary>
        public IReadOnlyList<TradeRoute> GetAll() => new List<TradeRoute>(_routes.Values);

        /// <summary>Routes leaving the given village, in ordinal route order.</summary>
        public IReadOnlyList<TradeRoute> GetFrom(VillageId village)
        {
            if (!village.IsValid) throw new ArgumentException("A village ID must be valid.", nameof(village));
            var matching = new List<TradeRoute>();
            foreach (TradeRoute route in _routes.Values)
                if (route.FromVillage == village) matching.Add(route);
            return matching;
        }

        /// <summary>Routes arriving at the given village, in ordinal route order.</summary>
        public IReadOnlyList<TradeRoute> GetTo(VillageId village)
        {
            if (!village.IsValid) throw new ArgumentException("A village ID must be valid.", nameof(village));
            var matching = new List<TradeRoute>();
            foreach (TradeRoute route in _routes.Values)
                if (route.ToVillage == village) matching.Add(route);
            return matching;
        }
    }
}
