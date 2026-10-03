using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Scales a merchant import offer's target stock for the current village. Called from
    /// MerchantSystem.SellImports when a configuration carries a policy; without one the
    /// offer's fixed target applies (the P2-08 behaviour).
    /// </summary>
    public interface IImportOrderPolicy
    {
        int ScaledTarget(MerchantImportOffer offer, WorldState state);
    }

    /// <summary>
    /// The adaptive import list (ECONOMY.md §6): Tilda's order sizes follow the village's
    /// visible prosperity against the world-open baseline, clamped to [50%, 150%] of the
    /// approved targets. Good years see bigger orders (more copper leaves through the
    /// import gate); lean years see smaller ones. Doran's exhaustion-gated iron is
    /// need-based, not prosperity-based — forty bars when the smithy runs dry, whatever
    /// the weather — so it keeps its fixed target.
    /// </summary>
    public sealed class ProsperityImportPolicy : IImportOrderPolicy
    {
        private const int MinPercent = 50;
        private const int MaxPercent = 150;

        public int ScaledTarget(MerchantImportOffer offer, WorldState state)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (offer.TargetStock < 1) throw new ArgumentOutOfRangeException(nameof(offer));
            if (offer.RequiresExhaustionOrder || !state.EconomyBaseline.IsInitialized)
                return offer.TargetStock;
            long total = ProsperityIndex.TotalVillageCopper(state);
            long baseline = state.EconomyBaseline.BaselineCopper;
            long percent = total * 100 / baseline;
            percent = Math.Max(MinPercent, Math.Min(MaxPercent, percent));
            return Math.Max(1, (int)(offer.TargetStock * percent / 100));
        }
    }
}
