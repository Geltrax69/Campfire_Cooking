using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>Verifies deterministic event perception without leaking truth to ineligible NPCs.</summary>
    public sealed class PerceptionTests
    {
        private static readonly LocationId Shop = new LocationId("loc_shop");
        private static readonly LocationId Home = new LocationId("loc_home");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly NpcId Awake = new NpcId("npc_awake");
        private static readonly NpcId Asleep = new NpcId("npc_asleep");
        private static readonly NpcId Remote = new NpcId("npc_remote");
        private static readonly NpcId Travelling = new NpcId("npc_travelling");

        [Test]
        public void OnlyPresentAwakeCandidatesReachTuningOrConsumeNoticeRolls()
        {
            var state = State(42, Awake, Asleep, Remote, Travelling);
            var context = new TestContext(new[] { Remote, Travelling, Asleep, Awake });
            context.PresentAt[Awake] = Shop;
            context.PresentAt[Asleep] = Shop;
            context.PresentAt[Remote] = Home;
            context.Awake.ExceptWith(new[] { Asleep, Travelling });
            var tuning = new TestTuning(100, 70);
            state.Events.Append(default, Shop, WorldEventType.Theft, ActorId.Player,
                visibility: EventVisibility.Quiet, itemType: Apple, quantity: 6);

            new PerceptionSystem(context, tuning).Tick(state);

            Assert.That(state.Knowledge.Get(Awake).Query(), Has.Count.EqualTo(1));
            Assert.That(state.Knowledge.Get(Asleep).Query(), Is.Empty);
            Assert.That(state.Knowledge.Get(Remote).Query(), Is.Empty);
            Assert.That(state.Knowledge.Get(Travelling).Query(), Is.Empty);
            Assert.That(tuning.ChanceRequests, Is.EqualTo(new[] { Awake }));
            var expected = new SimRng(42);
            expected.NextInt(100);
            Assert.That(state.Rng.State, Is.EqualTo(expected.State));
        }

        [TestCase(0UL, true)]
        [TestCase(1UL, false)]
        public void FixedSeedsReproduceNoticedAndMissedOutcomes(ulong seed, bool noticed)
        {
            var first = Observe(seed, 50);
            var second = Observe(seed, 50);
            Assert.That(first.noticed, Is.EqualTo(noticed));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void EventsAndCandidatesProcessInStableOrder()
        {
            var a = new NpcId("npc_A");
            var z = new NpcId("npc_z");
            var state = State(9, z, a);
            var context = new TestContext(new[] { z, a });
            context.PresentAt[a] = Shop;
            context.PresentAt[z] = Shop;
            var tuning = new TestTuning(100, 80);
            state.Events.Append(default, Shop, WorldEventType.Theft, ActorId.Player, itemType: Apple, quantity: 1);
            state.Events.Append(default, Shop, WorldEventType.Theft, ActorId.Player, itemType: Apple, quantity: 2);

            new PerceptionSystem(context, tuning).Tick(state);

            Assert.That(tuning.ChanceRequests, Is.EqualTo(new[] { a, z, a, z }));
            Assert.That(state.Knowledge.Get(a).Query().Select(belief => belief.Claim.Quantity), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(state.Knowledge.Get(z).Query().Select(belief => belief.Claim.Quantity), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void LaterTicksDoNotDuplicateBeliefsOrConsumeRollsForOldEvents()
        {
            var state = State(4, Awake);
            var context = Eligible(Awake);
            var tuning = new TestTuning(100, 60);
            state.Events.Append(default, Shop, WorldEventType.Theft, ActorId.Player, itemType: Apple, quantity: 6);
            var system = new PerceptionSystem(context, tuning);
            system.Tick(state);
            ulong rngAfterFirst = state.Rng.State;
            Belief belief = state.Knowledge.Get(Awake).Query().Single();

            system.Tick(state);

            Assert.That(state.Knowledge.Get(Awake).Query().Single(), Is.SameAs(belief));
            Assert.That(state.Rng.State, Is.EqualTo(rngAfterFirst));
            Assert.That(tuning.ChanceRequests, Is.EqualTo(new[] { Awake }));
        }

        [Test]
        public void ActorlessTheftRemainsUnidentifiedAndReferencesOnlyItsEvent()
        {
            var state = State(2, Awake);
            WorldEvent truth = state.Events.Append(new GameTime(5), Shop, WorldEventType.Theft,
                visibility: EventVisibility.Quiet, itemType: Apple, quantity: 6);
            var system = new PerceptionSystem(Eligible(Awake), new TestTuning(100, 75));

            system.Tick(state);

            Belief belief = state.Knowledge.Get(Awake).Query().Single();
            Assert.That(belief.Claim.Kind, Is.EqualTo(BeliefClaimKind.TheftObserved));
            Assert.That(belief.Claim.Subject, Is.Null);
            Assert.That((belief.Claim.Location, belief.Claim.ItemType, belief.Claim.Quantity),
                Is.EqualTo((Shop, (ItemTypeId?)Apple, 6)));
            Assert.That(belief.Source.Kind, Is.EqualTo(BeliefSourceKind.Seen));
            Assert.That(belief.Source.OriginEventId, Is.EqualTo(truth.Id));
            Assert.That(belief.Confidence, Is.EqualTo(75));
            Assert.That(belief.LearnedAt, Is.EqualTo(state.Clock));
        }

        [Test]
        public void PresenceBeliefsRequireActorFactsAndUnsupportedEventsStillAdvanceCursor()
        {
            var state = State(7, Awake);
            var context = Eligible(Awake);
            var tuning = new TestTuning(100, 50);
            state.Events.Append(default, Shop, WorldEventType.Conversation);
            state.Events.Append(default, Shop, WorldEventType.Arrival, ActorId.Player);
            var system = new PerceptionSystem(context, tuning);

            system.Tick(state);
            system.Tick(state);

            Belief belief = state.Knowledge.Get(Awake).Query().Single();
            Assert.That(belief.Claim.Kind, Is.EqualTo(BeliefClaimKind.Presence));
            Assert.That(belief.Claim.Subject, Is.EqualTo(ActorId.Player));
            Assert.That(tuning.ChanceRequests, Is.EqualTo(new[] { Awake }));
        }

        [Test]
        public void RejectsInvalidCollaboratorsCandidatesAndTuningOutputs()
        {
            Assert.Throws<ArgumentNullException>(() => new PerceptionSystem(null, new TestTuning(1, 1)));
            Assert.Throws<ArgumentNullException>(() => new PerceptionSystem(Eligible(Awake), null));
            var state = State(0, Awake);
            state.Events.Append(default, Shop, WorldEventType.Theft, itemType: Apple, quantity: 1);
            Assert.Throws<ArgumentException>(() =>
                new PerceptionSystem(new TestContext(new[] { default(NpcId) }), new TestTuning(1, 1)).Tick(state));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PerceptionSystem(Eligible(Awake), new TestTuning(101, 1)).Tick(state));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PerceptionSystem(Eligible(Awake), new TestTuning(100, -1)).Tick(StateWithTheft(0, Awake)));
        }

        private static (bool noticed, ulong rng) Observe(ulong seed, int chance)
        {
            var state = StateWithTheft(seed, Awake);
            new PerceptionSystem(Eligible(Awake), new TestTuning(chance, 70)).Tick(state);
            return (state.Knowledge.Get(Awake).Count == 1, state.Rng.State);
        }

        private static WorldState StateWithTheft(ulong seed, params NpcId[] npcs)
        {
            var state = State(seed, npcs);
            state.Events.Append(default, Shop, WorldEventType.Theft, ActorId.Player, itemType: Apple, quantity: 6);
            return state;
        }

        private static WorldState State(ulong seed, params NpcId[] npcs)
        {
            var state = new WorldState(seed, new GameTime(5));
            foreach (NpcId npc in npcs) state.Knowledge.Register(npc);
            return state;
        }

        private static TestContext Eligible(NpcId npc)
        {
            var context = new TestContext(new[] { npc });
            context.PresentAt[npc] = Shop;
            return context;
        }

        private sealed class TestContext : IPerceptionContext
        {
            private readonly IReadOnlyList<NpcId> _candidates;
            public TestContext(IReadOnlyList<NpcId> candidates)
            {
                _candidates = candidates;
                Awake = new HashSet<NpcId>(candidates.Where(candidate => candidate.IsValid));
            }
            public Dictionary<NpcId, LocationId> PresentAt { get; } = new Dictionary<NpcId, LocationId>();
            public HashSet<NpcId> Awake { get; }
            public IEnumerable<NpcId> Candidates(WorldEvent worldEvent) => _candidates;
            public bool IsPresentAt(NpcId npc, LocationId location) =>
                PresentAt.TryGetValue(npc, out var actual) && actual == location;
            public bool IsAwake(NpcId npc) => Awake.Contains(npc);
        }

        private sealed class TestTuning : IPerceptionTuning
        {
            private readonly int _chance;
            private readonly int _confidence;
            public TestTuning(int chance, int confidence) { _chance = chance; _confidence = confidence; }
            public List<NpcId> ChanceRequests { get; } = new List<NpcId>();
            public int NoticeChancePercent(NpcId npc, WorldEvent worldEvent)
            {
                ChanceRequests.Add(npc);
                return _chance;
            }
            public int Confidence(NpcId npc, WorldEvent worldEvent) => _confidence;
        }
    }
}
