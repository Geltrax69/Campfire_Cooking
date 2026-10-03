using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using NUnit.Framework;
using static LivingWorld.Simulation.Tests.Core.Locations.LocationMapTests;

namespace LivingWorld.Simulation.Tests.Core.Locations
{
    /// <summary>Checks clock-authoritative travel, endpoint presence and rejection without mutation.</summary>
    public sealed class TravelTests
    {
        private static readonly NpcId Npc = new NpcId("npc_test");
        private static WorldState State(long minute = 0)
        {
            var state = new WorldState(42, new GameTime(minute));
            state.InitializeTravel(TinyMap());
            state.Travel.RegisterNpc(Npc, A);
            return state;
        }

        [TestCase(0L)]
        [TestCase(1234L)]
        public void ArrivalOccursAfterFullDurationWithNoEndpointPresenceInTransit(long minute)
        {
            var state = State(minute);
            var world = new World(state);
            world.RegisterSystem(new TravelSystem());
            var npc = state.Travel[Npc];
            Assert.That(npc.CurrentLocation, Is.EqualTo(A));
            state.StartTravel(Npc, B);
            Assert.That(npc.CurrentLocation, Is.Null);
            Assert.That((npc.Journey.Origin, npc.Journey.Destination, npc.Journey.Arrival),
                Is.EqualTo((A, B, new GameTime(minute + 2))));
            AssertEvent(state.Events.Query().Single(), 1, minute, A, WorldEventType.Departure, Npc);
            world.Tick();
            Assert.That(npc.CurrentLocation, Is.Null);
            Assert.That(state.Events.Count, Is.EqualTo(1));
            world.Tick();
            Assert.That(npc.CurrentLocation, Is.EqualTo(B));
            Assert.That(npc.Journey, Is.Null);
            AssertEvent(state.Events.Query().Last(), 2, minute + 2, B, WorldEventType.Arrival, Npc);
            world.Tick();
            Assert.That(npc.CurrentLocation, Is.EqualTo(B));
            Assert.That(state.Events.Count, Is.EqualTo(2), "An arrival is logged once.");
            state.StartTravel(Npc, A);
            Assert.That(npc.Journey.Arrival.TotalMinutes, Is.EqualTo(minute + 5));
        }

        [TestCase(SimulationPhase.Commands, "start")]
        [TestCase(SimulationPhase.Decisions, "start")]
        [TestCase(SimulationPhase.Actions, "a.start")]
        [TestCase(SimulationPhase.Actions, "z.start")]
        public void TravelStartedInsideTickDoesNotGetAFreeMinute(SimulationPhase phase, string id)
        {
            var state = State(100);
            var world = new World(state);
            world.RegisterSystem(new TravelSystem());
            world.RegisterSystem(new StartSystem(phase, id));
            world.Tick();
            Assert.That(state.Travel[Npc].Journey.Arrival.TotalMinutes, Is.EqualTo(103));
            world.Tick();
            Assert.That(state.Travel[Npc].CurrentLocation, Is.Null);
            world.Tick();
            Assert.That(state.Travel[Npc].CurrentLocation, Is.EqualTo(B));
        }

        [Test]
        public void SystemRequiresExplicitRegistrationAndCompletesOverdueTravel()
        {
            var state = State();
            state.StartTravel(Npc, B);
            var world = new World(state);
            for (int i = 0; i < 3; i++) world.Tick();
            Assert.That(state.Travel[Npc].CurrentLocation, Is.Null);
            var system = new TravelSystem();
            Assert.That((system.Id, system.Phase), Is.EqualTo(("core.travel", SimulationPhase.Actions)));
            system.Tick(state);
            Assert.That(state.Travel[Npc].CurrentLocation, Is.EqualTo(B));
            AssertEvent(state.Events.Query().Last(), 2, 3, B, WorldEventType.Arrival, Npc);
        }

