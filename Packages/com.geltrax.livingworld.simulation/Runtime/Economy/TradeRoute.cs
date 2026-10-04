using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// An immutable trade route between two villages (P6-02). Goods flow from
    /// <see cref="FromVillage"/> to <see cref="ToVillage"/>; a merchant buys at
    /// the origin price, travels <see cref="TravelDays"/> days, and sells at the
    /// destination price. Prices live in <see cref="VillageTradePrices"/>.
    /// </summary>
    public sealed class TradeRoute
    {
        public TradeRoute(TradeRouteId id, VillageId fromVillage, VillageId toVillage,
            int travelDays, IEnumerable<ItemTypeId> goods)
        {
            if (!id.IsValid) throw new ArgumentException("A route needs a valid ID.", nameof(id));
            if (!fromVillage.IsValid) throw new ArgumentException("A route needs a valid origin village.", nameof(fromVillage));
            if (!toVillage.IsValid) throw new ArgumentException("A route needs a valid destination village.", nameof(toVillage));
            if (fromVillage == toVillage)
                throw new ArgumentException("A trade route must connect two different villages.", nameof(toVillage));
            if (travelDays < 1) throw new ArgumentOutOfRangeException(nameof(travelDays), "Travel takes at least one day.");
            if (goods == null) throw new ArgumentException("A route needs at least one good.", nameof(goods));

            var ordered = new SortedList<ItemTypeId, byte>();
            foreach (ItemTypeId good in goods)
            {
                if (!good.IsValid) throw new ArgumentException("Route goods must be valid item types.", nameof(goods));
                if (ordered.ContainsKey(good))
                    throw new ArgumentException("Route goods must not repeat: '" + good + "'.", nameof(goods));
                ordered.Add(good, 0);
            }
            if (ordered.Count == 0) throw new ArgumentException("A route needs at least one good.", nameof(goods));

            Id = id;
            FromVillage = fromVillage;
            ToVillage = toVillage;
            TravelDays = travelDays;
            Goods = new ReadOnlyCollection<ItemTypeId>(new List<ItemTypeId>(ordered.Keys));
        }

        public TradeRouteId Id { get; }
        public VillageId FromVillage { get; }
        public VillageId ToVillage { get; }
        public int TravelDays { get; }

        /// <summary>Goods that flow along this route, in ordinal item order.</summary>
        public IReadOnlyList<ItemTypeId> Goods { get; }
    }
}
