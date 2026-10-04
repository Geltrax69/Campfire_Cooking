using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// One recipient of travelers' coin: beds, ale, meals and horseshoes, paid as aggregate
    /// daily copper. The weight sets the payee's share of each month's traveler total.
    /// </summary>
    public sealed class TravelerPayee
    {
        public TravelerPayee(NpcId owner, Wallet wallet, LocationId location, int weight)
        {
            if (!owner.IsValid) throw new ArgumentException("An owner is required.", nameof(owner));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (!location.IsValid) throw new ArgumentException("A location is required.", nameof(location));
            if (weight < 1) throw new ArgumentOutOfRangeException(nameof(weight));
            Owner = owner;
            Wallet = wallet;
            Location = location;
            Weight = weight;
        }

        public NpcId Owner { get; }
        public Wallet Wallet { get; }
        public LocationId Location { get; }
        public int Weight { get; }
    }

    /// <summary>Immutable caller configuration for seasonal traveler spend.</summary>
    public sealed class TravelerConfiguration
    {
        public TravelerConfiguration(string id, IReadOnlyList<TravelerPayee> payees,
            EventVisibility visibility)
        {
            ProductionConfiguration.RequireId(id, nameof(id));
            if (payees == null) throw new ArgumentNullException(nameof(payees));
            if (payees.Count == 0) throw new ArgumentException("At least one payee is required.", nameof(payees));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            var seen = new HashSet<NpcId>();
            foreach (TravelerPayee payee in payees)
            {
                if (payee == null) throw new ArgumentException("Payees cannot contain null.", nameof(payees));
                if (!seen.Add(payee.Owner))
                    throw new ArgumentException("Each payee must be a different NPC.", nameof(payees));
            }
            Id = id;
            Payees = payees;
            Visibility = visibility;
        }

        public string Id { get; }
        public IReadOnlyList<TravelerPayee> Payees { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of traveler payouts: the last day paid and, for exact
    /// monthly totals under integer copper, how much each payee already received this month.
    /// Starts uninitialized; the system stays quiet until the world-build step initializes it.
    /// P2-12 extends the saver to write this state; until then use RestoreTravelerSpend.
    /// </summary>
    public sealed class TravelerSpendState
    {
        public TravelerSpendState(bool initialized = false, long lastPayoutDay = 0,
            int monthIndex = 0, IReadOnlyList<int> paidThisMonth = null)
        {
            if (initialized && lastPayoutDay < 0)
                throw new ArgumentOutOfRangeException(nameof(lastPayoutDay));
            IsInitialized = initialized;
            LastPayoutDay = lastPayoutDay;
            MonthIndex = monthIndex;
            PaidThisMonth = paidThisMonth == null
                ? new List<int>().AsReadOnly()
                : new List<int>(paidThisMonth).AsReadOnly();
        }

        public bool IsInitialized { get; }
        public long LastPayoutDay { get; internal set; }
        public int MonthIndex { get; internal set; }
        public IReadOnlyList<int> PaidThisMonth { get; internal set; }
    }

    /// <summary>
    /// Pays seasonal traveler spend into village tills (economy.travelers, Economy phase).
    /// Travelers on the Alder Road spend ~900 copper/month in summer and harvest, ~500 in
    /// spring, and almost nothing in winter when the ford runs high (ECONOMY.md §6). The coin
    /// comes from outside the village — the second named gate. Daily payouts use a cumulative
    /// formula so each payee's monthly total lands exactly on its share of the monthly rate
    /// despite integer copper.
    /// </summary>
    public sealed class TravelerSpendSystem : IWorldSystem
    {
        private readonly List<TravelerConfiguration> _configurations;

        // The village's evening room, from the approved Content/world/locations.json.
        // Only payees here feel the tavern-popularity bonus: longer stays mean more
        // ale and bed sales, not more horseshoes.
        private static readonly LocationId Tavern = new LocationId("loc_tavern");

        public TravelerSpendSystem(IEnumerable<TravelerConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<TravelerConfiguration>();
            foreach (TravelerConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Traveler configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "economy.travelers";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.TravelerSpend.IsInitialized) return;
            foreach (TravelerConfiguration configuration in _configurations)
                PayDueDays(state, configuration);
        }

        private static void PayDueDays(WorldState state, TravelerConfiguration configuration)
        {
            TravelerSpendState progress = state.TravelerSpend;
            while (progress.LastPayoutDay < state.Clock.Day)
            {
                progress.LastPayoutDay++;
                var day = new GameTime((progress.LastPayoutDay - 1) * 1440);
                int month = VillageCalendar.MonthIndex(day);
                if (month != progress.MonthIndex || progress.PaidThisMonth.Count != configuration.Payees.Count)
                {
                    progress.MonthIndex = month;
                    progress.PaidThisMonth = Zeroes(configuration.Payees.Count);
                }
                // The event is stamped at the current tick: backdating it to the payout day's
                // midnight could precede same-day events other systems already logged.
                PayDay(state, configuration, day, state.Clock, progress);
            }
        }

        private static void PayDay(WorldState state, TravelerConfiguration configuration,
            GameTime day, GameTime eventTime, TravelerSpendState progress)
        {
            int monthlyRate = MonthlyRate(VillageCalendar.SeasonAt(day));
            if (monthlyRate < 1) return;
            int totalWeight = 0;
            foreach (TravelerPayee payee in configuration.Payees)
                totalWeight += payee.Weight;
            int dayOfMonth = VillageCalendar.DayOfMonth(day);
            var paid = new List<int>(progress.PaidThisMonth);
            // P3-03: a renowned tavern keeps travelers an extra night or two per
            // 20 popularity above 50, which lands as proportionally more ale and
            // bed sales for the tavern's own payees.
            int extraNights = TavernPopularitySystem.ExtraNights(state.TavernPopularity.Popularity);
            for (int i = 0; i < configuration.Payees.Count; i++)
            {
                TravelerPayee payee = configuration.Payees[i];
                // Cumulative formula: the payee's exact monthly share, minus what they already
                // got this month, is what's due through today.
                int share = checked(monthlyRate * payee.Weight) / totalWeight;
                if (extraNights > 0 && payee.Location == Tavern)
                    share = checked(share * (VillageCalendar.DaysPerMonth + extraNights))
                        / VillageCalendar.DaysPerMonth;
                int due = checked(share * dayOfMonth) / VillageCalendar.DaysPerMonth - paid[i];
                if (due < 1) continue;
                state.Events.Append(eventTime, payee.Location, WorldEventType.Purchase,
                    ActorId.ForNpc(payee.Owner), visibility: configuration.Visibility,
                    copper: due);
                payee.Wallet.Credit(due);
                paid[i] += due;
            }
            progress.PaidThisMonth = paid.AsReadOnly();
        }

        private static IReadOnlyList<int> Zeroes(int count)
        {
            var zeroes = new List<int>(count);
            for (int i = 0; i < count; i++) zeroes.Add(0);
            return zeroes.AsReadOnly();
        }

        /// <summary>
        /// Monthly traveler spend by season (ECONOMY.md §6): the road is busy in summer and at
        /// harvest, quieter in spring, and all but empty in winter.
        /// </summary>
        internal static int MonthlyRate(Season season)
        {
            switch (season)
            {
                case Season.Summer: return 900;
                case Season.Autumn: return 900;
                case Season.Spring: return 500;
                default: return 0;
            }
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TravelerConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Traveler configuration IDs must be unique.", "configurations");
        }
    }
}
