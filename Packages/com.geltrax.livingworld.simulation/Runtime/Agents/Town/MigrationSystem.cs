using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Immutable caller configuration for migration.</summary>
    public sealed class MigrationConfiguration
    {
        public MigrationConfiguration(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A configuration needs an ID.", nameof(id));
            Id = id;
        }

        public string Id { get; }
    }

    /// <summary>
    /// Village growth and decline through conditions, never upgrade buttons (P5-02,
    /// TOWN.md section 2). Each tick the system checks whether a seasonal boundary
    /// was crossed and runs the appropriate migration rule:
    ///
    /// In-migration (any season): when happiness &gt;= 65, employment &gt;= 80,
    /// foodSupply &gt;= 50 and housing &gt;= 70, one household of 3-5 background
    /// villagers arrives at the Market Square (an Arrival truth event). At most one
    /// household per season; newcomers are background villagers, so the 20
    /// simulated NPCs stay fixed per the prototype.
    ///
    /// Out-migration (spring): each NPC aged 15-25 with ambitious &gt;= 70 and
    /// (poor employment prospects or family tension) has a 30% seeded chance to
    /// leave for King's Rest. Departure removes them from the registry (a Departure
    /// truth event); the population stat falls because it counts registered NPCs.
    ///
    /// The decline spiral is emergent, not scripted: low foodSupply -&gt; hunger
    /// rises (NeedsSystem) -&gt; happiness falls (stat formula) -&gt; in-migration
    /// stops and out-migration conditions are met -&gt; population falls.
    ///
    /// Village-to-town thresholds (TOWN.md: population &gt;= 500 etc.) are documented
    /// here but deliberately NOT implemented: the design says they are unreachable
    /// in the prototype, which runs months, not the years 4x growth would need.
    /// </summary>
    public sealed class MigrationSystem : IWorldSystem
    {
        /// <summary>In-migration stat gates (TOWN.md section 2).</summary>
        public const int InMigrationHappinessGate = 65;
        public const int InMigrationEmploymentGate = 80;
        public const int InMigrationFoodSupplyGate = 50;
        public const int InMigrationHousingGate = 70;

        /// <summary>Out-migration: ambitious at or above this (0-100 trait scale).</summary>
        public const int AmbitiousThreshold = 70;
        /// <summary>Out-migration: working-age bounds for candidates.</summary>
        public const int MigrantAgeMin = 15;
        public const int MigrantAgeMax = 25;
        /// <summary>Out-migration: seeded chance (percent) per eligible youth per spring.</summary>
        public const int DepartureChancePercent = 30;
        /// <summary>Out-migration: town employment below this counts as poor prospects.</summary>
        public const int PoorProspectsEmploymentBelow = 70;
        /// <summary>Out-migration: trust below this with a housemate counts as family tension.</summary>
        public const int FamilyTensionTrustBelow = 30;

        /// <summary>New household size: 3-5 people (TOWN.md).</summary>
        public const int HouseholdMinPeople = 3;
        public const int HouseholdMaxPeople = 5;

        private static readonly LocationId Square = new LocationId("loc_square");

        private readonly List<MigrationConfiguration> _configurations;

        public MigrationSystem(IEnumerable<MigrationConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<MigrationConfiguration>();
            foreach (MigrationConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Migration configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MigrationConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Migration configuration IDs must be unique.", nameof(configurations));
        }

        public string Id => "agents.migration";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_configurations.Count == 0) return;

            long seasonIndex = (state.Clock.Day - 1) / VillageCalendar.DaysPerSeason;
            long yearIndex = (state.Clock.Day - 1) / VillageCalendar.DaysPerYear;
            Season season = VillageCalendar.SeasonAt(state.Clock);

            // In-migration is checked once per season, on any season.
            if (state.Migration.LastInMigrationSeason != seasonIndex)
                CheckInMigration(state, seasonIndex);

            // Out-migration is checked once per spring.
            if (season == Season.Spring && state.Migration.LastOutMigrationYear != yearIndex)
            {
                CheckOutMigration(state);
                state.Migration.RecordOutMigrationCheck(yearIndex);
            }
        }

        private static void CheckInMigration(WorldState state, long seasonIndex)
        {
            // Mark the season checked even when the stats are missing or the gates
            // fail: a missed season does not retry every tick.
            state.Migration.NoteSeasonChecked(seasonIndex);
            TownStats stats = state.TownStats.Values;
            if (stats == null) return;
            if (stats.Happiness < InMigrationHappinessGate) return;
            if (stats.Employment < InMigrationEmploymentGate) return;
            if (stats.FoodSupply < InMigrationFoodSupplyGate) return;
            if (stats.Housing < InMigrationHousingGate) return;

            int villagers = HouseholdMinPeople + state.Rng.NextInt(HouseholdMaxPeople - HouseholdMinPeople + 1);
            state.Migration.AddHousehold(villagers, seasonIndex);
            state.Events.Append(state.Clock, Square, WorldEventType.Arrival,
                visibility: EventVisibility.Normal, quantity: villagers);
        }

        private static void CheckOutMigration(WorldState state)
        {
            // Snapshot the candidates first: departures mutate the registry.
            var candidates = new List<NpcState>();
            foreach (NpcState npc in state.Npcs.Npcs)
            {
                int age = npc.Definition.Age;
                if (age < MigrantAgeMin || age > MigrantAgeMax) continue;
                if (TraitOrDefault(npc, "ambitious") < AmbitiousThreshold) continue;
                if (!HasPoorProspects(state, npc) && !HasFamilyTension(state, npc)) continue;
                candidates.Add(npc);
            }
            // Registry iterates in ordinal ID order already; departures are processed
            // in that order for determinism.
            foreach (NpcState npc in candidates)
            {
                if (state.Rng.NextInt(100) >= DepartureChancePercent) continue;
                NpcId id = npc.Definition.Id;
                LocationId home = npc.Definition.Home;
                state.Npcs.Unregister(id);
                state.Events.Append(state.Clock, home, WorldEventType.Departure,
                    actor: ActorId.ForNpc(id), visibility: EventVisibility.Normal);
            }
        }

        private static bool HasPoorProspects(WorldState state, NpcState npc)
        {
            // An NPC with no productive occupation has poor prospects regardless of
            // the town average; otherwise the town employment stat decides.
            if (IsNonProductive(npc.Definition.Occupation)) return true;
            TownStats stats = state.TownStats.Values;
            return stats != null && stats.Employment < PoorProspectsEmploymentBelow;
        }

        private static bool HasFamilyTension(WorldState state, NpcState npc)
        {
            // Family tension proxy: low trust with someone sharing the NPC's home.
            // The relationship registry is the village's memory of how people treat
            // each other; a cold hearth is a reason to leave.
            foreach (NpcState other in state.Npcs.Npcs)
            {
                if (other.Definition.Id == npc.Definition.Id) continue;
                if (other.Definition.Home != npc.Definition.Home) continue;
                if (state.Knowledge.Relationships.Trust(npc.Definition.Id, other.Definition.Id) < FamilyTensionTrustBelow)
                    return true;
            }
            return false;
        }

        private static bool IsNonProductive(string occupation) =>
            string.Equals(occupation, "child", StringComparison.OrdinalIgnoreCase)
            || string.Equals(occupation, "retired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(occupation, "none", StringComparison.OrdinalIgnoreCase)
            || string.Equals(occupation, "unemployed", StringComparison.OrdinalIgnoreCase);

        private static int TraitOrDefault(NpcState npc, string trait)
        {
            if (npc.Definition.Traits.TryGetValue(trait, out int value)) return value;
            return 0;
        }
    }
}
