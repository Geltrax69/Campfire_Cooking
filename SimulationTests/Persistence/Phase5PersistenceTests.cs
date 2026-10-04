using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Persistence
{
    /// <summary>
    /// Proves every Phase 5 state section survives the version 5 save format:
    /// town stats, migration state and emergent events — plus version
    /// compatibility (v4 and older documents load with town defaults) and
    /// corruption rejection.
    /// </summary>
    public sealed class Phase5PersistenceTests
    {
        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }

        /// <summary>World with non-default values in every Phase 5 section.</summary>
        private static WorldState Phase5RichWorld(ContentBundle bundle)
        {
            var state = new WorldState(4242, new GameTime(9000));

            // Town stats: a computed month with distinctive values.
            var stats = new TownStats(
                population: 127, wealthCopper: 12345, foodSupply: 42, safety: 61,
                housing: 83, employment: 88, trade: 71, happiness: 66, crime: 3,
                infrastructure: 59, reputation: 68);
            state.RestoreTownStats(new TownStatsState(computedMonth: 11, values: stats));

            // Migration: one household arrived in season 3.
            var migration = new MigrationState();
            migration.RecordInMigration(villagers: 4, households: 1, seasonIndex: 3);
            state.RestoreMigration(migration);

            // Emergent events: merchant arrival active since day 200.
            var events = new EmergentEventState();
            events.Restore(new EmergentEventSnapshot(
                new List<KeyValuePair<string, long>>
                {
                    new KeyValuePair<string, long>("event_merchant_arrival", 200)
                },
                lastMerchantDay: 200));
            state.RestoreEmergentEvents(events);

            return state;
        }

        [Test]
        public void TownStateSaveLoadRoundTrip()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase5RichWorld(bundle);

            string json = WorldSaver.Save(state);
            WorldState loaded = WorldLoader.Load(json, root);

            // Town stats.
            Assert.That(loaded.TownStats.IsComputed, Is.True);
            Assert.That(loaded.TownStats.ComputedMonth, Is.EqualTo(11));
            TownStats values = loaded.TownStats.Values;
            Assert.That(values.Population, Is.EqualTo(127));
            Assert.That(values.WealthCopper, Is.EqualTo(12345));
            Assert.That(values.FoodSupply, Is.EqualTo(42));
            Assert.That(values.Safety, Is.EqualTo(61));
            Assert.That(values.Housing, Is.EqualTo(83));
            Assert.That(values.Employment, Is.EqualTo(88));
            Assert.That(values.Trade, Is.EqualTo(71));
            Assert.That(values.Happiness, Is.EqualTo(66));
            Assert.That(values.Crime, Is.EqualTo(3));
            Assert.That(values.Infrastructure, Is.EqualTo(59));
            Assert.That(values.Reputation, Is.EqualTo(68));

            // Migration.
            Assert.That(loaded.Migration.AdditionalBackgroundVillagers, Is.EqualTo(4));
            Assert.That(loaded.Migration.AdditionalBackgroundHouseholds, Is.EqualTo(1));
            Assert.That(loaded.Migration.AdditionalBackgroundSoundRoofs, Is.EqualTo(1));
            Assert.That(loaded.Migration.LastInMigrationSeason, Is.EqualTo(3));
            Assert.That(loaded.Migration.LastOutMigrationYear, Is.EqualTo(-1));

            // Emergent events.
            Assert.That(loaded.EmergentEvents.IsActive(EmergentEventId.MerchantArrival), Is.True);
            Assert.That(loaded.EmergentEvents.StartedDay(EmergentEventId.MerchantArrival), Is.EqualTo(200));
            Assert.That(loaded.EmergentEvents.LastMerchantDay, Is.EqualTo(200));
        }

        [Test]
        public void TownSaveLoadSaveIsByteIdentical()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase5RichWorld(bundle);

            string first = WorldSaver.Save(state);
            WorldState loaded = WorldLoader.Load(first, root);
            string second = WorldSaver.Save(loaded);

            Assert.That(second, Is.EqualTo(first), "Save → load → save must be byte-identical.");
        }

        [Test]
        public void EmptyTownStateRoundTrips()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            var state = new WorldState(4242, new GameTime(9000));

            string json = WorldSaver.Save(state);
            WorldState loaded = WorldLoader.Load(json, root);

            Assert.That(loaded.TownStats.IsComputed, Is.False);
            Assert.That(loaded.TownStats.ComputedMonth, Is.EqualTo(-1));
            Assert.That(loaded.Migration.AdditionalBackgroundVillagers, Is.EqualTo(0));
            Assert.That(loaded.EmergentEvents.ActiveEvents, Is.Empty);
            Assert.That(loaded.EmergentEvents.LastMerchantDay, Is.EqualTo(-1));
        }

        [Test]
        public void TownSaveFormatVersion()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase5RichWorld(bundle);

            string v6 = WorldSaver.Save(state);
            Assert.That(v6, Does.Contain("\"formatVersion\": 6"));

            // A v4 document loads with town defaults.
            WorldState loadedV4 = WorldLoader.Load(StripToVersion4(v6), root);
            Assert.That(loadedV4.TownStats.IsComputed, Is.False,
                "A v4 document has no town stats: uncomputed.");
            Assert.That(loadedV4.Migration.AdditionalBackgroundVillagers, Is.EqualTo(0),
                "A v4 document has no migration: empty.");
            Assert.That(loadedV4.EmergentEvents.ActiveEvents, Is.Empty,
                "A v4 document has no emergent events.");
            Assert.That(loadedV4.EmergentEvents.LastMerchantDay, Is.EqualTo(-1));
        }

        [Test]
        public void FutureVersionIsRejected()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase5RichWorld(bundle);
            string json = WorldSaver.Save(state);
            string v7 = json.Replace("\"formatVersion\": 6", "\"formatVersion\": 7");

            Assert.Throws<LoadException>(() => WorldLoader.Load(v7, root));
        }

        [Test]
        public void CorruptTownStatsAreRejected()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            string json = WorldSaver.Save(Phase5RichWorld(bundle));

            // Food supply out of range.
            string bad = json.Replace("\"foodSupply\": 42", "\"foodSupply\": 142");
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root),
                "A food supply above 100 must be rejected.");
        }

        [Test]
        public void CorruptEmergentEventsAreRejected()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            string json = WorldSaver.Save(Phase5RichWorld(bundle));

            // Unknown event ID.
            string bad = json.Replace("event_merchant_arrival", "event_nonexistent");
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root),
                "An unknown event ID must be rejected.");
        }

        /// <summary>Rewrites a version 5 document as version 4 (drops Phase 5 sections).</summary>
        private static string StripToVersion4(string v5)
        {
            using (JsonDocument document = JsonDocument.Parse(v5))
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        foreach (JsonProperty property in document.RootElement.EnumerateObject())
                        {
                            switch (property.Name)
                            {
                                case "formatVersion":
                                    writer.WriteNumber("formatVersion", 4);
                                    break;
                                case "townStats":
                                case "migration":
                                case "emergentEvents":
                                    break;
                                default:
                                    property.WriteTo(writer);
                                    break;
                            }
                        }
                        writer.WriteEndObject();
                        writer.Flush();
                    }
                    return System.Text.Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }
    }
}
