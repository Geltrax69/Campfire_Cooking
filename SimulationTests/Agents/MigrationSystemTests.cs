using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Proves village growth and decline emerge from conditions, never from upgrade
    /// buttons (P5-02, TOWN.md section 2). In-migration needs good stats; ambitious
    /// youth may leave in spring; the decline spiral runs through the stat formulas.
    /// </summary>
    public sealed class MigrationSystemTests
    {
        private static readonly LocationId HomeA = new LocationId("loc_home_a");
        private static readonly LocationId Square = new LocationId("loc_square");

        private static ItemCatalog Catalog() => new ItemCatalog(new[]
        {
            new ItemDefinition(new ItemTypeId("item_apple"), "Apple", "food", 3, 1, hungerEffect: -15),
        });

        private static NpcState Npc(string id, int age, string occupation,
            Dictionary<string, int> traits, int hunger = 20, int social = 60)
        {
            var npcId = new NpcId(id);
            var definition = new NpcDefinition(npcId, id, age, "unspecified",
                occupation, HomeA, HomeA, 0, traits,
                new NeedRates(6, 6, 6),
                new NpcSchedule(
                    Array.Empty<ScheduleEntry>(),
                    Array.Empty<ScheduleEntry>()));
            return new NpcState(definition, hunger, 80, social);
        }

        private static WorldState StateAtDay(long day) =>
            new WorldState(777, new GameTime((day - 1) * 1440));

        private static TownStats Stats(int happiness, int employment, int foodSupply, int housing) =>
            new TownStats(
                population: 120, wealthCopper: 10000, foodSupply: foodSupply, safety: 65,
                housing: housing, employment: employment, trade: 75, happiness: happiness,
                crime: 2, infrastructure: 57, reputation: 65);

        private static void StoreStats(WorldState state, TownStats stats)
        {
            long month = (state.Clock.Day - 1) / VillageCalendar.DaysPerMonth;
            state.TownStats.Store(month, stats);
        }

        private static MigrationSystem System() =>
            new MigrationSystem(new[] { new MigrationConfiguration("test") });

        [Test]
        public void HighStatsTriggerInMigration()
        {
            var state = StateAtDay(10); // Autumn.
            StoreStats(state, Stats(happiness: 70, employment: 85, foodSupply: 60, housing: 75));

            System().Tick(state);

            Assert.That(state.Migration.AdditionalBackgroundVillagers, Is.InRange(3, 5),
                "One household of 3-5 arrives when happiness>=65, employment>=80, foodSupply>=50, housing>=70.");
            Assert.That(state.Migration.AdditionalBackgroundHouseholds, Is.EqualTo(1));
            // Population stat now reflects the newcomers: 0 NPCs + 100 base + arrivals.
            int population = TownStatsCalculator.Compute(state, Catalog()).Population;
            Assert.That(population, Is.EqualTo(
                state.Npcs.Count + 100 + state.Migration.AdditionalBackgroundVillagers));
        }

        [Test]
        public void LowStatsPreventInMigration()
        {
            var state = StateAtDay(10); // Autumn.
            // Food supply below the 50 threshold blocks in-migration.
            StoreStats(state, Stats(happiness: 70, employment: 85, foodSupply: 30, housing: 75));

            System().Tick(state);

            Assert.That(state.Migration.AdditionalBackgroundVillagers, Is.EqualTo(0));
            Assert.That(state.Migration.AdditionalBackgroundHouseholds, Is.EqualTo(0));
        }

        [Test]
        public void InMigrationHappensAtMostOncePerSeason()
        {
            var state = StateAtDay(10); // Autumn.
            StoreStats(state, Stats(happiness: 70, employment: 85, foodSupply: 60, housing: 75));

            var system = System();
            system.Tick(state);
            int afterFirst = state.Migration.AdditionalBackgroundVillagers;
            Assert.That(afterFirst, Is.GreaterThan(0));

            // Same season, stats still good: no second household.
            system.Tick(state);
            Assert.That(state.Migration.AdditionalBackgroundVillagers, Is.EqualTo(afterFirst));

            // Next season (winter, day 91+): eligible again.
            var winter = StateAtDay(100);
            winter.Migration.RecordInMigration(afterFirst, 1, seasonIndex: 0); // arrived last autumn
            StoreStats(winter, Stats(happiness: 70, employment: 85, foodSupply: 60, housing: 75));
            system.Tick(winter); // season index 1 != 0, so the check runs again
            Assert.That(winter.Migration.AdditionalBackgroundVillagers, Is.GreaterThan(afterFirst));
        }

        [Test]
        public void InMigrationRecordsArrivalTruth()
        {
            var state = StateAtDay(10);
            StoreStats(state, Stats(happiness: 70, employment: 85, foodSupply: 60, housing: 75));

            System().Tick(state);

            var arrivals = state.Events.Query(type: WorldEventType.Arrival);
            Assert.That(arrivals.Count, Is.EqualTo(1));
            Assert.That(arrivals[0].Location, Is.EqualTo(Square));
        }

        [Test]
        public void AmbitiousYouthMayLeave()
        {
            // Piotr-like: 20 years old, ambitious 80, unemployed (poor prospects).
            // Spring starts day 181.
            bool anyLeft = false;
            for (int trial = 0; trial < 30 && !anyLeft; trial++)
            {
                var state = new WorldState(1000 + (ulong)trial, new GameTime((181 - 1) * 1440));
                var youth = Npc("npc_youth", 20, "unemployed",
                    new Dictionary<string, int> { { "ambitious", 80 } });
                state.Npcs.Register(youth);
                StoreStats(state, Stats(happiness: 60, employment: 50, foodSupply: 60, housing: 75));

                System().Tick(state);
                anyLeft = state.Npcs.Count == 0;
            }
            Assert.That(anyLeft, Is.True,
                "An ambitious unemployed 20-year-old should sometimes leave in spring (30% chance per spring).");
        }

        [Test]
        public void ContentYouthStays()
        {
            // Low ambition: never a candidate, regardless of RNG.
            var state = new WorldState(42, new GameTime((181 - 1) * 1440));
            var youth = Npc("npc_youth", 20, "unemployed",
                new Dictionary<string, int> { { "ambitious", 30 } });
            state.Npcs.Register(youth);
            StoreStats(state, Stats(happiness: 60, employment: 50, foodSupply: 60, housing: 75));

            System().Tick(state);

            Assert.That(state.Npcs.Count, Is.EqualTo(1), "Low ambition (30 < 70) never triggers out-migration.");
        }

        [Test]
        public void OutMigrationRecordsDepartureTruth()
        {
            // Find a seed where the youth leaves, then check the event.
            WorldState departed = null;
            for (int trial = 0; trial < 30 && departed == null; trial++)
            {
                var state = new WorldState(1000 + (ulong)trial, new GameTime((181 - 1) * 1440));
                var youth = Npc("npc_youth", 20, "unemployed",
                    new Dictionary<string, int> { { "ambitious", 80 } });
                state.Npcs.Register(youth);
                StoreStats(state, Stats(happiness: 60, employment: 50, foodSupply: 60, housing: 75));
                System().Tick(state);
                if (state.Npcs.Count == 0) departed = state;
            }
            Assert.That(departed, Is.Not.Null, "Need a seed where departure happens for the event check.");
            var departures = departed.Events.Query(type: WorldEventType.Departure);
            Assert.That(departures.Count, Is.EqualTo(1));
            Assert.That(departures[0].Actor, Is.EqualTo(ActorId.ForNpc(new NpcId("npc_youth"))));
        }

        [Test]
        public void DeclineSpiralEmerges()
        {
            // The spiral runs through the stat formulas, not through scripted rules:
            // food shortage -> hunger rises -> happiness falls -> out-migration conditions.
            // Here we prove the hunger->happiness link that carries the spiral.
            var fed = StateAtDay(100); // Winter.
            fed.Npcs.Register(Npc("npc_a", 30, "farmer",
                new Dictionary<string, int>(), hunger: 20, social: 60));
            fed.Npcs.Register(Npc("npc_b", 40, "baker",
                new Dictionary<string, int>(), hunger: 20, social: 60));
            int happyWhenFed = TownStatsCalculator.Compute(fed, Catalog()).Happiness;

            var starving = StateAtDay(100); // Winter.
            starving.Npcs.Register(Npc("npc_a", 30, "farmer",
                new Dictionary<string, int>(), hunger: 90, social: 60));
            starving.Npcs.Register(Npc("npc_b", 40, "baker",
                new Dictionary<string, int>(), hunger: 90, social: 60));
            int happyWhenStarving = TownStatsCalculator.Compute(starving, Catalog()).Happiness;

            Assert.That(happyWhenStarving, Is.LessThan(happyWhenFed),
                "High hunger (food shortage) must drag happiness down through the stat formula.");
        }

        [Test]
        public void MigrationStateRoundTrip()
        {
            var state = new MigrationState();
            state.RecordInMigration(4, 1, seasonIndex: 0);
            Assert.That(state.AdditionalBackgroundVillagers, Is.EqualTo(4));
            Assert.That(state.AdditionalBackgroundHouseholds, Is.EqualTo(1));
            Assert.That(state.AdditionalBackgroundSoundRoofs, Is.EqualTo(1));

            var snapshot = state.Capture();
            var restored = new MigrationState();
            restored.Restore(snapshot);
            Assert.That(restored.AdditionalBackgroundVillagers, Is.EqualTo(4));
            Assert.That(restored.AdditionalBackgroundHouseholds, Is.EqualTo(1));
            Assert.That(restored.LastInMigrationSeason, Is.EqualTo(state.LastInMigrationSeason));
        }
    }
}