        [Test]
        public void InitializationRegistrationAndInvalidTravelDoNotMutateState()
        {
            var state = new WorldState(42);
            Assert.That(state.Travel, Is.Null);
            Assert.Throws<InvalidOperationException>(() => state.StartTravel(Npc, A));
            Assert.Throws<ArgumentNullException>(() => state.InitializeTravel(null));
            var map = TinyMap();
            state.InitializeTravel(map);
            Assert.Throws<InvalidOperationException>(() => state.InitializeTravel(TinyMap()));
            Assert.That(state.Travel.Map, Is.SameAs(map));
            Assert.Throws<ArgumentException>(() => state.Travel.RegisterNpc(default, A));
            Assert.Throws<ArgumentException>(() => state.Travel.RegisterNpc(Npc, default));
            Assert.Throws<ArgumentException>(() => state.Travel.RegisterNpc(Npc, new LocationId("unknown")));
            Assert.That(state.Travel.Npcs, Is.Empty);
            state.Travel.RegisterNpc(Npc, A);
            Assert.Throws<ArgumentException>(() => state.Travel.RegisterNpc(Npc, B));
            Assert.Throws<ArgumentException>(() => state.StartTravel(default, B));
            Assert.Throws<ArgumentException>(() => state.StartTravel(new NpcId("unknown"), B));
            Assert.Throws<ArgumentException>(() => state.StartTravel(Npc, default));
            Assert.Throws<ArgumentException>(() => state.StartTravel(Npc, new LocationId("unknown")));
            Assert.Throws<InvalidOperationException>(() => state.StartTravel(Npc, C));
            state.StartTravel(Npc, A);
            Assert.That(state.Travel[Npc].CurrentLocation, Is.EqualTo(A));
            Assert.That(state.Travel[Npc].Journey, Is.Null);
            Assert.That(state.Travel.Npcs.Count, Is.EqualTo(1));
            Assert.That(state.Events.Count, Is.Zero, "Registration, rejection and same-place travel are not moves.");
            state.StartTravel(Npc, B);
            var journey = state.Travel[Npc].Journey;
            Assert.Throws<InvalidOperationException>(() => state.StartTravel(Npc, A));
            Assert.Throws<InvalidOperationException>(() => state.StartTravel(Npc, B));
            Assert.That(state.Travel[Npc].Journey, Is.SameAs(journey));
            Assert.That(state.Travel[Npc].CurrentLocation, Is.Null);
            Assert.That(state.Clock.TotalMinutes, Is.Zero);
            Assert.That(state.Rng.State, Is.EqualTo(42UL));
            Assert.That(state.Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void ArrivalOverflowLeavesNpcAtOriginAndSamePlaceRemainsNoOp()
        {
            var state = State(long.MaxValue - 1);
            Assert.Throws<OverflowException>(() => state.StartTravel(Npc, B));
            state.StartTravel(Npc, A);
            Assert.That(state.Travel[Npc].CurrentLocation, Is.EqualTo(A));
            Assert.That(state.Travel[Npc].Journey, Is.Null);
            Assert.That(state.Clock.TotalMinutes, Is.EqualTo(long.MaxValue - 1));
            Assert.That(state.Events.Count, Is.Zero);
        }

        [Test]
        public void SeveralTravellersHaveStableOrdinalEnumerationAndIdenticalResults()
        {
            var traces = new[] { new List<string>(), new List<string>() };
            for (int run = 0; run < 2; run++)
            {
                var state = new WorldState(42);
                state.InitializeTravel(TinyMap());
                var ids = new[] { "ä", "a", "B", "A" };
                foreach (var id in run == 0 ? ids : ids.Reverse()) state.Travel.RegisterNpc(new NpcId(id), A);
                Assert.Throws<NotSupportedException>(() => ((IList<NpcTravelState>)state.Travel.Npcs).Clear());
                var world = new World(state);
                world.RegisterSystem(new TravelSystem());
                foreach (var npc in state.Travel.Npcs)
                    if (npc.Id.Value != "a") state.StartTravel(npc.Id, B);
                for (int tick = 0; tick < 4; tick++)
                {
                    if (tick == 1) state.StartTravel(new NpcId("a"), B);
                    world.Tick();
                    if (tick == 1)
                    {
                        Assert.That(state.Travel[new NpcId("a")].CurrentLocation, Is.Null);
                        Assert.That(state.Travel[new NpcId("A")].CurrentLocation, Is.EqualTo(B));
                    }
                    foreach (var npc in state.Travel.Npcs)
                        traces[run].Add($"{state.Clock.TotalMinutes}:{npc.Id}:{npc.CurrentLocation}:{npc.Journey?.Arrival.TotalMinutes}");
                }
                Assert.That(state.Travel.Npcs.Select(npc => npc.Id.Value), Is.EqualTo(new[] { "A", "B", "a", "ä" }));
                Assert.That(state.Travel.Npcs.All(npc => npc.CurrentLocation == B), Is.True);
                Assert.That(state.Events.Query(type: WorldEventType.Arrival).Select(entry => entry.Actor.Value.Npc.Value.Value),
                    Is.EqualTo(new[] { "A", "B", "ä", "a" }), "Simultaneous arrivals are ordinal; later journeys remain pending.");
            }
            Assert.That(traces[0], Is.EqualTo(traces[1]));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EventAppendFailureLeavesDepartureOrArrivalUnchanged(bool arriving)
        {
            var state = State(10);
            if (arriving) state.StartTravel(Npc, B);
            var npc = state.Travel[Npc];
            var location = npc.CurrentLocation;
            var journey = npc.Journey;
            state.Events.Append(new GameTime(20), A, WorldEventType.Conversation);
            var before = state.Events.Query();
            var world = new World(state);
            world.RegisterSystem(new TravelSystem());
            if (arriving)
            {
                world.Tick();
                Assert.Throws<ArgumentException>(() => world.Tick());
                Assert.That(world.IsFaulted, Is.True);
            }
            else Assert.Throws<ArgumentException>(() => state.StartTravel(Npc, B));
            Assert.That(npc.CurrentLocation, Is.EqualTo(location));
            Assert.That(npc.Journey, Is.SameAs(journey));
            Assert.That(state.Events.Query(), Is.EqualTo(before));
        }

        [Test]
        public void QueuedTravelLogsNextTickDepartureAndTimedArrivalDespiteReversedRegistration()
        {
            var traces = new List<string>[2];
            var lastNpc = new NpcId("npc_z");
            var ids = new[] { lastNpc, Npc };
            for (int run = 0; run < 2; run++)
            {
                var state = new WorldState(42, new GameTime(100));
                state.InitializeTravel(TinyMap());
                foreach (var id in run == 0 ? ids : ids.Reverse()) state.Travel.RegisterNpc(id, A);
                var world = new World(state);
                IWorldSystem[] systems = { new TravelSystem(), new CommandSystem() };
                foreach (var system in run == 0 ? systems : systems.Reverse()) world.RegisterSystem(system);
                foreach (var id in ids) state.EnqueueCommand(new TravelCommand(id, B));
                Assert.That(state.Travel.Npcs.All(npc => npc.CurrentLocation == A), Is.True);
                Assert.That(state.Events.Count, Is.Zero);
                world.Tick();
                Assert.That(state.PendingCommandCount, Is.Zero);
                Assert.That(state.Events.Count, Is.EqualTo(2));
                Assert.That(state.Travel.Npcs.All(npc => npc.CurrentLocation == null && npc.Journey.Arrival.TotalMinutes == 103), Is.True);
                world.Tick();
                Assert.That(state.Events.Count, Is.EqualTo(2));
                Assert.That(state.Travel.Npcs.All(npc => npc.CurrentLocation == null), Is.True);
                world.Tick();
                Assert.That(state.Travel.Npcs.All(npc => npc.CurrentLocation == B && npc.Journey == null), Is.True);
                var events = state.Events.Query();
                Assert.That(events.Count, Is.EqualTo(4));
                var actors = new[] { lastNpc, Npc, Npc, lastNpc };
                for (int i = 0; i < events.Count; i++)
                    AssertEvent(events[i], i + 1, i < 2 ? 101 : 103, i < 2 ? A : B,
                        i < 2 ? WorldEventType.Departure : WorldEventType.Arrival, actors[i]);
                traces[run] = events.Select(entry => $"{entry.Id.Value}:{entry.Time.TotalMinutes}:{entry.Type}:{entry.Location}:{entry.Actor.Value.Npc}").ToList();
            }
            Assert.That(traces[0], Is.EqualTo(traces[1]));
        }

        private static void AssertEvent(WorldEvent entry, long id, long minute, LocationId location, WorldEventType type, NpcId npc)
        {
            Assert.That((entry.Id.Value, entry.Time.TotalMinutes, entry.Location, entry.Type, entry.Actor, entry.Visibility),
                Is.EqualTo((id, minute, location, type, (ActorId?)ActorId.ForNpc(npc), EventVisibility.Normal)));
        }

        private sealed class TravelCommand : IWorldCommand
        {
            private readonly NpcId _npc;
            private readonly LocationId _destination;
            public TravelCommand(NpcId npc, LocationId destination) { _npc = npc; _destination = destination; }
            public void Execute(WorldState state) => state.StartTravel(_npc, _destination);
        }

        private sealed class StartSystem : IWorldSystem
        {
            public string Id { get; }
            public SimulationPhase Phase { get; }
            public StartSystem(SimulationPhase phase, string id) { Phase = phase; Id = id; }
            public void Tick(WorldState state) { if (state.Clock.TotalMinutes == 101) state.StartTravel(Npc, B); }
        }
    }
}
