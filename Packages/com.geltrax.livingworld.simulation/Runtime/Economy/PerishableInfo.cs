using System;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// How far a perishable lot has aged. Spoiled lots are removed, so they have no state.
    /// </summary>
    public enum Freshness
    {
        Fresh,
        Stale
    }

    /// <summary>
    /// The two-stage spoilage spec for one item type, from the approved `perishable` block
    /// in Content/items/items.json (ECONOMY.md §6: nothing keeps forever). A lot stays fresh
    /// for <see cref="FreshDays"/> daily agings, then goes stale for <see cref="StaleDays"/>
    /// more (trading at <see cref="StalePricePercent"/> of list and applying the stale
    /// effects when eaten), then spoils and is removed to compost or animal feed.
    /// </summary>
    public sealed class PerishableInfo
    {
        public PerishableInfo(int freshDays, int staleDays, int stalePricePercent,
            string staleName = null, string spoiledName = null,
            int? staleHungerEffect = null, int? staleHealthEffect = null)
        {
            if (freshDays < 1) throw new ArgumentOutOfRangeException(nameof(freshDays));
            if (staleDays < 1) throw new ArgumentOutOfRangeException(nameof(staleDays));
            if (stalePricePercent < 1 || stalePricePercent > 99)
                throw new ArgumentOutOfRangeException(nameof(stalePricePercent));
            FreshDays = freshDays;
            StaleDays = staleDays;
            StalePricePercent = stalePricePercent;
            StaleName = staleName;
            SpoiledName = spoiledName;
            StaleHungerEffect = staleHungerEffect;
            StaleHealthEffect = staleHealthEffect;
        }

        public int FreshDays { get; }
        public int StaleDays { get; }
        public int StalePricePercent { get; }
        public string StaleName { get; }
        public string SpoiledName { get; }
        public int? StaleHungerEffect { get; }
        public int? StaleHealthEffect { get; }

        /// <summary>
        /// Freshness of a lot aged the given number of days. Spoiled is reported as stale
        /// here; the caller removes lots past their stale life (see Inventory.AgeLots).
        /// </summary>
        public Freshness FreshnessAtAge(int ageDays)
        {
            if (ageDays < 0) throw new ArgumentOutOfRangeException(nameof(ageDays));
            return ageDays <= FreshDays ? Freshness.Fresh : Freshness.Stale;
        }

        /// <summary>True once the lot's stale life is over: it spoils and is removed.</summary>
        public bool IsSpoiledAtAge(int ageDays)
        {
            if (ageDays < 0) throw new ArgumentOutOfRangeException(nameof(ageDays));
            return ageDays > FreshDays + StaleDays;
        }
    }
}
