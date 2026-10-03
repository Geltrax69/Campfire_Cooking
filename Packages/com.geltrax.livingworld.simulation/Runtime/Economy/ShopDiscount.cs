using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Computes a buyer-specific discount percent (0–20) for one shop purchase.</summary>
    public interface IShopDiscountPolicy
    {
        int DiscountPercentFor(ActorId buyer);
    }

    /// <summary>
    /// Integer-copper friend pricing. A shopkeeper may shave up to a fifth off the list
    /// price for a buyer they like; the copper taken off is always at least 1 when the
    /// percent is positive (so a friend's discount is visible even on a 2-copper ale),
    /// and the final price never drops below 1 copper (prices must stay positive).
    /// </summary>
    public static class ShopDiscount
    {
        /// <summary>Maximum friend discount: a shopkeeper never gives away more than a fifth of the price.</summary>
        public const int MaxDiscountPercent = 20;

        public static int DiscountedPrice(int unitPrice, int discountPercent)
        {
            if (unitPrice <= 0) throw new ArgumentOutOfRangeException(nameof(unitPrice));
            int percent = Math.Max(0, Math.Min(MaxDiscountPercent, discountPercent));
            if (percent == 0) return unitPrice;
            int copperOff = Math.Max(1, unitPrice * percent / 100);
            return Math.Max(1, unitPrice - copperOff);
        }
    }
}
