using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Caller-owned, restorable record of the village's copper baseline: the total village
    /// copper when the sinks were wired (world-open). The tax assessment and the import
    /// order size both scale against it, so good years pay more and lean years less —
    /// the stabilizer from ECONOMY.md §6. P2-12 extends the saver to write this state;
    /// until then use RestoreEconomyBaseline.
    /// </summary>
    public sealed class EconomyBaselineState
    {
        public EconomyBaselineState(bool initialized = false, long baselineCopper = 0)
        {
            if (initialized && baselineCopper < 1)
                throw new ArgumentOutOfRangeException(nameof(baselineCopper));
            IsInitialized = initialized;
            BaselineCopper = baselineCopper;
        }

        public bool IsInitialized { get; }
        public long BaselineCopper { get; }
    }

    /// <summary>
    /// The village's visible prosperity: total copper held across every personal wallet,
    /// shop till, business belonging and named pot, against the world-open baseline.
    /// The reeve can see a fat purse and a thin one; the assessment follows what he sees.
    /// Wallets shared between two owners (the apple stall's till is Mira's purse) are
    /// counted once — money, not containers, is what prosperity measures.
    /// </summary>
    public static class ProsperityIndex
    {
        public static long TotalVillageCopper(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var seen = new HashSet<Wallet>();
            long total = 0;
            foreach (NpcBelongingsEntry entry in state.Belongings.Entries)
                total += AddOnce(seen, entry.Wallet);
            foreach (Shop shop in state.Shops.Shops)
                total += AddOnce(seen, shop.OwnerWallet);
            total += AddOnce(seen, state.VillageFund.Funds);
            total += AddOnce(seen, state.CommunityFund.CommunityPot);
            total += AddOnce(seen, state.CommunityFund.FeastPot);
            return total;
        }

        private static long AddOnce(HashSet<Wallet> seen, Wallet wallet)
        {
            if (wallet == null || !seen.Add(wallet)) return 0;
            return wallet.Balance;
        }

        /// <summary>
        /// Scales a base copper amount by prosperity (total/baseline), clamped to
        /// [base/2, base*2]: the reeve's assessment rises in good years and falls in bad
        /// ones, but never doubles the burden or halves it away. Integer math throughout.
        /// </summary>
        public static int ScaleByProsperity(int baseCopper, long totalCopper, long baselineCopper)
        {
            if (baseCopper < 1) throw new ArgumentOutOfRangeException(nameof(baseCopper));
            if (totalCopper < 0) throw new ArgumentOutOfRangeException(nameof(totalCopper));
            if (baselineCopper < 1) throw new ArgumentOutOfRangeException(nameof(baselineCopper));
            long scaled = (long)baseCopper * totalCopper / baselineCopper;
            long floor = Math.Max(1, baseCopper / 2);
            long ceiling = (long)baseCopper * 2;
            return (int)Math.Max(floor, Math.Min(ceiling, scaled));
        }
    }
}
