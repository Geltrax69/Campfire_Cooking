using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// How badly the village wants each ingredient right now (P3-03, SKILLS.md section 2:
    /// "good cooked food bids up raw ingredients"). Cooking a recipe adds one demand
    /// point per input unit consumed; demand halves each day as the market moves on.
    /// While an item's demand is high, the price system nudges its shop price upward.
    /// Stored in ordinal item order so iteration is deterministic.
    /// </summary>
    public sealed class IngredientDemandState
    {
        /// <summary>Demand at or above this counts as high for price nudges.</summary>
        public const int HighDemandThreshold = 10;

        private readonly SortedDictionary<ItemTypeId, int> _demand =
            new SortedDictionary<ItemTypeId, int>();

        /// <summary>The last game day the decay system ran; it decays at most once a day.</summary>
        public long LastDecayDay { get; internal set; } = -1;

        /// <summary>Current demand for the item; 0 when nothing wants it.</summary>
        public int Demand(ItemTypeId item)
        {
            if (!item.IsValid) throw new ArgumentException("Demand needs a valid item type.", nameof(item));
            return _demand.TryGetValue(item, out int amount) ? amount : 0;
        }

        /// <summary>True when demand is high enough to nudge shop prices upward.</summary>
        public bool IsHigh(ItemTypeId item) => Demand(item) >= HighDemandThreshold;

        /// <summary>Adds demand points for an item; amount must be positive.</summary>
        public void AddDemand(ItemTypeId item, int amount)
        {
            if (!item.IsValid) throw new ArgumentException("Demand needs a valid item type.", nameof(item));
            if (amount < 1) throw new ArgumentOutOfRangeException(nameof(amount), "Demand must grow.");
            _demand[item] = checked(Demand(item) + amount);
        }

        /// <summary>
        /// Every item's demand, in ordinal item order (deterministic snapshots).
        /// </summary>
        public IReadOnlyList<KeyValuePair<ItemTypeId, int>> All =>
            new ReadOnlyCollection<KeyValuePair<ItemTypeId, int>>(new List<KeyValuePair<ItemTypeId, int>>(_demand));

        /// <summary>
        /// Halves every demand counter (rounded down), dropping items that reach
        /// zero. Called once per game day by <see cref="IngredientDemandSystem"/>.
        /// </summary>
        internal void DecayOneDay()
        {
            var survivors = new List<KeyValuePair<ItemTypeId, int>>(_demand.Count);
            foreach (KeyValuePair<ItemTypeId, int> pair in _demand)
            {
                int halved = pair.Value / 2;
                if (halved > 0) survivors.Add(new KeyValuePair<ItemTypeId, int>(pair.Key, halved));
            }
            _demand.Clear();
            foreach (KeyValuePair<ItemTypeId, int> pair in survivors)
                _demand.Add(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// Ages ingredient demand: once per game day every counter halves (rounded down)
    /// as the market moves on. Runs in the Economy phase.
    /// </summary>
    public sealed class IngredientDemandSystem : IWorldSystem
    {
        public string Id => "economy.ingredient-demand";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            IngredientDemandState demand = state.IngredientDemand;
            long today = state.Clock.Day;
            if (demand.LastDecayDay >= today) return;
            demand.LastDecayDay = today;
            demand.DecayOneDay();
        }
    }
}
