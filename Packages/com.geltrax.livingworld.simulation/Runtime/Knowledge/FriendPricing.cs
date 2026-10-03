using System;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Friend prices from the shopkeeper's own directed relationships: the discount for a
    /// buyer comes from what the owner feels about them (trust + affection), never from
    /// world truth. Neutral (50/50) pays full price; a perfect 100/100 friendship earns
    /// the full 20%; the scale is linear between. The player and strangers get no discount.
    /// </summary>
    public static class FriendPricing
    {
        /// <summary>
        /// Discount percent from directed trust and affection: (trust + affection − 100)
        /// scaled so 100/100 earns the 20% cap, clamped to 0–20.
        /// </summary>
        public static int DiscountPercent(int trust, int affection)
        {
            if (trust < 0 || trust > 100) throw new ArgumentOutOfRangeException(nameof(trust));
            if (affection < 0 || affection > 100) throw new ArgumentOutOfRangeException(nameof(affection));
            int scaled = (trust + affection - 100) * ShopDiscount.MaxDiscountPercent / 100;
            return Math.Max(0, Math.Min(ShopDiscount.MaxDiscountPercent, scaled));
        }

        /// <summary>
        /// A discount policy for one shopkeeper's shop, reading their live directed
        /// relationships: as feelings change, so do the prices their friends pay.
        /// </summary>
        public static IShopDiscountPolicy ForShopkeeper(WorldState state, NpcId owner)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!owner.IsValid) throw new ArgumentException("A shopkeeper needs a valid NPC id.", nameof(owner));
            return new RelationshipDiscountPolicy(state, owner);
        }

        private sealed class RelationshipDiscountPolicy : IShopDiscountPolicy
        {
            private readonly WorldState _state;
            private readonly NpcId _owner;

            public RelationshipDiscountPolicy(WorldState state, NpcId owner)
            {
                _state = state;
                _owner = owner;
            }

            public int DiscountPercentFor(ActorId buyer)
            {
                if (!buyer.IsValid || buyer.IsPlayer || !buyer.Npc.HasValue) return 0;
                RelationshipRegistry registry = _state.Knowledge.Relationships;
                NpcId buyerNpc = buyer.Npc.Value;
                return DiscountPercent(registry.Trust(_owner, buyerNpc), registry.Affection(_owner, buyerNpc));
            }
        }
    }
}
