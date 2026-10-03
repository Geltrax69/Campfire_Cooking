using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable caller configuration for one mill's grain-to-flour operation.</summary>
    public sealed class MillingConfiguration
    {
        public MillingConfiguration(string id, LocationId mill, NpcId miller, Inventory millStock,
            ItemTypeId grain, ItemTypeId flour, int grainPerBatch, int tollSacksPerBatch,
            EventVisibility visibility)
        {
            RequireId(id, nameof(id));
            if (!mill.IsValid) throw new ArgumentException("A mill location is required.", nameof(mill));
            if (!miller.IsValid) throw new ArgumentException("A miller is required.", nameof(miller));
            if (millStock == null) throw new ArgumentNullException(nameof(millStock));
            if (!grain.IsValid) throw new ArgumentException("A grain item is required.", nameof(grain));
            if (!flour.IsValid) throw new ArgumentException("A flour item is required.", nameof(flour));
            if (grain == flour)
                throw new ArgumentException("Grain and flour must be different items.", nameof(flour));
            millStock.Count(grain);
            millStock.Count(flour);
            if (grainPerBatch < 1) throw new ArgumentOutOfRangeException(nameof(grainPerBatch));
            if (tollSacksPerBatch < 0) throw new ArgumentOutOfRangeException(nameof(tollSacksPerBatch));
            if (tollSacksPerBatch >= grainPerBatch)
                throw new ArgumentException("The toll must leave grain to grind.", nameof(tollSacksPerBatch));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            Mill = mill;
            Miller = miller;
            MillStock = millStock;
            Grain = grain;
            Flour = flour;
            GrainPerBatch = grainPerBatch;
            TollSacksPerBatch = tollSacksPerBatch;
            Visibility = visibility;
        }

        public string Id { get; }
        public LocationId Mill { get; }
        public NpcId Miller { get; }
        public Inventory MillStock { get; }
        public ItemTypeId Grain { get; }
        public ItemTypeId Flour { get; }
        public int GrainPerBatch { get; }
        public int TollSacksPerBatch { get; }
        public EventVisibility Visibility { get; }

        internal static void RequireId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A stable milling configuration ID is required.", parameterName);
        }
    }

    /// <summary>
    /// Grinds delivered grain into flour one batch per tick. The miller's toll stays in the
    /// mill stock as grain: it is payment in kind, so no copper moves and the toll becomes
    /// the miller's own stock. Milling needs no progress state — the stock counts are the
    /// whole truth — so save/load is covered by the persisted inventories.
    /// </summary>
    public sealed class MillingSystem : IWorldSystem
    {
        private readonly List<MillingConfiguration> _configurations;

        public MillingSystem(IEnumerable<MillingConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<MillingConfiguration>();
            foreach (MillingConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Milling configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort(Compare);
            ValidateConfigurations();
        }

        public string Id => "economy.milling";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (MillingConfiguration configuration in _configurations)
            {
                if (configuration.MillStock.Count(configuration.Grain) < configuration.GrainPerBatch) continue;
                // The toll sacks are simply not ground: they stay in the mill stock as the
                // miller's grain. Only the remainder is removed and reappears as flour.
                int grind = configuration.GrainPerBatch - configuration.TollSacksPerBatch;
                configuration.MillStock.EnsureCanReceive(configuration.Flour, grind);
                state.Events.Append(state.Clock, configuration.Mill, WorldEventType.Produced,
                    ActorId.ForNpc(configuration.Miller), visibility: configuration.Visibility,
                    itemType: configuration.Flour, quantity: grind);
                if (!configuration.MillStock.TryRemove(configuration.Grain, grind))
                    throw new InvalidOperationException("Preflighted grain removal unexpectedly failed.");
                configuration.MillStock.Add(configuration.Flour, grind);
            }
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MillingConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Milling configuration IDs must be unique.", "configurations");
            for (int index = 1; index < _configurations.Count; index++)
                if (SameMill(_configurations[index - 1], _configurations[index]))
                    throw new ArgumentException("Mill configurations must be unique.", "configurations");
        }

        private static int Compare(MillingConfiguration left, MillingConfiguration right)
        {
            int comparison = left.Mill.CompareTo(right.Mill);
            if (comparison != 0) return comparison;
            comparison = left.Grain.CompareTo(right.Grain);
            if (comparison != 0) return comparison;
            comparison = left.Miller.CompareTo(right.Miller);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.Id, right.Id);
        }

        private static bool SameMill(MillingConfiguration left, MillingConfiguration right) =>
            left.Mill == right.Mill && left.Grain == right.Grain && left.Miller == right.Miller;
    }
}
