using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable caller configuration for the community fund.</summary>
    public sealed class CommunityFundConfiguration
    {
        public CommunityFundConfiguration(string id, int monthlyCopper, int feastCopper,
            LocationId location, EventVisibility visibility)
        {
            ProductionConfiguration.RequireId(id, nameof(id));
            if (monthlyCopper < 1) throw new ArgumentOutOfRangeException(nameof(monthlyCopper));
            if (feastCopper < 1) throw new ArgumentOutOfRangeException(nameof(feastCopper));
            if (!location.IsValid) throw new ArgumentException("A location is required.", nameof(location));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            MonthlyCopper = monthlyCopper;
            FeastCopper = feastCopper;
            Location = location;
            Visibility = visibility;
        }

        public string Id { get; }
        public int MonthlyCopper { get; }
        public int FeastCopper { get; }
        public LocationId Location { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of the community fund: the two named pots and the
    /// last scheduled movement. The pots are savings, not destruction — they stay in the
    /// village total. Starts uninitialized; the system stays quiet until the world-build
    /// step installs it. P2-12 extends the saver to write this state; until then use
    /// RestoreCommunityFund.
    /// </summary>
    public sealed class CommunityFundState
    {
        public CommunityFundState(bool initialized = false, long lastMonthlyDay = 0, long lastFeastYear = 0)
        {
            if (initialized && lastMonthlyDay < 1)
                throw new ArgumentOutOfRangeException(nameof(lastMonthlyDay));
            if (lastFeastYear < 0) throw new ArgumentOutOfRangeException(nameof(lastFeastYear));
            IsInitialized = initialized;
            CommunityPot = new Wallet();
            FeastPot = new Wallet();
            LastMonthlyDay = lastMonthlyDay;
            LastFeastYear = lastFeastYear;
        }

        public bool IsInitialized { get; }
        public Wallet CommunityPot { get; }
        public Wallet FeastPot { get; }
        public long LastMonthlyDay { get; internal set; }
        public long LastFeastYear { get; internal set; }
    }

    /// <summary>
    /// The village council's savings (economy.community_fund, Economy phase), twice
    /// scheduled (ECONOMY.md §6). Every thirty days the council moves the monthly share
    /// from the village fund into the community pot — the shared chest for hard winters —
    /// taking only what is there (a lean fund sets aside a lean share, never going
    /// negative). Each autumn it sets aside the feast reserve for the Harvest Feast.
    /// Both pots are named containers in the village total: moving copper into them is
    /// saving, not spending. Movements are zero-quantity
    /// <see cref="WorldEventType.Purchase"/> events (the Payroll convention) with no
    /// actor, logged with the actual amount moved.
    /// </summary>
    public sealed class CommunityFundSystem : IWorldSystem
    {
        private readonly List<CommunityFundConfiguration> _configurations;

        public CommunityFundSystem(IEnumerable<CommunityFundConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<CommunityFundConfiguration>();
            foreach (CommunityFundConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Community fund configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "economy.community_fund";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.CommunityFund.IsInitialized) return;
            foreach (CommunityFundConfiguration configuration in _configurations)
            {
                while (state.CommunityFund.LastMonthlyDay + 30 <= state.Clock.Day)
                {
                    state.CommunityFund.LastMonthlyDay += 30;
                    MoveMonthly(state, configuration);
                }
                long year = (state.Clock.Day - 1) / VillageCalendar.DaysPerYear + 1;
                if (state.CommunityFund.LastFeastYear < year &&
                    VillageCalendar.SeasonAt(state.Clock) == Season.Autumn)
                {
                    state.CommunityFund.LastFeastYear = year;
                    MoveFeast(state, configuration);
                }
            }
        }

        private static void MoveMonthly(WorldState state, CommunityFundConfiguration configuration)
        {
            int available = state.VillageFund.Funds.Balance;
            int moved = Math.Min(configuration.MonthlyCopper, available);
            if (moved < 1) return;
            state.VillageFund.Funds.TransferTo(state.CommunityFund.CommunityPot, moved);
            state.Events.Append(state.Clock, configuration.Location, WorldEventType.Purchase, null,
                visibility: configuration.Visibility, quantity: 0, copper: moved);
        }

        private static void MoveFeast(WorldState state, CommunityFundConfiguration configuration)
        {
            int available = state.VillageFund.Funds.Balance;
            int moved = Math.Min(configuration.FeastCopper, available);
            if (moved < 1) return;
            state.VillageFund.Funds.TransferTo(state.CommunityFund.FeastPot, moved);
            state.Events.Append(state.Clock, configuration.Location, WorldEventType.Purchase, null,
                visibility: configuration.Visibility, quantity: 0, copper: moved);
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CommunityFundConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Community fund configuration IDs must be unique.", "configurations");
        }
    }
}
