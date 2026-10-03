using System;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// What eating one unit of an item does, given its freshness. Fresh goods apply the
    /// catalog effects; stale goods apply the approved `staleEffect` from the item's
    /// `perishable` block instead (replacing, not adding to, the fresh effects — a mushy
    /// apple is less filling, not differently filling). The future eat action calls here;
    /// no simulation system eats yet, so this is the documented hook.
    /// </summary>
    public static class Consumption
    {
        public static void EffectiveEffects(ItemDefinition definition, Freshness freshness,
            out int? hungerEffect, out int? healthEffect)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            PerishableInfo perishable = definition.Perishable;
            if (freshness == Freshness.Stale && perishable != null)
            {
                hungerEffect = perishable.StaleHungerEffect;
                healthEffect = perishable.StaleHealthEffect;
                return;
            }
            hungerEffect = definition.HungerEffect;
            healthEffect = definition.HealthEffect;
        }
    }
}
