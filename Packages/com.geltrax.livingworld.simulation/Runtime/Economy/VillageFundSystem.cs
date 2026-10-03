using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>One NPC who pays the weekly household levy, and the wallet it comes from.</summary>
    public sealed class LevyPayer
    {
        public LevyPayer(NpcId payer, Wallet wallet)
        {
            if (!payer.IsValid) throw new ArgumentException("A payer is required.", nameof(payer));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            Payer = payer;
            Wallet = wallet;
        }

        public NpcId Payer { get; }
        public Wallet Wallet { get; }
    }

    /// <summary>One NPC paid a daily wage from the village fund, and where the wage is earned.</summary>
    public sealed class WageEarner
    {
        public WageEarner(NpcId worker, Wallet wallet, int copperPerDay, LocationId workplace)
        {
            if (!worker.IsValid) throw new ArgumentException("A worker is required.", nameof(worker));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (copperPerDay < 1) throw new ArgumentOutOfRangeException(nameof(copperPerDay));
            if (!workplace.IsValid) throw new ArgumentException("A workplace is required.", nameof(workplace));
            Worker = worker;
            Wallet = wallet;
            CopperPerDay = copperPerDay;
            Workplace = workplace;
        }

        public NpcId Worker { get; }
        public Wallet Wallet { get; }
        public int CopperPerDay { get; }
        public LocationId Workplace { get; }
    }

    /// <summary>Immutable caller configuration for the village fund.</summary>
    public sealed class VillageFundConfiguration
    {
        public VillageFundConfiguration(string id, IReadOnlyList<LevyPayer> levyPayers,
            int levyCopperPerWeek, IReadOnlyList<WageEarner> wageEarners,
            NpcId retainerWorker, Wallet retainerWallet, LocationId retainerWorkplace,
            int retainerCopperPerWeek, int backgroundCopperPerWeek,
            LocationId levyLocation, EventVisibility visibility)
        {
            ProductionConfiguration.RequireId(id, nameof(id));
            if (levyPayers == null) throw new ArgumentNullException(nameof(levyPayers));
            if (levyCopperPerWeek < 1) throw new ArgumentOutOfRangeException(nameof(levyCopperPerWeek));
            if (wageEarners == null) throw new ArgumentNullException(nameof(wageEarners));
            if (!retainerWorker.IsValid) throw new ArgumentException("A retainer worker is required.", nameof(retainerWorker));
            if (retainerWallet == null) throw new ArgumentNullException(nameof(retainerWallet));
            if (!retainerWorkplace.IsValid) throw new ArgumentException("A workplace is required.", nameof(retainerWorkplace));
            if (retainerCopperPerWeek < 1) throw new ArgumentOutOfRangeException(nameof(retainerCopperPerWeek));
            if (backgroundCopperPerWeek < 0) throw new ArgumentOutOfRangeException(nameof(backgroundCopperPerWeek));
            if (!levyLocation.IsValid) throw new ArgumentException("A levy location is required.", nameof(levyLocation));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            var seen = new HashSet<NpcId>();
            foreach (LevyPayer payer in levyPayers)
            {
                if (payer == null) throw new ArgumentException("Levy payers cannot contain null.", nameof(levyPayers));
                if (!seen.Add(payer.Payer))
                    throw new ArgumentException("Each levy payer must be listed once.", nameof(levyPayers));
            }
            Id = id;
            LevyPayers = levyPayers;
            LevyCopperPerWeek = levyCopperPerWeek;
            WageEarners = wageEarners;
            RetainerWorker = retainerWorker;
            RetainerWallet = retainerWallet;
            RetainerWorkplace = retainerWorkplace;
            RetainerCopperPerWeek = retainerCopperPerWeek;
            BackgroundCopperPerWeek = backgroundCopperPerWeek;
            LevyLocation = levyLocation;
            Visibility = visibility;
        }

        public string Id { get; }
        public IReadOnlyList<LevyPayer> LevyPayers { get; }
        public int LevyCopperPerWeek { get; }
        public IReadOnlyList<WageEarner> WageEarners { get; }
        public NpcId RetainerWorker { get; }
        public Wallet RetainerWallet { get; }
        public LocationId RetainerWorkplace { get; }
        public int RetainerCopperPerWeek { get; }
        public int BackgroundCopperPerWeek { get; }
        public LocationId LevyLocation { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of the village fund: its wallet and the last day each
    /// recurring flow ran. Starts uninitialized; the system stays quiet until the world-build
    /// step installs it. P2-12 extends the saver to write this state; until then use
    /// RestoreVillageFund.
    /// </summary>
    public sealed class VillageFundState
    {
        public VillageFundState(bool initialized = false, int startingCopper = 0)
        {
            if (startingCopper < 0) throw new ArgumentOutOfRangeException(nameof(startingCopper));
            IsInitialized = initialized;
            Funds = new Wallet(startingCopper);
        }

        public bool IsInitialized { get; }
        public Wallet Funds { get; }
        public long LastLevyDay { get; internal set; }
        public long LastWageDay { get; internal set; }
        public long LastRetainerDay { get; internal set; }
    }

    /// <summary>
    /// Runs the village fund (economy.fund, Economy phase): the weekly household levy in,
    /// the abstracted background villagers' share in, and Bram's guard wage, Elswith's council
    /// stipend, and Ralf's winter retainer out (ECONOMY.md §7). Wages are redistribution —
    /// no new money — while the levy and background payments are the fund's income. A payer
    /// who cannot cover the levy is logged, not covered: the shortfall is visible for the
    /// debt system (P2-10).
    /// </summary>
    public sealed class VillageFundSystem : IWorldSystem
    {
        private readonly List<VillageFundConfiguration> _configurations;

        public VillageFundSystem(IEnumerable<VillageFundConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<VillageFundConfiguration>();
            foreach (VillageFundConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Fund configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "economy.fund";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.VillageFund.IsInitialized) return;
            foreach (VillageFundConfiguration configuration in _configurations)
            {
                PayDailyWages(state, configuration);
                CollectWeeklyLevies(state, configuration);
                PayWinterRetainer(state, configuration);
            }
        }

        private static void PayDailyWages(WorldState state, VillageFundConfiguration configuration)
        {
            VillageFundState fund = state.VillageFund;
            while (fund.LastWageDay < state.Clock.Day)
            {
                fund.LastWageDay++;
                foreach (WageEarner earner in configuration.WageEarners)
                    Payroll.PayWage(state, fund.Funds, earner.Wallet, earner.Worker,
                        earner.Workplace, earner.CopperPerDay, configuration.Visibility);
            }
        }

        private static void CollectWeeklyLevies(WorldState state, VillageFundConfiguration configuration)
        {
            VillageFundState fund = state.VillageFund;
            while (fund.LastLevyDay + 7 <= state.Clock.Day)
            {
                fund.LastLevyDay += 7;
                foreach (LevyPayer payer in configuration.LevyPayers)
                    Payroll.ChargeLevy(state, payer.Wallet, fund.Funds, payer.Payer,
                        configuration.LevyLocation, configuration.LevyCopperPerWeek,
                        configuration.Visibility);
                // The ~100 background villagers are abstracted: their share arrives as a
                // single weekly payment (ECONOMY.md §7). No actor — they are off-screen.
                if (configuration.BackgroundCopperPerWeek > 0)
                {
                    state.Events.Append(state.Clock, configuration.LevyLocation,
                        WorldEventType.Purchase, null, visibility: configuration.Visibility,
                        quantity: 0, copper: configuration.BackgroundCopperPerWeek);
                    fund.Funds.Credit(configuration.BackgroundCopperPerWeek);
                }
            }
        }

        private static void PayWinterRetainer(WorldState state, VillageFundConfiguration configuration)
        {
            VillageFundState fund = state.VillageFund;
            while (fund.LastRetainerDay + 7 <= state.Clock.Day)
            {
                fund.LastRetainerDay += 7;
                if (VillageCalendar.SeasonAt(state.Clock) != Season.Winter) continue;
                Payroll.PayWage(state, fund.Funds, configuration.RetainerWallet,
                    configuration.RetainerWorker, configuration.RetainerWorkplace,
                    configuration.RetainerCopperPerWeek, configuration.Visibility);
            }
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (VillageFundConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Fund configuration IDs must be unique.", "configurations");
        }
    }
}
