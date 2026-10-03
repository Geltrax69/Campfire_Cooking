using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>Proves configurable utility choices without Economy or Knowledge coupling.</summary>
    public sealed class DecisionSystemTests
    {
        private static readonly LocationId Home = new LocationId("loc_home");
        private static readonly LocationId Work = new LocationId("loc_work");
        private static readonly LocationId Food = new LocationId("loc_food");
        private static readonly LocationId Shop = new LocationId("loc_shop");
        private static readonly LocationId Social = new LocationId("loc_social");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        [Test]
        public void HungryNpcShopsOnlyWithSuppliedMoneyAndAvailability()
        {
            var withShop = Decide(State("npc_test", 90, 80, 80), Context(copper: 5, shopAvailable: true),
                Tuning(schedule: 100, eatHunger: 5, shopHunger: 10));
            Assert.That((withShop.Kind, withShop.Destination), Is.EqualTo((ActivityKind.Shop, Shop)));

            var noMoney = Decide(State("npc_test", 90, 80, 80), Context(copper: 0, shopAvailable: true),
                Tuning(schedule: 100, eatHunger: 5, shopHunger: 10));
            var unavailable = Decide(State("npc_test", 90, 80, 80), Context(copper: 5, shopAvailable: false),
                Tuning(schedule: 100, eatHunger: 5, shopHunger: 10));
            Assert.That(noMoney.Kind, Is.Not.EqualTo(ActivityKind.Shop));
            Assert.That(unavailable.Kind, Is.Not.EqualTo(ActivityKind.Shop));
        }

        [Test]
        public void NightScheduleChoosesSleepAndMarksNpcSleeping()
        {
            var npc = State("npc_test", 10, 90, 90);
            var state = WorldStateAt(day: 1, hour: 22, npc);
            new DecisionSystem(Tuning(schedule: 1000), new FixedContexts(Context())).Tick(state);

            Assert.That(npc.CurrentIntention.Kind, Is.EqualTo(ActivityKind.Sleep));
            Assert.That(npc.CurrentIntention.Destination, Is.EqualTo(Home));
            Assert.That(npc.CurrentIntention.ChosenAt, Is.EqualTo(state.Clock));
            Assert.That(npc.IsSleeping, Is.True);
        }

        [Test]
        public void CallerWeightsAllowUrgentNeedToOverrideWorkSchedule()
        {
            var npc = State("npc_test", 10, 90, 0);
            var intention = Decide(npc, Context(), Tuning(schedule: 100, socialUrgency: 20));
            Assert.That((intention.Kind, intention.Destination), Is.EqualTo((ActivityKind.Socialize, Social)));
            Assert.That(npc.IsSleeping, Is.False);
        }

        [Test]
        public void SeededTiesAndOrdinalRegistryGiveIdenticalPerNpcResults()
        {
            var first = RunTies(new[] { "npc_z", "npc_A" }, 42);
            var second = RunTies(new[] { "npc_A", "npc_z" }, 42);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(RunTies(new[] { "npc_z", "npc_A" }, 42), Is.EqualTo(first));
            Assert.That(first.Keys, Is.EqualTo(new[] { "npc_A", "npc_z" }));
        }

        [Test]
        public void ConfidentOwnBeliefThatShopItemIsUnavailableChangesChoice()
        {
            var npc = State("npc_test", 90, 80, 80);
            var withoutBelief = Decide(npc, Context(),
                Tuning(eatHunger: 5, shopHunger: 10, adjustments: UnavailableAppleAdjustment(70, 500)));
            Assert.That(withoutBelief.Kind, Is.EqualTo(ActivityKind.Shop));

            npc = State("npc_test", 90, 80, 80);
            var beliefs = BeliefsFor(npc.Definition.Id, Belief(Shop, Apple, quantity: 0, confidence: 80));
            var withBelief = Decide(npc, Context(beliefs: beliefs),
                Tuning(eatHunger: 5, shopHunger: 10, adjustments: UnavailableAppleAdjustment(70, 500)));
            Assert.That(withBelief.Kind, Is.EqualTo(ActivityKind.Eat));
        }

        [Test]
        public void AnotherNpcsMatchingBeliefDoesNotAffectDecision()
        {
            var decidingNpc = State("npc_deciding", 90, 80, 80);
            var state = WorldStateAt(day: 1, hour: 14, decidingNpc);
            var otherStore = state.Knowledge.Register(new NpcId("npc_other"));
            otherStore.Set(Belief(Shop, Apple, quantity: 0, confidence: 100));
            var decidingStore = state.Knowledge.Register(decidingNpc.Definition.Id);

            new DecisionSystem(
                Tuning(eatHunger: 5, shopHunger: 10, adjustments: UnavailableAppleAdjustment(70, 500)),
                new FixedContexts(Context(beliefs: BeliefsFor(decidingStore.Owner, decidingStore.Query()))))
                .Tick(state);

            Assert.That(otherStore.Query(), Has.Count.EqualTo(1));
            Assert.That(decidingNpc.CurrentIntention.Kind, Is.EqualTo(ActivityKind.Shop));
        }

        [Test]
        public void LowConfidenceAndUnrelatedBeliefsPreserveExistingChoice()
        {
            var tuning = Tuning(eatHunger: 5, shopHunger: 10,
                adjustments: UnavailableAppleAdjustment(70, 500));
            var lowConfidenceNpc = State("npc_low", 90, 80, 80);
            var lowConfidence = BeliefsFor(lowConfidenceNpc.Definition.Id,
                Belief(Shop, Apple, quantity: 0, confidence: 69));
            Assert.That(Decide(lowConfidenceNpc, Context(beliefs: lowConfidence), tuning).Kind,
                Is.EqualTo(ActivityKind.Shop));

            var unrelatedNpc = State("npc_unrelated", 90, 80, 80);
            var unrelated = BeliefsFor(unrelatedNpc.Definition.Id,
                Belief(Shop, new ItemTypeId("item_bread"), quantity: 0, confidence: 100));
            Assert.That(Decide(unrelatedNpc, Context(beliefs: unrelated), tuning).Kind,
                Is.EqualTo(ActivityKind.Shop));
        }

        [Test]
        public void BeliefAdjustedSeededTieIsRepeatable()
        {
            ActivityKind first = RunBeliefTie(seed: 17);
            Assert.That(RunBeliefTie(seed: 17), Is.EqualTo(first));
            Assert.That(RunBeliefTie(seed: 17), Is.EqualTo(first));
        }

        [Test]
        public void DecisionInputsAndSystemMetadataAreValidated()
        {
            Assert.Throws<ArgumentNullException>(() => new DecisionSystem(null, new FixedContexts(Context())));
            Assert.Throws<ArgumentNullException>(() => new DecisionSystem(Tuning(), null));
            var stateWithNpc = new WorldState(1, Time(1, 14));
            stateWithNpc.Npcs.Register(State("npc_test", 50, 50, 50));
            Assert.Throws<ArgumentNullException>(() =>
                new DecisionSystem(Tuning(), new NullContexts()).Tick(stateWithNpc));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DecisionContext(-1, true, Food, Shop, Social));
            Assert.Throws<ArgumentException>(() => new DecisionContext(1, true, Food, default, Social));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActivityUtilityWeights(-1, 0, 0, 0, 0));
            Assert.Throws<ArgumentException>(() => new DecisionTuning(Array.Empty<KeyValuePair<ActivityKind, ActivityUtilityWeights>>(),
                50, 1, TieBreakBehavior.SeededRandom));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BeliefUtilityAdjustment(ActivityKind.Shop, UnavailableAppleClaim(), -1, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BeliefUtilityAdjustment(ActivityKind.Shop, UnavailableAppleClaim(), 70, 0));
            Assert.Throws<ArgumentException>(() => new NpcBeliefSnapshot(default, Array.Empty<Belief>()));
            var wrongOwner = BeliefsFor(new NpcId("npc_other"));
            Assert.Throws<InvalidOperationException>(() =>
                Decide(State("npc_test", 90, 80, 80), Context(beliefs: wrongOwner), Tuning()));
            Assert.That(new DecisionSystem(Tuning(), new FixedContexts(Context())).Phase, Is.EqualTo(SimulationPhase.Decisions));
            Assert.That(new DecisionSystem(Tuning(), new FixedContexts(Context())).Id, Is.EqualTo("agents.decisions"));
        }

        private static NpcIntention Decide(NpcState npc, DecisionContext context, DecisionTuning tuning)
        {
            var state = WorldStateAt(day: 1, hour: 14, npc);
            new DecisionSystem(tuning, new FixedContexts(context)).Tick(state);
            return npc.CurrentIntention;
        }

        private static SortedDictionary<string, ActivityKind> RunTies(IEnumerable<string> ids, ulong seed)
        {
            var state = new WorldState(seed, Time(1, 14));
            foreach (var id in ids) state.Npcs.Register(State(id, 50, 50, 50));
            new DecisionSystem(Tuning(), new FixedContexts(Context())).Tick(state);
            return new SortedDictionary<string, ActivityKind>(state.Npcs.Npcs.ToDictionary(
                npc => npc.Definition.Id.Value, npc => npc.CurrentIntention.Kind), StringComparer.Ordinal);
        }

        private static ActivityKind RunBeliefTie(ulong seed)
        {
            var npc = State("npc_test", 90, 80, 80);
            var state = new WorldState(seed, Time(1, 14));
            state.Npcs.Register(npc);
            var beliefs = BeliefsFor(npc.Definition.Id, Belief(Shop, Apple, quantity: 0, confidence: 80));
            new DecisionSystem(Tuning(eatHunger: 5, shopHunger: 10,
                    adjustments: UnavailableAppleAdjustment(70, 450)),
                new FixedContexts(Context(beliefs: beliefs))).Tick(state);
            return npc.CurrentIntention.Kind;
        }

        private static WorldState WorldStateAt(int day, int hour, NpcState npc)
        {
            var state = new WorldState(42, Time(day, hour));
            state.Npcs.Register(npc);
            return state;
        }

        private static NpcState State(string id, int hunger, int energy, int social)
        {
            var workday = new[] {
                new ScheduleEntry(ActivityKind.Work, 8 * 60, 17 * 60, Work),
                new ScheduleEntry(ActivityKind.Sleep, 21 * 60, 6 * 60, Home) };
            var restday = new[] { new ScheduleEntry(ActivityKind.Sleep, 21 * 60, 7 * 60, Home) };
            var definition = new NpcDefinition(new NpcId(id), id, 30, "test", "tester", Home, Work, 10,
                new Dictionary<string, int>(), new NeedRates(0, 0, 0), new NpcSchedule(workday, restday));
            return new NpcState(definition, hunger, energy, social);
        }

        private static DecisionContext Context(int copper = 10, bool shopAvailable = true,
            NpcBeliefSnapshot beliefs = null) =>
            new DecisionContext(copper, shopAvailable, Food, Shop, Social, beliefs);

        private static DecisionTuning Tuning(int schedule = 0, int eatHunger = 0,
            int shopHunger = 0, int socialUrgency = 0,
            IEnumerable<BeliefUtilityAdjustment> adjustments = null)
        {
            var values = Enum.GetValues(typeof(ActivityKind)).Cast<ActivityKind>().ToDictionary(kind => kind,
                kind => new ActivityUtilityWeights(0, schedule, kind == ActivityKind.Eat ? eatHunger :
                    kind == ActivityKind.Shop ? shopHunger : 0, 0,
                    kind == ActivityKind.Socialize ? socialUrgency : 0));
            return new DecisionTuning(values, shopHungerThreshold: 70, shopMinimumCopper: 1,
                TieBreakBehavior.SeededRandom, adjustments);
        }

        private static IEnumerable<BeliefUtilityAdjustment> UnavailableAppleAdjustment(
            int minimumConfidence, int penalty)
        {
            return new[] { new BeliefUtilityAdjustment(ActivityKind.Shop, UnavailableAppleClaim(),
                minimumConfidence, penalty) };
        }

        private static BeliefClaim UnavailableAppleClaim() =>
            new BeliefClaim(BeliefClaimKind.StockAvailable, Shop, Apple, quantity: 0);

        private static Belief Belief(LocationId location, ItemTypeId item, int quantity, int confidence) =>
            new Belief(new BeliefClaim(BeliefClaimKind.StockAvailable, location, item, quantity: quantity),
                new BeliefSource(BeliefSourceKind.Inferred), confidence, Time(1, 13));

        private static NpcBeliefSnapshot BeliefsFor(NpcId owner, params Belief[] beliefs) =>
            new NpcBeliefSnapshot(owner, beliefs);

        private static NpcBeliefSnapshot BeliefsFor(NpcId owner, IEnumerable<Belief> beliefs) =>
            new NpcBeliefSnapshot(owner, beliefs);

        private static GameTime Time(int day, int hour) => new GameTime((day - 1L) * 1440 + hour * 60);

        private sealed class FixedContexts : IDecisionContextProvider
        {
            private readonly DecisionContext _context;
            public FixedContexts(DecisionContext context) { _context = context; }
            public DecisionContext GetSnapshot(NpcState npc) => _context;
        }

        private sealed class NullContexts : IDecisionContextProvider
        {
            public DecisionContext GetSnapshot(NpcState npc) => null;
        }
    }
}
