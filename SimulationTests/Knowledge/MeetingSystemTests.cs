using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>
    /// Verifies the evening tavern rule: at 20:00 every NPC whose schedule has them
    /// socializing at the tavern meets exactly one partner, chosen by ConversationChoice
    /// (friends seek each other out), and shares their most newsworthy belief through
    /// RumorExchange with the relationship-based trust filter — a trusted friend's
    /// warning lands, a distrusted stranger's dies.
    /// </summary>
    public sealed class MeetingSystemTests
    {
        private static readonly NpcId Bessa = new NpcId("npc_bessa");
        private static readonly NpcId Bram = new NpcId("npc_bram");
        private static readonly NpcId Tom = new NpcId("npc_tom");
        private static readonly LocationId Tavern = new LocationId("loc_tavern");
        private static readonly LocationId Home = new LocationId("loc_test_home");
        private static readonly ItemTypeId Apples = new ItemTypeId("item_apple");

        [Test]
        public void TavernGoersConverseAndShareRumors()
        {
            WorldState state = TavernWorld(42, out World world, Bessa, Bram);
            SeedSeenTheft(state, Bessa, Tom, Apples, confidence: 80);

            world.RegisterSystem(new MeetingSystem());
            world.Tick();

            IReadOnlyList<WorldEvent> conversations = Conversations(state);
            Assert.That(conversations.Count, Is.EqualTo(1), "One pair, one conversation.");
            IReadOnlyList<Belief> bramKnows = state.Knowledge.Get(Bram)
                .Query(BeliefClaimKind.TheftObserved);
            Assert.That(bramKnows.Count, Is.EqualTo(1), "A stranger half-believes the rumor.");
            Assert.That(bramKnows[0].Source.Kind, Is.EqualTo(BeliefSourceKind.ToldBy));
            Assert.That(bramKnows[0].Confidence, Is.EqualTo(27),
                "80 confidence x 80 plausibility x 50 stranger trust, all / 100, minus 5 loss.");
        }

        [Test]
        public void DistrustedSpeakerRumorDiesButConversationHappens()
        {
            WorldState state = TavernWorld(42, out World world, Bessa, Bram);
            state.Knowledge.InitializeRelationships(new[]
            {
                new Relationship(Bram, Bessa, 10, 20, "test"),
                new Relationship(Bessa, Bram, 50, 50, "test"),
            });
            SeedSeenTheft(state, Bessa, Tom, Apples, confidence: 80);

            world.RegisterSystem(new MeetingSystem());
            world.Tick();

            Assert.That(Conversations(state).Count, Is.EqualTo(1));
            Assert.That(state.Knowledge.Get(Bram).Query(BeliefClaimKind.TheftObserved), Is.Empty,
                "Bram distrusts Bessa: the rumor dies with the evening.");
        }

        [Test]
        public void TrustedFriendWarningLands()
        {
            WorldState state = TavernWorld(42, out World world, Bessa, Bram);
            state.Knowledge.InitializeRelationships(new[]
            {
                new Relationship(Bram, Bessa, 80, 80, "test"),
                new Relationship(Bessa, Bram, 80, 80, "test"),
            });
            SeedSeenTheft(state, Bessa, Tom, Apples, confidence: 80);

            world.RegisterSystem(new MeetingSystem());
            world.Tick();

            IReadOnlyList<Belief> bramKnows = state.Knowledge.Get(Bram)
                .Query(BeliefClaimKind.TheftObserved);
            Assert.That(bramKnows.Count, Is.EqualTo(1));
            Assert.That(bramKnows[0].Confidence, Is.EqualTo(46),
                "80 x 80 plausibility x 80 trust, all / 100, minus 5 loss: a trusted friend's warning lands.");
        }

        [Test]
        public void NpcNotAtTavernDoesNotMeet()
        {
            WorldState state = MixedWorld(42, out World world,
                (Bessa, Tavern), (Bram, Home));

            world.RegisterSystem(new MeetingSystem());
            world.Tick();

            Assert.That(Conversations(state), Is.Empty, "No partner, no conversation.");
        }

        [Test]
        public void EveryoneMeetsAtMostOncePerEvening()
        {
            WorldState state = TavernWorld(42, out World world, Bessa, Bram, Tom);

            world.RegisterSystem(new MeetingSystem());
            world.Tick();

            IReadOnlyList<WorldEvent> conversations = Conversations(state);
            Assert.That(conversations.Count, Is.EqualTo(1), "Three goers make one pair and a spare.");
            var participants = new List<NpcId>();
            foreach (WorldEvent c in conversations)
            {
                if (c.Actor.HasValue && c.Actor.Value.Npc.HasValue)
                    participants.Add(c.Actor.Value.Npc.Value);
                foreach (ActorId target in c.Targets)
                    if (target.Npc.HasValue) participants.Add(target.Npc.Value);
            }
            Assert.That(participants, Has.Count.EqualTo(2), "Nobody is paired twice.");
            Assert.That(participants.Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void MeetingsOnlyHappenAtTwenty()
        {
            var state = new WorldState(42, new GameTime(19 * 60));
            var world = new World(state);
            RegisterWithSchedule(state, Bessa, Tavern);
            RegisterWithSchedule(state, Bram, Tavern);

            world.RegisterSystem(new MeetingSystem());
            world.Tick();

            Assert.That(Conversations(state), Is.Empty);
        }

        [Test]
        public void PairingIsDeterministic()
        {
            IReadOnlyList<WorldEvent> first = RunEvening(7);
            IReadOnlyList<WorldEvent> second = RunEvening(7);
            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int i = 0; i < first.Count; i++)
            {
                var firstPair = first[i].Targets.Prepend(first[i].Actor.Value)
                    .Select(id => id.Npc.Value.Value).OrderBy(v => v).ToList();
                var secondPair = second[i].Targets.Prepend(second[i].Actor.Value)
                    .Select(id => id.Npc.Value.Value).OrderBy(v => v).ToList();
                Assert.That(firstPair, Is.EqualTo(secondPair), "Same seed, same pairs.");
            }
        }

        private static IReadOnlyList<WorldEvent> RunEvening(ulong seed)
        {
            WorldState state = TavernWorld(seed, out World world, Bessa, Bram, Tom);
            SeedSeenTheft(state, Bessa, Tom, Apples, confidence: 80);
            world.RegisterSystem(new MeetingSystem());
            world.Tick();
            return Conversations(state);
        }

        private static IReadOnlyList<WorldEvent> Conversations(WorldState state) =>
            state.Events.Query(null, null, Tavern, WorldEventType.Conversation).ToList();

        private static void SeedSeenTheft(WorldState state, NpcId speaker, NpcId thief,
            ItemTypeId item, int confidence)
        {
            WorldEvent theft = state.Events.Append(state.Clock, Tavern, WorldEventType.Theft,
                ActorId.ForNpc(thief), new[] { ActorId.ForNpc(speaker) }, EventVisibility.Normal,
                item, 6);
            var claim = new BeliefClaim(BeliefClaimKind.TheftObserved, theft.Location,
                theft.ItemType, theft.Actor, theft.Quantity);
            state.Knowledge.Get(speaker).Set(new Belief(claim,
                new BeliefSource(BeliefSourceKind.Seen, originEventId: theft.Id),
                confidence, state.Clock));
        }

        private static WorldState TavernWorld(ulong seed, out World world, params NpcId[] npcs)
        {
            var destinations = new (NpcId, LocationId)[npcs.Length];
            for (int i = 0; i < npcs.Length; i++) destinations[i] = (npcs[i], Tavern);
            return MixedWorld(seed, out world, destinations);
        }

        private static WorldState MixedWorld(ulong seed, out World world,
            params (NpcId npc, LocationId destination)[] goers)
        {
            var state = new WorldState(seed, new GameTime(20 * 60 - 1));
            world = new World(state);
            foreach ((NpcId npc, LocationId destination) in goers)
                RegisterWithSchedule(state, npc, destination);
            return state;
        }

        private static void RegisterWithSchedule(WorldState state, NpcId npc, LocationId destination)
        {
            var definition = new NpcDefinition(npc, npc.Value, 30, "test", "tester",
                Home, Home, 0, new Dictionary<string, int> { ["honest"] = 50 },
                new NeedRates(0, 0, 0), EveningSchedule(destination));
            state.Npcs.Register(new NpcState(definition, 30, 80, 50));
            state.Knowledge.Register(npc);
        }

        private static NpcSchedule EveningSchedule(LocationId destination)
        {
            var workday = new[]
            {
                new ScheduleEntry(ActivityKind.Work, 9 * 60, 17 * 60, Home),
                new ScheduleEntry(ActivityKind.Socialize, 19 * 60, 23 * 60, destination),
            };
            return new NpcSchedule(workday, workday);
        }
    }
}
