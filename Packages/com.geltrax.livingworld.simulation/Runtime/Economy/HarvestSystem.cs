using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>One harvest hand on Corvin's farm, and the wallet the wage goes to.</summary>
    public sealed class HarvestHand
    {
        public HarvestHand(NpcId worker, Wallet wallet)
        {
            if (!worker.IsValid) throw new ArgumentException("A worker is required.", nameof(worker));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            Worker = worker;
            Wallet = wallet;
        }

        public NpcId Worker { get; }
        public Wallet Wallet { get; }
    }

    /// <summary>Immutable caller configuration for the autumn harvest wages.</summary>
    public sealed class HarvestConfiguration
    {
        public HarvestConfiguration(string id, NpcId payer, Wallet payerWallet,
            IReadOnlyList<HarvestHand> hands, int wagePerDayCopper, LocationId farm,
            EventVisibility visibility)
        {
            ProductionConfiguration.RequireId(id, nameof(id));
            if (!payer.IsValid) throw new ArgumentException("A payer is required.", nameof(payer));
            if (payerWallet == null) throw new ArgumentNullException(nameof(payerWallet));
            if (hands == null) throw new ArgumentNullException(nameof(hands));
            if (wagePerDayCopper < 1) throw new ArgumentOutOfRangeException(nameof(wagePerDayCopper));
            if (!farm.IsValid) throw new ArgumentException("A farm location is required.", nameof(farm));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            var seen = new HashSet<NpcId>();
            foreach (HarvestHand hand in hands)
            {
                if (hand == null) throw new ArgumentException("Hands cannot contain null.", nameof(hands));
                if (!seen.Add(hand.Worker))
                    throw new ArgumentException("Each hand must be listed once.", nameof(hands));
            }
            Id = id;
            Payer = payer;
            PayerWallet = payerWallet;
            Hands = hands;
            WagePerDayCopper = wagePerDayCopper;
            Farm = farm;
            Visibility = visibility;
        }

        public string Id { get; }
        public NpcId Payer { get; }
        public Wallet PayerWallet { get; }
        public IReadOnlyList<HarvestHand> Hands { get; }
        public int WagePerDayCopper { get; }
        public LocationId Farm { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of harvest wage payments: the last day wages ran.
    /// Starts uninitialized; the system stays quiet until the world-build step initializes it.
    /// P2-12 extends the saver to write this state; until then use RestoreHarvest.
    /// </summary>
    public sealed class HarvestState
    {
        public HarvestState(bool initialized = false, long lastWageDay = 0)
        {
            IsInitialized = initialized;
            LastWageDay = lastWageDay;
        }

        public bool IsInitialized { get; }
        public long LastWageDay { get; internal set; }
    }

    /// <summary>
    /// Pays the autumn harvest wages (economy.harvest, Economy phase): Corvin pays each
    /// registered hand 8 copper/day through the harvest season (ECONOMY.md §7). The hands are
    /// registered when someone takes harvest work — the named villagers are unpaid family
    /// labor, so the default wiring starts empty and the rule waits for workers (the player's
    /// market, or background hands a later task names).
    /// </summary>
    public sealed class HarvestSystem : IWorldSystem
    {
        private readonly List<HarvestConfiguration> _configurations;

        public HarvestSystem(IEnumerable<HarvestConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<HarvestConfiguration>();
            foreach (HarvestConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Harvest configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "economy.harvest";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Harvest.IsInitialized) return;
            if (VillageCalendar.SeasonAt(state.Clock) != Season.Autumn) return;
            foreach (HarvestConfiguration configuration in _configurations)
                PayHands(state, configuration);
        }

        private static void PayHands(WorldState state, HarvestConfiguration configuration)
        {
            HarvestState progress = state.Harvest;
            while (progress.LastWageDay < state.Clock.Day)
            {
                progress.LastWageDay++;
                foreach (HarvestHand hand in configuration.Hands)
                    Payroll.PayWage(state, configuration.PayerWallet, hand.Wallet, hand.Worker,
                        configuration.Farm, configuration.WagePerDayCopper, configuration.Visibility);
            }
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (HarvestConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Harvest configuration IDs must be unique.", "configurations");
        }
    }
}
