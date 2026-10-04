using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Proves the 11 town stats are computed from world truth (never set by hand) and
    /// recomputed on month boundaries only. P5-01.
    /// </summary>
    public sealed class TownStatsTests
    {
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly ItemTypeId Bread = new ItemTypeId("item_bread");
        private static readonly ItemTypeId Grain = new ItemTypeId("item_grain");
        private static readonly ItemTypeId Flour = new ItemTypeId("item_flour");

        private static readonly LocationId HomeA = new LocationId("loc_home_a");
        private static readonly LocationId HomeB = new LocationId("loc_home_b");
        private static readonly LocationId HomeC = new LocationId("loc_home_c");
        private static readonly LocationId ShopLoc = new LocationId("loc_shop");
        private static readonly LocationId Farm = new LocationId("loc_farm");

        private static ItemCatalog Catalog() => new ItemCatalog(new[]
        {
            new ItemDefinition(Apple, "Apple", "food", 3, 1, hungerEffect: -15),
            new ItemDefinition(Bread, "Bread", "food", 3, 1, hungerEffect: -30),
            new ItemDefinition(Grain, "Grain", "material", 8, 1),
            new ItemDefinition(Flour, "Flour", "material", 10, 1),
        });

        private static NpcState Npc(string id, int age, string occupation,
            LocationId home, int hunger, int energy, int social)
        {
            var npcId = new NpcId(id);
            var definition = new NpcDefinition(npcId, id, age, "unspecified",
                occupation, home, home, 0, new Dictionary<string, int>(),
                new NeedRates(0, 0, 0),
                new NpcSchedule(
                    Array.Empty<ScheduleEntry>(),
                    Array.Empty<ScheduleEntry>()));
            return new NpcState(definition, hunger, energy, social);
        }

        private static WorldState StateAtDay(long day) =>
            new WorldState(12345, new GameTime((day - 1) * 1440));

        private static void RegisterBelongings(WorldState state, ItemCatalog catalog,
            NpcId npc, int copper, ItemTypeId item, int quantity)
        {
            var inventory = new Inventory(catalog);
            if (quantity > 0) inventory.Add(item, quantity);
            state.Belongings.Register(ActorId.ForNpc(npc), inventory, new Wallet(copper));
        }

        /// <summary>
        /// The shared scenario: 4 NPCs, 1 shop, village fund, granary, roofs, 2 recent
        /// thefts and 1 recent livestock predation, 600 traveler + 400 merchant copper.
        /// Every expected value below is hand-computed from the P5-01 formulas.
        /// </summary>
        private sealed class Scenario
        {
            public Scenario()
            {
                Catalog = TownStatsTests.Catalog();
                State = StateAtDay(5);

                NpcA = Npc("npc_a", 30, "farmer", HomeA, hunger: 20, energy: 80, social: 60);
                NpcB = Npc("npc_b", 40, "baker", HomeB, hunger: 40, energy: 80, social: 80);
                NpcC = Npc("npc_c", 10, "child", HomeA, hunger: 50, energy: 80, social: 50);
                NpcD = Npc("npc_d", 70, "retired", HomeC, hunger: 30, energy: 80, social: 70);
                State.Npcs.Register(NpcA);
                State.Npcs.Register(NpcB);
                State.Npcs.Register(NpcC);
                State.Npcs.Register(NpcD);

                RegisterBelongings(State, Catalog, NpcA.Definition.Id, 100, Apple, 10);
                RegisterBelongings(State, Catalog, NpcB.Definition.Id, 200, Bread, 5);
                RegisterBelongings(State, Catalog, NpcC.Definition.Id, 0, Apple, 0);
                RegisterBelongings(State, Catalog, NpcD.Definition.Id, 50, Apple, 2);

                var shopStock = new Inventory(Catalog);
                shopStock.Add(Apple, 20);
                State.Shops.Register(new Shop(ShopLoc, new NpcId("npc_shopkeeper"), shopStock,
                    new Wallet(500), new[] { new KeyValuePair<ItemTypeId, int>(Apple, 3) }));

                State.RestoreVillageFund(new VillageFundState(initialized: true, startingCopper: 1000));
                State.RestoreGranary(new GranaryState(sacks: 50));

                var housing = new HousingState();
                housing.SetRoofCondition(HomeA, 90);
                housing.SetRoofCondition(HomeB, 80);
                housing.SetRoofCondition(HomeC, 30);
                State.RestoreHousing(housing);

                // Two recent thefts (crime + unresolved crimes) and one livestock predation
                // (wolf incident this winter: winter starts at day 91-360 = -269).
                // Appends must be chronological: the event log rejects backwards time.
                State.Events.Append(new GameTime((2 - 1) * 1440), ShopLoc, WorldEventType.Theft,
                    ActorId.ForNpc(NpcA.Definition.Id), copper: 30);
                State.Events.Append(new GameTime((3 - 1) * 1440), Farm, WorldEventType.Predation,
                    quantity: 1, copper: 100);
                State.Events.Append(new GameTime((4 - 1) * 1440), ShopLoc, WorldEventType.Theft,
                    ActorId.ForNpc(NpcA.Definition.Id), copper: 12);

                State.RestoreTravelerSpend(new TravelerSpendState(initialized: true,
                    lastPayoutDay: 5, monthIndex: 0, paidThisMonth: new[] { 600 }));
                var merchant = new MerchantScheduleState(initialized: true, nextVisitDay: 100);
                merchant.RecordMerchantTrade(400, VillageCalendar.MonthIndex(State.Clock));
                State.RestoreMerchantSchedule(merchant);
            }

            public ItemCatalog Catalog { get; }
            public WorldState State { get; }
            public NpcState NpcA { get; }
            public NpcState NpcB { get; }
            public NpcState NpcC { get; }
            public NpcState NpcD { get; }
        }

        [Test]
        public void StatsComputeFromWorldState()
        {
            var scenario = new Scenario();
            TownStats stats =
                TownStatsCalculator.Compute(scenario.State, scenario.Catalog);

            // Population: 4 simulated + 100 background.
            Assert.That(stats.Population, Is.EqualTo(104));
            // Wealth: NPC money (100+200+0+50) + fund 1000 + granary 50x8 + shop stock 20x3.
            // The shop's separate 500-copper till is working cash, not NPC money: excluded.
            Assert.That(stats.WealthCopper, Is.EqualTo(1810));
            // Food supply: granary 50 sacks x 5520 points + household food 330 points =
            // 276330 points / (100 x 104 people) = 26.57 days -> 26.57/180 x 100 = 15.
            Assert.That(stats.FoodSupply, Is.EqualTo(15));
            // Safety: 100 - 1x15 (wolf) - 2x5 (unresolved crimes) + 0 (no night watch) + 8 (palisade 80).
            Assert.That(stats.Safety, Is.EqualTo(83));
            // Housing: 2 sound roofs of 3 homes -> 67.
            Assert.That(stats.Housing, Is.EqualTo(67));
            // Employment: npc_a and npc_b are working age with productive work; the child
            // and the retiree are excluded from the denominator -> 2/2.
            Assert.That(stats.Employment, Is.EqualTo(100));
            // Trade: (600 traveler + 400 merchant) / 1700 x 100 -> 59.
            Assert.That(stats.Trade, Is.EqualTo(59));
            // Happiness: avg of (0.4 x social + 0.3 x (100-hunger) + 0 - 0.1 x 3 fear events):
            // (47.7 + 49.7 + 34.7 + 48.7) / 4 = 45.2 -> 45.
            Assert.That(stats.Happiness, Is.EqualTo(45));
            // Crime: 2 thefts in the last 30 days.
            Assert.That(stats.Crime, Is.EqualTo(2));
            // Infrastructure: 30x0.4 + 80x0.25 + 70x0.2 + 75x0.15 = 57.25 -> 57.
            Assert.That(stats.Infrastructure, Is.EqualTo(57));
            // Reputation: (70 + 60 + 65) / 3.
            Assert.That(stats.Reputation, Is.EqualTo(65));
        }

        [Test]
        public void SafetyReflectsWolfIncidents()
        {
            var scenario = new Scenario();
            // Clear the scenario's incidents: rebuild the log without them.
            scenario.State.Events.PruneBefore(new GameTime((5 - 1) * 1440), Array.Empty<WorldEventId>());

            TownStats calm =
                TownStatsCalculator.Compute(scenario.State, scenario.Catalog);
            // No incidents, no crimes, palisade 80: 100 + 8, clamped to 100.
            Assert.That(calm.Safety, Is.EqualTo(100));

            scenario.State.Events.Append(scenario.State.Clock, Farm, WorldEventType.Predation,
                quantity: 1, copper: 100);
            scenario.State.Events.Append(scenario.State.Clock, Farm, WorldEventType.Predation,
                quantity: 1, copper: 250);
            TownStats attacked =
                TownStatsCalculator.Compute(scenario.State, scenario.Catalog);
            Assert.That(attacked.Safety, Is.EqualTo(100 - 2 * 15 + 8));

            // A wild kill (no livestock value) is not a wolf incident against the village.
            scenario.State.Events.Append(scenario.State.Clock, Farm, WorldEventType.Predation,
                quantity: 1);
            TownStats wildKill =
                TownStatsCalculator.Compute(scenario.State, scenario.Catalog);
            Assert.That(wildKill.Safety, Is.EqualTo(attacked.Safety));
        }

        [Test]
        public void SafetyClampsAtZero()
        {
            var scenario = new Scenario();
            for (int i = 0; i < 20; i++)
                scenario.State.Events.Append(scenario.State.Clock, Farm, WorldEventType.Predation,
                    quantity: 1, copper: 100);
            TownStats stats =
                TownStatsCalculator.Compute(scenario.State, scenario.Catalog);
            Assert.That(stats.Safety, Is.EqualTo(0));
        }

        [Test]
        public void CrimeCountsRecentEvents()
        {
            // Clock at day 50: the window is [day 20, day 50]. The day-20 theft is
            // exactly on the boundary (inclusive); the day-19 theft is outside it.
            WorldState state = StateAtDay(50);
            // Appends must be chronological.
            state.Events.Append(new GameTime((19 - 1) * 1440), ShopLoc, WorldEventType.Theft, copper: 5);
            state.Events.Append(new GameTime((20 - 1) * 1440), ShopLoc, WorldEventType.Theft, copper: 5);

            TownStats stats =
                TownStatsCalculator.Compute(state, Catalog());
            Assert.That(stats.Crime, Is.EqualTo(1));

            // Thirty days later both thefts have aged out of the window.
            WorldState later = StateAtDay(80);
            later.Events.Append(new GameTime((19 - 1) * 1440), ShopLoc, WorldEventType.Theft, copper: 5);
            later.Events.Append(new GameTime((20 - 1) * 1440), ShopLoc, WorldEventType.Theft, copper: 5);
            TownStats laterStats =
                TownStatsCalculator.Compute(later, Catalog());
            Assert.That(laterStats.Crime, Is.EqualTo(0));
        }

        [Test]
        public void StatsAreMonthly()
        {
            var scenario = new Scenario();
            var system = new TownStatsSystem(scenario.Catalog);
            var world = new World(scenario.State);
            world.RegisterSystem(system);

            // First tick computes the stats for month 0 (the scenario starts on day 5).
            world.Tick();
            TownStats day5 = scenario.State.TownStats.Values;
            Assert.That(scenario.State.TownStats.IsComputed, Is.True);
            Assert.That(scenario.State.TownStats.ComputedMonth, Is.EqualTo(0));

            // Tick to the start of day 30: still month 0, stats unchanged.
            for (int i = 0; i < 25 * 1440 - 1; i++) world.Tick();
            Assert.That(scenario.State.Clock.Day, Is.EqualTo(30));
            Assert.That(scenario.State.TownStats.ComputedMonth, Is.EqualTo(0));
            AssertSameStats(day5, scenario.State.TownStats.Values);

            // A theft on day 30 does not move the stats mid-month.
            scenario.State.Events.Append(scenario.State.Clock, ShopLoc, WorldEventType.Theft, copper: 7);
            world.Tick();
            Assert.That(scenario.State.TownStats.ComputedMonth, Is.EqualTo(0));
            Assert.That(scenario.State.TownStats.Values.Crime, Is.EqualTo(day5.Crime));

            // The first tick of day 31 crosses into month 1 and recomputes.
            for (int i = 0; i < 1439; i++) world.Tick();
            Assert.That(scenario.State.Clock.Day, Is.EqualTo(31));
            Assert.That(scenario.State.TownStats.ComputedMonth, Is.EqualTo(1));
            Assert.That(scenario.State.TownStats.Values.Crime, Is.EqualTo(day5.Crime + 1));
        }

        private static void AssertSameStats(TownStats expected,
            TownStats actual)
        {
            Assert.That(actual.Population, Is.EqualTo(expected.Population));
            Assert.That(actual.WealthCopper, Is.EqualTo(expected.WealthCopper));
            Assert.That(actual.FoodSupply, Is.EqualTo(expected.FoodSupply));
            Assert.That(actual.Safety, Is.EqualTo(expected.Safety));
            Assert.That(actual.Housing, Is.EqualTo(expected.Housing));
            Assert.That(actual.Employment, Is.EqualTo(expected.Employment));
            Assert.That(actual.Trade, Is.EqualTo(expected.Trade));
            Assert.That(actual.Happiness, Is.EqualTo(expected.Happiness));
            Assert.That(actual.Crime, Is.EqualTo(expected.Crime));
            Assert.That(actual.Infrastructure, Is.EqualTo(expected.Infrastructure));
            Assert.That(actual.Reputation, Is.EqualTo(expected.Reputation));
        }

        [Test]
        public void StartingValuesApproximateDesign()
        {
            // A world shaped like the TOWN.md starting position, clocked just after the
            // Harvest Feast (day ~89) so the feast counts as recent. Each stat is checked
            // against the design's starting value; documented discrepancies are explained.
            ItemCatalog catalog = Catalog();
            WorldState state = StateAtDay(95);
            string[] homes = { "loc_home_1", "loc_home_2", "loc_home_3", "loc_home_4",
                "loc_home_5", "loc_home_6", "loc_home_7", "loc_home_8", "loc_home_9",
                "loc_home_10", "loc_home_11", "loc_home_12" };
            string[] occupations = { "farmer", "farmer", "miller", "baker", "blacksmith",
                "tavern keeper", "guard", "healer", "woodcutter", "weaver", "fisherman",
                "general-store owner", "hunter", "farmhand", "village elder", "child",
                "child", "child", "farmer's wife", "baker's wife" };
            int[] ages = { 45, 42, 38, 44, 48, 52, 39, 60, 36, 34, 50, 41, 55, 16, 71, 8, 10, 12, 40, 44 };
            for (int i = 0; i < 20; i++)
            {
                var npc = Npc("npc_start_" + i, ages[i], occupations[i],
                    new LocationId(homes[i % homes.Length]), hunger: 30, energy: 80, social: 70);
                state.Npcs.Register(npc);
                var inventory = new Inventory(catalog);
                inventory.Add(Bread, 800); // 20 x 800 x 30 hunger points = ~40 days of household stores
                state.Belongings.Register(ActorId.ForNpc(npc.Definition.Id), inventory, new Wallet(480));
            }
            state.RestoreVillageFund(new VillageFundState(initialized: true, startingCopper: 1000));
            state.RestoreGranary(new GranaryState(sacks: 50));
            // 30 households (WORLD.md): 12 simulated homes with sound roofs, 18 background
            // households of which 14 are sound -> (12 + 14) / 30 = 87 vs design 85.
            state.RestoreHousing(new HousingState(backgroundHouseholds: 18, backgroundSoundRoofs: 14));
            // Tom's windfalls: 2 recent thefts. Two wolf incidents this winter (days 92, 94).
            state.Events.Append(new GameTime((80 - 1) * 1440), ShopLoc, WorldEventType.Theft, copper: 20);
            state.Events.Append(new GameTime((90 - 1) * 1440), ShopLoc, WorldEventType.Theft, copper: 8);
            state.Events.Append(new GameTime((92 - 1) * 1440), Farm, WorldEventType.Predation,
                quantity: 1, copper: 120);
            state.Events.Append(new GameTime((94 - 1) * 1440), Farm, WorldEventType.Predation,
                quantity: 1, copper: 90);
            state.RestoreTravelerSpend(new TravelerSpendState(initialized: true,
                lastPayoutDay: 95, monthIndex: 3, paidThisMonth: new[] { 900 }));
            var merchant = new MerchantScheduleState(initialized: true, nextVisitDay: 200);
            merchant.RecordMerchantTrade(375, VillageCalendar.MonthIndex(state.Clock));
            state.RestoreMerchantSchedule(merchant);

            TownStats stats =
                TownStatsCalculator.Compute(state, catalog);

            Assert.That(stats.Population, Is.EqualTo(120)); // design 120
            Assert.That(stats.WealthCopper, Is.EqualTo(11000)); // design ~11,000
            Assert.That(stats.FoodSupply, Is.EqualTo(35)); // design 35
            // Design says 65: the formula gives 100 - 30 - 10 + 0 + 8 = 68. The design's 65
            // is narrative; the palisade bonus (80 -> 8) and no night watch explain the gap.
            Assert.That(stats.Safety, Is.EqualTo(68));
            Assert.That(stats.Housing, Is.EqualTo(87)); // design 85
            // Design says 90, but the starting roster has no unemployed working-age NPC:
            // every simulated adult works, so the formula honestly yields 100.
            Assert.That(stats.Employment, Is.EqualTo(100));
            Assert.That(stats.Trade, Is.EqualTo(75)); // design 75
            // 0.4x70 + 0.3x70 + 0.2x100 (feast 6 days ago) - 0.1x4 fear events = 68.6 -> 69.
            Assert.That(stats.Happiness, Is.EqualTo(69)); // design 70
            Assert.That(stats.Crime, Is.EqualTo(2)); // design 2
            Assert.That(stats.Infrastructure, Is.EqualTo(57)); // design 57
            Assert.That(stats.Reputation, Is.EqualTo(65)); // design 65
        }
    }
}
