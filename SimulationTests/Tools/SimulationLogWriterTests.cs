using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Tools
{
    /// <summary>Checks deterministic, factual rendering of the world event log.</summary>
    public sealed class SimulationLogWriterTests
    {
        private static readonly LocationId Shop = new LocationId("loc_shop");
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Tom = new NpcId("npc_tom");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        [Test]
        public void FormatsEveryEventTypeAndCountsActualKindsAndDays()
        {
            var log = new EventLog();
            log.Append(new GameTime(10), Shop, WorldEventType.Purchase, ActorId.ForNpc(Tom),
                new[] { ActorId.ForNpc(Mira) }, itemType: Apple, quantity: 5, copper: 15);
            log.Append(new GameTime(20), Shop, WorldEventType.PartialPurchase, ActorId.ForNpc(Tom),
                itemType: Apple, quantity: 2, copper: 6);
            log.Append(new GameTime(30), Shop, WorldEventType.FailedPurchase, ActorId.ForNpc(Tom), itemType: Apple);
            log.Append(new GameTime(840), Shop, WorldEventType.Theft, ActorId.Player,
                visibility: EventVisibility.Quiet, itemType: Apple, quantity: 6);
            log.Append(new GameTime(900), Shop, WorldEventType.StockCounted, ActorId.ForNpc(Mira),
                itemType: Apple, quantity: 14);
            log.Append(new GameTime(1100), Shop, WorldEventType.Conversation, ActorId.ForNpc(Mira),
                new[] { ActorId.ForNpc(Tom) });
            log.Append(new GameTime(1439), Farm, WorldEventType.Departure, ActorId.ForNpc(Tom));
            log.Append(new GameTime(1441), Shop, WorldEventType.Arrival, ActorId.ForNpc(Tom));

            string report = Render(log.Query());

            StringAssert.StartsWith("WORLD TRUTH REPORT — facts below do not imply NPC knowledge\n", report);
            StringAssert.Contains("Summary: 8 events across Day 1–Day 2; Purchase 1, PartialPurchase 1, FailedPurchase 1, Theft 1, StockCounted 1, Conversation 1, Departure 1, Arrival 1.\n", report);
            StringAssert.Contains("Day 1 00:10 — Purchase | actor: npc_tom | location: loc_shop | targets: npc_mira | item: item_apple | quantity: 5 | copper: 15 | visibility: Normal\n", report);
            StringAssert.Contains("Day 1 14:00 — Theft | actor: Player | location: loc_shop | item: item_apple | quantity: 6 | visibility: Quiet\n", report);
            StringAssert.Contains("Day 2 00:01 — Arrival | actor: npc_tom | location: loc_shop | visibility: Normal\n", report);
        }

        [Test]
        public void OmitsAbsentOptionalFieldsAndDoesNotInventFacts()
        {
            var log = new EventLog();
            log.Append(new GameTime(61), Shop, WorldEventType.FailedPurchase);
            string eventLine = Render(log.Query()).Split('\n').Single(line => line.StartsWith("Day ", StringComparison.Ordinal));
            Assert.That(eventLine, Is.EqualTo("Day 1 01:01 — FailedPurchase | location: loc_shop | visibility: Normal"));
            StringAssert.DoesNotContain("actor:", eventLine);
            StringAssert.DoesNotContain("witness", eventLine.ToLowerInvariant());
            StringAssert.DoesNotContain("wanted", eventLine.ToLowerInvariant());
        }

        [Test]
        public void UsesSafeDisplayNamesAndCanonicalFallbacks()
        {
            var log = new EventLog();
            log.Append(default, Shop, WorldEventType.Purchase, ActorId.ForNpc(Tom),
                new[] { ActorId.ForNpc(Mira), ActorId.Player }, itemType: Apple);
            var npcs = new Dictionary<NpcId, string> { [Tom] = "Tom\r\nThe Buyer" };
            var locations = new Dictionary<LocationId, string> { [Shop] = "Apple\t Stall" };
            var items = new Dictionary<ItemTypeId, string> { [Apple] = "Apple\u0007" };

            string report = Render(log.Query(), npcs, locations, items);

            StringAssert.Contains("actor: Tom The Buyer", report);
            StringAssert.Contains("location: Apple Stall", report);
            StringAssert.Contains("targets: npc_mira, Player", report);
            StringAssert.Contains("item: Apple", report);
            Assert.That(report.Count(character => character == '\n'), Is.EqualTo(4));
        }

        [Test]
        public void SortsSnapshotWithoutMutatingInputAndIgnoresCurrentCulture()
        {
            var log = new EventLog();
            WorldEvent first = log.Append(new GameTime(5), Shop, WorldEventType.Departure, ActorId.ForNpc(Tom));
            WorldEvent second = log.Append(new GameTime(65), Farm, WorldEventType.Arrival, ActorId.ForNpc(Tom), copper: 1234);
            var input = new List<WorldEvent> { second, first };
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ar-SA");
                string report = Render(input);
                Assert.That(report.IndexOf("Day 1 00:05", StringComparison.Ordinal),
                    Is.LessThan(report.IndexOf("Day 1 01:05", StringComparison.Ordinal)));
                StringAssert.Contains("copper: 1234", report);
            }
            finally { Thread.CurrentThread.CurrentCulture = original; }
            Assert.That(input, Is.EqualTo(new[] { second, first }));
        }

        [Test]
        public void EmptyReportAndInvalidArgumentsAreExplicit()
        {
            Assert.That(Render(Array.Empty<WorldEvent>()), Is.EqualTo(
                "WORLD TRUTH REPORT — facts below do not imply NPC knowledge\nSummary: 0 events; no game days covered.\n\n"));
            Assert.Throws<ArgumentNullException>(() => SimulationLogWriter.Write(null, Array.Empty<WorldEvent>()));
            Assert.Throws<ArgumentNullException>(() => SimulationLogWriter.Write(new StringWriter(), null));
            Assert.Throws<ArgumentException>(() => Render(new WorldEvent[] { null }));
            Assert.Throws<ArgumentException>(() => Render(Array.Empty<WorldEvent>(),
                new Dictionary<NpcId, string> { [default] = "Nobody" }));
            Assert.Throws<ArgumentException>(() => Render(Array.Empty<WorldEvent>(),
                new Dictionary<NpcId, string> { [Tom] = " \r\n" }));
            Assert.Throws<ArgumentException>(() => Render(Array.Empty<WorldEvent>(),
                new Dictionary<NpcId, string> { [Tom] = "\u0007" }));
        }

        [Test]
        public void FoundationTravelDemoPrintsWorldTruthReport()
        {
            var start = new LocationDefinition(Farm, "Hill Farm", "farm", 0, 0);
            var end = new LocationDefinition(Shop, "Apple Stall", "shop", 1, 0);
            var state = new WorldState(42, new GameTime(839));
            state.InitializeTravel(new LocationMap(new[] { start, end }, new[] { new TravelLink(Farm, Shop, 2) }));
            state.Travel.RegisterNpc(Tom, Farm);
            var world = new World(state);
            world.RegisterSystem(new CommandSystem());
            world.RegisterSystem(new TravelSystem());
            state.EnqueueCommand(new TravelCommand(Tom, Shop));
            world.Tick();
            world.Tick();
            world.Tick();

            string report = Render(state.Events.Query(),
                new Dictionary<NpcId, string> { [Tom] = "Tom" },
                new Dictionary<LocationId, string> { [Farm] = "Hill Farm", [Shop] = "Apple Stall" });
            TestContext.Progress.WriteLine("FOUNDATION DEMO — queued travel only; not the Apple Test or an autonomous village.");
            TestContext.Progress.Write(report);

            StringAssert.Contains("Day 1 14:00 — Departure | actor: Tom | location: Hill Farm", report);
            StringAssert.Contains("Day 1 14:02 — Arrival | actor: Tom | location: Apple Stall", report);
            Assert.That(state.Events.Query().Select(entry => entry.Type),
                Is.EqualTo(new[] { WorldEventType.Departure, WorldEventType.Arrival }));
        }

        private static string Render(IEnumerable<WorldEvent> events,
            IReadOnlyDictionary<NpcId, string> npcs = null,
            IReadOnlyDictionary<LocationId, string> locations = null,
            IReadOnlyDictionary<ItemTypeId, string> items = null)
        {
            var writer = new StringWriter(CultureInfo.InvariantCulture);
            SimulationLogWriter.Write(writer, events, npcs, locations, items);
            return writer.ToString();
        }

        private sealed class TravelCommand : IWorldCommand
        {
            private readonly NpcId _npc;
            private readonly LocationId _destination;
            public TravelCommand(NpcId npc, LocationId destination) { _npc = npc; _destination = destination; }
            public void Execute(WorldState state) => state.StartTravel(_npc, _destination);
        }
    }
}
