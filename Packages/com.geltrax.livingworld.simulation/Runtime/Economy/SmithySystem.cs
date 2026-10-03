using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>One forge recipe: a fixed iron cost per batch and a make-to-stock target.</summary>
    public sealed class SmithyRecipe
    {
        public SmithyRecipe(ItemTypeId product, int ironKgPerBatch, int unitsPerBatch, int targetStock)
        {
            if (!product.IsValid) throw new ArgumentException("A valid product item is required.", nameof(product));
            if (ironKgPerBatch < 1) throw new ArgumentOutOfRangeException(nameof(ironKgPerBatch));
            if (unitsPerBatch < 1) throw new ArgumentOutOfRangeException(nameof(unitsPerBatch));
            if (targetStock < 0) throw new ArgumentOutOfRangeException(nameof(targetStock));
            Product = product;
            IronKgPerBatch = ironKgPerBatch;
            UnitsPerBatch = unitsPerBatch;
            TargetStock = targetStock;
        }

        public ItemTypeId Product { get; }
        public int IronKgPerBatch { get; }
        public int UnitsPerBatch { get; }
        public int TargetStock { get; }
    }

    /// <summary>Immutable caller configuration for one smithy's forge.</summary>
    public sealed class SmithyConfiguration
    {
        public SmithyConfiguration(string id, LocationId smithy, NpcId smith, Inventory stock,
            ItemTypeId iron, IReadOnlyList<SmithyRecipe> recipes,
            int ironOrderQuantityKg, int ironOrderPricePerKg, EventVisibility visibility)
        {
            ProductionConfiguration.RequireId(id, nameof(id));
            if (!smithy.IsValid) throw new ArgumentException("A smithy location is required.", nameof(smithy));
            if (!smith.IsValid) throw new ArgumentException("A smith is required.", nameof(smith));
            if (stock == null) throw new ArgumentNullException(nameof(stock));
            if (!iron.IsValid) throw new ArgumentException("An iron stock item is required.", nameof(iron));
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            if (recipes.Count == 0) throw new ArgumentException("At least one recipe is required.", nameof(recipes));
            stock.Count(iron);
            var seen = new HashSet<ItemTypeId>();
            foreach (SmithyRecipe recipe in recipes)
            {
                if (recipe == null)
                    throw new ArgumentException("Recipes cannot contain null.", nameof(recipes));
                if (recipe.Product == iron)
                    throw new ArgumentException("A recipe cannot produce the iron stock itself.", nameof(recipes));
                if (!seen.Add(recipe.Product))
                    throw new ArgumentException("Each recipe must produce a different item.", nameof(recipes));
                stock.Count(recipe.Product);
            }
            if (ironOrderQuantityKg < 1) throw new ArgumentOutOfRangeException(nameof(ironOrderQuantityKg));
            if (ironOrderPricePerKg < 1) throw new ArgumentOutOfRangeException(nameof(ironOrderPricePerKg));
            _ = checked(ironOrderQuantityKg * ironOrderPricePerKg);
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            Smithy = smithy;
            Smith = smith;
            Stock = stock;
            Iron = iron;
            Recipes = recipes;
            IronOrderQuantityKg = ironOrderQuantityKg;
            IronOrderPricePerKg = ironOrderPricePerKg;
            Visibility = visibility;
        }

        public string Id { get; }
        public LocationId Smithy { get; }
        public NpcId Smith { get; }
        public Inventory Stock { get; }
        public ItemTypeId Iron { get; }
        public IReadOnlyList<SmithyRecipe> Recipes { get; }
        public int IronOrderQuantityKg { get; }
        public int IronOrderPricePerKg { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of the forge's one-shot progress: whether the
    /// iron-exhaustion order has been logged since the last iron delivery. The stock counts
    /// themselves are the rest of the truth, so save/load of inventories covers them.
    /// P2-12 extends the saver to write this state; until then use RestoreSmithy directly.
    /// P2-08 merchants call <see cref="ResetIronExhaustion"/> when iron arrives, so the
    /// next depletion logs a fresh order.
    /// </summary>
    public sealed class SmithyState
    {
        public SmithyState(bool ironExhaustionOrdered = false)
        {
            IronExhaustionOrdered = ironExhaustionOrdered;
        }

        public bool IronExhaustionOrdered { get; private set; }

        internal void MarkIronExhaustionOrdered() => IronExhaustionOrdered = true;

        internal void ResetIronExhaustion() => IronExhaustionOrdered = false;
    }

    /// <summary>
    /// Runs each smithy's forge once per tick: one batch per recipe while the rack sits below
    /// its target and iron covers the batch cost. Iron is imported and finite (ECONOMY.md):
    /// when the stock can no longer cover the cheapest batch, production stops and the smith
    /// logs a single standing iron order for the next traveling merchant (fulfilled in P2-08).
    /// Batch order follows the configuration's recipe list, so the run is deterministic.
    /// </summary>
    public sealed class SmithySystem : IWorldSystem
    {
        private readonly List<SmithyConfiguration> _configurations;

        public SmithySystem(IEnumerable<SmithyConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<SmithyConfiguration>();
            foreach (SmithyConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Smithy configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort(Compare);
            ValidateConfigurations();
        }

        public string Id => "economy.smithy";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (SmithyConfiguration configuration in _configurations)
            {
                ForgeBatches(state, configuration);
                MaybeOrderIron(state, configuration);
            }
        }

        private static void ForgeBatches(WorldState state, SmithyConfiguration configuration)
        {
            foreach (SmithyRecipe recipe in configuration.Recipes)
            {
                if (configuration.Stock.Count(recipe.Product) >= recipe.TargetStock) continue;
                if (configuration.Stock.Count(configuration.Iron) < recipe.IronKgPerBatch) continue;
                configuration.Stock.EnsureCanReceive(recipe.Product, recipe.UnitsPerBatch);
                // Append first: the following single-threaded transfers were preflighted and cannot fail.
                state.Events.Append(state.Clock, configuration.Smithy, WorldEventType.Produced,
                    ActorId.ForNpc(configuration.Smith), visibility: configuration.Visibility,
                    itemType: recipe.Product, quantity: recipe.UnitsPerBatch);
                if (!configuration.Stock.TryRemove(configuration.Iron, recipe.IronKgPerBatch))
                    throw new InvalidOperationException("Preflighted iron removal unexpectedly failed.");
                configuration.Stock.Add(recipe.Product, recipe.UnitsPerBatch);
            }
        }

        private static void MaybeOrderIron(WorldState state, SmithyConfiguration configuration)
        {
            if (state.Smithy.IronExhaustionOrdered) return;
            int cheapestBatchKg = int.MaxValue;
            foreach (SmithyRecipe recipe in configuration.Recipes)
                if (recipe.IronKgPerBatch < cheapestBatchKg) cheapestBatchKg = recipe.IronKgPerBatch;
            if (configuration.Stock.Count(configuration.Iron) >= cheapestBatchKg) return;
            // The forge is cold: no recipe can run. Doran logs a standing order at the design-doc
            // merchant price (~10 copper/kg); P2-08 merchants will read RestockOrdered events.
            int copper = checked(configuration.IronOrderQuantityKg * configuration.IronOrderPricePerKg);
            state.Events.Append(state.Clock, configuration.Smithy, WorldEventType.RestockOrdered,
                ActorId.ForNpc(configuration.Smith), visibility: configuration.Visibility,
                itemType: configuration.Iron, quantity: configuration.IronOrderQuantityKg, copper: copper);
            state.Smithy.MarkIronExhaustionOrdered();
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SmithyConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Smithy configuration IDs must be unique.", "configurations");
            for (int index = 1; index < _configurations.Count; index++)
                if (SameSmithy(_configurations[index - 1], _configurations[index]))
                    throw new ArgumentException("Smithy configurations must be unique.", "configurations");
        }

        private static int Compare(SmithyConfiguration left, SmithyConfiguration right)
        {
            int comparison = left.Smithy.CompareTo(right.Smithy);
            if (comparison != 0) return comparison;
            comparison = left.Smith.CompareTo(right.Smith);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.Id, right.Id);
        }

        private static bool SameSmithy(SmithyConfiguration left, SmithyConfiguration right) =>
            left.Smithy == right.Smithy && left.Smith == right.Smith;
    }
}
