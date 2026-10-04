using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable caller configuration for the crown's wolf bounties.</summary>
    public sealed class WolfBountyConfiguration
    {
        public WolfBountyConfiguration(string id, NpcId hunter, Wallet hunterWallet,
            LocationId location, int bountyPerPeltCopper, EventVisibility visibility)
        {
            ProductionConfiguration.RequireId(id, nameof(id));
            if (!hunter.IsValid) throw new ArgumentException("A hunter is required.", nameof(hunter));
            if (hunterWallet == null) throw new ArgumentNullException(nameof(hunterWallet));
            if (!location.IsValid) throw new ArgumentException("A location is required.", nameof(location));
            if (bountyPerPeltCopper < 1) throw new ArgumentOutOfRangeException(nameof(bountyPerPeltCopper));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            Hunter = hunter;
            HunterWallet = hunterWallet;
            Location = location;
            BountyPerPeltCopper = bountyPerPeltCopper;
            Visibility = visibility;
        }

        public string Id { get; }
        public NpcId Hunter { get; }
        public Wallet HunterWallet { get; }
        public LocationId Location { get; }
        public int BountyPerPeltCopper { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of wolf bounties: which winter year has a rolled
    /// schedule and which bounty days of that winter are still unpaid. Starts uninitialized;
    /// the system stays quiet until the world-build step initializes it. P2-12 extends the
    /// saver to write this state; until then use RestoreWolfBounty.
    /// </summary>
    public sealed class WolfBountyState
    {
        public WolfBountyState(bool initialized = false, long winterYear = -1,
            IReadOnlyList<long> bountyDays = null)
        {
            IsInitialized = initialized;
            WinterYear = winterYear;
            BountyDays = bountyDays == null
                ? new List<long>().AsReadOnly()
                : new List<long>(bountyDays).AsReadOnly();
        }

        public bool IsInitialized { get; }
        public long WinterYear { get; internal set; }
        public IReadOnlyList<long> BountyDays { get; internal set; }
    }

    /// <summary>
    /// Pays the crown's wolf bounties (economy.bounty, Economy phase): 50 copper per pelt,
    /// 2-4 incidents per winter, winter only (ECONOMY.md §6 — the village's winter windfall).
    /// The coin comes from the crown, outside the village — the third named gate. Each
    /// winter's incident days are drawn once from the shared seeded RNG, so the run is
    /// deterministic.
    ///
    /// Pelt economics (P4-02): a wolf pelt is worth 10 copper base (Content/items/items.json);
    /// the crown's 50-copper winter bounty makes wolf work pay. When a villager kills a wolf
    /// they get the pelt's value — 10 copper, or 50 in winter with the bounty. Actual wolf
    /// hunting by villagers is future work (it needs the hunting system); this system only
    /// pays the scheduled winter bounties.
    /// </summary>
    public sealed class WolfBountySystem : IWorldSystem
    {
        /// <summary>Wolf pelt, per Content/items/items.json.</summary>
        public static readonly ItemTypeId Pelt = new ItemTypeId("item_pelt");

        private readonly List<WolfBountyConfiguration> _configurations;

        public WolfBountySystem(IEnumerable<WolfBountyConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<WolfBountyConfiguration>();
            foreach (WolfBountyConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Bounty configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "economy.bounty";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.WolfBounty.IsInitialized) return;
            if (VillageCalendar.SeasonAt(state.Clock) != Season.Winter) return;
            long year = (state.Clock.Day - 1) / VillageCalendar.DaysPerYear;
            if (state.WolfBounty.WinterYear != year)
                RollWinter(state, year);
            foreach (WolfBountyConfiguration configuration in _configurations)
                PayDueBounties(state, configuration);
        }

        private static void RollWinter(WorldState state, long year)
        {
            // 2-4 wolf incidents per winter (ECONOMY.md), on distinct random days.
            int incidents = 2 + state.Rng.NextInt(3);
            long winterStart = year * VillageCalendar.DaysPerYear + VillageCalendar.DaysPerSeason + 1;
            var days = new HashSet<long>();
            while (days.Count < incidents)
                days.Add(winterStart + state.Rng.NextInt(VillageCalendar.DaysPerSeason));
            var ordered = new List<long>(days);
            ordered.Sort();
            state.WolfBounty.WinterYear = year;
            state.WolfBounty.BountyDays = ordered.AsReadOnly();
        }

        private static void PayDueBounties(WorldState state, WolfBountyConfiguration configuration)
        {
            var remaining = new List<long>(state.WolfBounty.BountyDays);
            bool changed = false;
            foreach (long day in state.WolfBounty.BountyDays)
            {
                if (day > state.Clock.Day) break;
                state.Events.Append(state.Clock, configuration.Location, WorldEventType.Purchase,
                    ActorId.ForNpc(configuration.Hunter), visibility: configuration.Visibility,
                    itemType: Pelt, quantity: 1,
                    copper: configuration.BountyPerPeltCopper);
                // The crown pays: new money enters the village through the bounty gate.
                configuration.HunterWallet.Credit(configuration.BountyPerPeltCopper);
                remaining.Remove(day);
                changed = true;
            }
            if (changed)
                state.WolfBounty.BountyDays = remaining.AsReadOnly();
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WolfBountyConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Bounty configuration IDs must be unique.", "configurations");
        }
    }
}
