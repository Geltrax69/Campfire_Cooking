using System;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// What a cooked meal does to the eater and the village (P3-03, SKILLS.md section 2).
    /// Meal quality moves happiness by round((quality - 50) / 25): a small daily effect
    /// that compounds. Every meal is recorded as a MealEaten truth event carrying the
    /// quality. Feeding someone is a social act: a good meal (quality >= 60) is given
    /// as a Gift, which the P2-03 relationship system rewards with +1 trust; a great
    /// meal (quality >= 80) adds +1 trust directly, for +2 total once the Social phase
    /// runs. Health effects are future work: no health system exists yet.
    /// </summary>
    public static class MealEffects
    {
        /// <summary>Minimum meal quality that counts as a gift worth trusting for.</summary>
        public const int GiftQualityThreshold = 60;

        /// <summary>Meal quality that earns the extra direct trust point.</summary>
        public const int GreatMealQualityThreshold = 80;

        private const int DirectTrustBonus = 1;

        /// <summary>
        /// Happiness shift for a meal of the given quality: round((quality - 50) / 25).
        /// Quality 80 gives +1, 100 gives +2, 25 gives -1, 50 gives 0. Quality is an
        /// integer 0-100, so (quality - 50) / 25 never lands exactly on a .5 midpoint
        /// and the rounding mode never matters.
        /// </summary>
        public static int HappinessDeltaForQuality(int quality)
        {
            if (quality < 0 || quality > 100)
                throw new ArgumentOutOfRangeException(nameof(quality), "Meal quality is 0-100.");
            return (int)Math.Round((quality - 50) / 25.0);
        }

        /// <summary>
        /// An NPC eats a cooked meal of known quality: happiness shifts and a MealEaten
        /// truth event is recorded (the event's quantity carries the quality 0-100).
        /// Hunger is the caller's business — EatSystem handles ordinary meals from
        /// inventory, GiveMeal handles a meal eaten the moment it is given.
        /// </summary>
        public static void EatCookedMeal(NpcState eater, ItemTypeId item, int quality,
            WorldState state, LocationId location)
        {
            if (eater == null) throw new ArgumentNullException(nameof(eater));
            if (!item.IsValid) throw new ArgumentException("A meal needs a valid item type.", nameof(item));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!location.IsValid) throw new ArgumentException("A meal needs a valid location.", nameof(location));
            int delta = HappinessDeltaForQuality(quality); // validates the quality range
            eater.AdjustHappiness(delta);
            state.Events.Append(state.Clock, location, WorldEventType.MealEaten,
                ActorId.ForNpc(eater.Definition.Id), visibility: EventVisibility.Quiet,
                itemType: item, quantity: quality);
        }

        /// <summary>
        /// One NPC feeds another a cooked meal: the meal leaves the giver's inventory
        /// and is eaten on the spot (hunger restored from the catalog, happiness from
        /// the quality, MealEaten recorded). At quality >= 60 the feeding is also a
        /// Gift event for the P2-03 relationship system (+1 trust in the Social phase);
        /// at quality >= 80 the receiver's trust in the giver rises +1 more at once.
        /// Below 60 it is just food: no trust moves. A missing meal is a caller bug.
        /// </summary>
        public static void GiveMeal(WorldState state, ItemCatalog catalog, NpcId giver,
            NpcId receiver, ItemTypeId item, int quality, LocationId location)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!giver.IsValid) throw new ArgumentException("A giver is required.", nameof(giver));
            if (!receiver.IsValid) throw new ArgumentException("A receiver is required.", nameof(receiver));
            if (giver == receiver)
                throw new ArgumentException("Feeding yourself is eating, not giving.", nameof(receiver));
            if (!item.IsValid) throw new ArgumentException("A meal needs a valid item type.", nameof(item));
            if (!location.IsValid) throw new ArgumentException("A meal needs a valid location.", nameof(location));
            HappinessDeltaForQuality(quality); // validates the quality range early

            NpcState eater = state.Npcs[receiver]; // throws on unknown NPC: caller bug
            if (!state.Belongings.TryGet(ActorId.ForNpc(giver), out NpcBelongingsEntry holdings) ||
                !holdings.Inventory.TryRemove(item, 1))
                throw new InvalidOperationException(
                    "GiveMeal called without the meal in the giver's inventory: " + item.Value + ".");

            // Eaten on the spot: hunger from the catalog's fresh effect, happiness
            // from the quality, and the truth event.
            int restore = Math.Max(0, -(catalog[item].HungerEffect ?? 0));
            if (restore > 0) eater.Needs.ReduceHunger(restore);
            EatCookedMeal(eater, item, quality, state, location);

            if (quality < GiftQualityThreshold) return;

            // A good meal is a gift: the P2-03 dynamics system turns this into +1
            // trust (and +3 affection) for the receiver toward the giver.
            state.Events.Append(state.Clock, location, WorldEventType.Gift,
                ActorId.ForNpc(giver), new[] { ActorId.ForNpc(receiver) },
                EventVisibility.Quiet, itemType: item, quantity: 1);

            if (quality >= GreatMealQualityThreshold)
                AddDirectTrust(state, receiver, giver, location,
                    "was fed a fine meal by " + giver.Value);
        }

        /// <summary>
        /// A small immediate trust shift, mirroring RelationshipDynamicsSystem.ApplyShift
        /// (clamped 0-100, quiet RelationshipShift truth event) without duplicating its
        /// baseline bookkeeping: the reason extends the pair's current reason.
        /// </summary>
        private static void AddDirectTrust(WorldState state, NpcId from, NpcId to,
            LocationId location, string cause)
        {
            RelationshipRegistry registry = state.Knowledge.Relationships;
            int oldTrust = registry.Trust(from, to);
            int newTrust = Math.Min(100, oldTrust + DirectTrustBonus);
            if (newTrust == oldTrust) return; // already at the ceiling: nothing to log
            int affection = registry.Affection(from, to);
            string reason = registry.TryGet(from, to, out Relationship existing)
                ? existing.Reason + " — lately: " + cause
                : "Strangers — lately: " + cause;
            registry.Set(new Relationship(from, to, newTrust, affection, reason));
            state.Events.Append(state.Clock, location, WorldEventType.RelationshipShift,
                ActorId.ForNpc(from), new[] { ActorId.ForNpc(to) }, EventVisibility.Quiet);
        }
    }
}
