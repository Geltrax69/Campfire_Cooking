using System;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>
    /// Verifies relationship dynamics: honest trades, gifts, tavern conversations, witnessed
    /// thefts and missed debt repayments shift trust/affection by small bounded rules, every
    /// shift logs a RelationshipShift event, and pairs decay back toward their Content
    /// baselines one point per day per axis.
    /// </summary>
    public sealed class RelationshipDynamicsTests
    {
        private const long Day = 1440;
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Ralf = new NpcId("npc_ralf");
        private static readonly NpcId Tom = new NpcId("npc_tom");
        private static readonly NpcId Bessa = new NpcId("npc_bessa");
        private static readonly LocationId Store = new LocationId("loc_store");
        private static readonly LocationId Tavern = new LocationId("loc_tavern");
        private static readonly LocationId Square = new LocationId("loc_square");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        [Test]
        public void HonestTradeRaisesMutualTrustByTwo()
        {
            World world = NewWorld(
                (Mira, Ralf, 60, 55, "Steady company"),
                (Ralf, Mira, 30, 90, "Fond of his landlady"));
            WorldState state = world.State;

            // Ralf buys from Mira: a completed honest trade.
            state.Events.Append(state.Clock, Store, WorldEventType.Purchase,
                ActorId.ForNpc(Ralf), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 3, 9);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Ralf, Mira), Is.EqualTo(32));
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(62));
            Assert.That(state.Knowledge.Relationships.Affection(Ralf, Mira), Is.EqualTo(90),
                "Trade moves trust, not affection.");
            Assert.That(state.Knowledge.Relationships.Affection(Mira, Ralf), Is.EqualTo(55));

            var shifts = state.Events.Query(type: WorldEventType.RelationshipShift).ToList();
            Assert.That(shifts, Has.Count.EqualTo(2), "One shift event per direction.");
            Assert.That(shifts.Select(e => (e.Actor.Value, e.Targets.Single())),
                Is.EquivalentTo(new[]
                {
                    (ActorId.ForNpc(Ralf), ActorId.ForNpc(Mira)),
                    (ActorId.ForNpc(Mira), ActorId.ForNpc(Ralf)),
                }));
            Assert.That(shifts.Select(e => e.Visibility), Is.All.EqualTo(EventVisibility.Quiet));
            Assert.That(state.Knowledge.Relationships.TryGet(Mira, Ralf, out Relationship pair), Is.True);
            Assert.That(pair.Reason, Does.Contain("Steady company").And.Contain("honest trade"));
        }

        [Test]
        public void PartialPurchaseDoesNotShiftTrust()
        {
            World world = NewWorld((Mira, Ralf, 60, 55, "Steady company"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Store, WorldEventType.PartialPurchase,
                ActorId.ForNpc(Ralf), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 2, 6);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Ralf, Mira), Is.EqualTo(50),
                "Only completed trades build trust; the pair was never recorded, so strangers stay 50.");
            Assert.That(state.Events.Query(type: WorldEventType.RelationshipShift), Is.Empty);
        }

        [Test]
        public void WitnessedTheftDropsTrustScaledByPriorTrust()
        {
            World world = NewWorld(
                (Mira, Tom, 80, 60, "Trusted friend"),
                (Bessa, Tom, 10, 20, "Barely knows him"),
                (Ralf, Tom, 70, 50, "Old neighbor"));
            WorldState state = world.State;

            WorldEvent theft = state.Events.Append(state.Clock, Store, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
            Witness(state, Mira, theft);
            Witness(state, Bessa, theft);
            // Ralf has no perception record: he did not see it.
            world.Tick();

            // Drop = 1 + priorTrust * 4 / 100: betrayal by a trusted friend hurts more.
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(80 - 4));
            Assert.That(state.Knowledge.Relationships.Trust(Bessa, Tom), Is.EqualTo(10 - 1));
            Assert.That(state.Knowledge.Relationships.Trust(Ralf, Tom), Is.EqualTo(70),
                "No perception record, no shift: truth alone moves nothing.");
            Assert.That(state.Knowledge.Relationships.Affection(Mira, Tom), Is.EqualTo(60),
                "Theft moves trust, not affection.");
            Assert.That(state.Events.Query(type: WorldEventType.RelationshipShift), Has.Count.EqualTo(2));
        }

        [Test]
        public void ThiefDoesNotLoseTrustInSelf()
        {
            World world = NewWorld((Mira, Tom, 80, 60, "Trusted friend"));
            WorldState state = world.State;

            WorldEvent theft = state.Events.Append(state.Clock, Store, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
            Witness(state, Mira, theft);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(76));
            Assert.That(state.Knowledge.Relationships.Count, Is.EqualTo(1),
                "No self-pair may ever be created.");
        }

        [Test]
        public void GiftRaisesReceiverAffectionAndSmallTrust()
        {
            World world = NewWorld(
                (Mira, Ralf, 60, 55, "Steady company"),
                (Ralf, Mira, 30, 90, "Fond of his landlady"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Tavern, WorldEventType.Gift,
                ActorId.ForNpc(Ralf), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Quiet, Apple, 2);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Affection(Mira, Ralf), Is.EqualTo(58));
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(61));
            Assert.That(state.Knowledge.Relationships.Affection(Ralf, Mira), Is.EqualTo(90),
                "Gifts move the receiver's feelings, not the giver's.");
            Assert.That(state.Events.Query(type: WorldEventType.RelationshipShift), Has.Count.EqualTo(1));
        }

        [Test]
        public void TavernConversationBuildsMutualAffection()
        {
            World world = NewWorld(
                (Mira, Ralf, 60, 55, "Steady company"),
                (Ralf, Mira, 30, 90, "Fond of his landlady"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Tavern, WorldEventType.Conversation,
                ActorId.ForNpc(Mira), new[] { ActorId.ForNpc(Ralf) }, EventVisibility.Quiet);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Affection(Mira, Ralf), Is.EqualTo(56));
            Assert.That(state.Knowledge.Relationships.Affection(Ralf, Mira), Is.EqualTo(91));
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(60),
                "Tavern talk moves affection, not trust.");
        }

        [Test]
        public void ConversationOutsideTavernDoesNothing()
        {
            World world = NewWorld((Mira, Ralf, 60, 55, "Steady company"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Square, WorldEventType.Conversation,
                ActorId.ForNpc(Mira), new[] { ActorId.ForNpc(Ralf) }, EventVisibility.Quiet);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Affection(Mira, Ralf), Is.EqualTo(55));
            Assert.That(state.Events.Query(type: WorldEventType.RelationshipShift), Is.Empty);
        }

        [Test]
        public void DebtMissedDropsCreditorTrustInDebtor()
        {
            World world = NewWorld(
                (Mira, Tom, 40, 60, "Half-fond anyway"),
                (Tom, Mira, 50, 50, "Keeps a tab"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Tavern, WorldEventType.DebtMissed,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Quiet);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(37));
            Assert.That(state.Knowledge.Relationships.Trust(Tom, Mira), Is.EqualTo(50),
                "The debtor's trust in the creditor does not move.");
        }

        [Test]
        public void TrustClampsAtHundredAndSkipsNoOpShiftEvent()
        {
            World world = NewWorld((Mira, Ralf, 100, 55, "Steady company"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Store, WorldEventType.Purchase,
                ActorId.ForNpc(Ralf), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 1, 3);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(100));
            Assert.That(state.Events.Query(type: WorldEventType.RelationshipShift), Has.Count.EqualTo(1),
                "Only Ralf->Mira shifted (50->52); Mira->Ralf was already 100, so no event.");
        }

        [Test]
        public void TrustClampsAtZero()
        {
            World world = NewWorld((Mira, Tom, 2, 60, "Barely tolerated"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Tavern, WorldEventType.DebtMissed,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Quiet);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(0),
                "2 - 3 clamps to 0, never negative.");
        }

        [Test]
        public void PlayerTradeDoesNotShiftNpcRelationships()
        {
            World world = NewWorld((Mira, Ralf, 60, 55, "Steady company"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Store, WorldEventType.Purchase,
                ActorId.Player, new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 3, 9);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(60));
            Assert.That(state.Knowledge.Relationships.Count, Is.EqualTo(1));
            Assert.That(state.Events.Query(type: WorldEventType.RelationshipShift), Is.Empty,
                "The player is not in the relationship registry.");
        }

        [Test]
        public void DecayDriftsOnePointPerDayTowardBaseline()
        {
            World world = NewWorld((Mira, Tom, 40, 60, "Half-fond anyway"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Tavern, WorldEventType.DebtMissed,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Quiet);
            world.Tick();
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(37));

            TickDays(world, 3);

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(40),
                "37 -> 38 -> 39 -> 40: one point per day back to the Content baseline.");
            Assert.That(state.Knowledge.Relationships.TryGet(Mira, Tom, out Relationship pair), Is.True);
            Assert.That(pair.Reason, Is.EqualTo("Half-fond anyway"),
                "A fully decayed pair recovers its baseline reason.");
        }

        [Test]
        public void DecayNeverOvershootsBaseline()
        {
            World world = NewWorld((Mira, Tom, 40, 60, "Half-fond anyway"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Tavern, WorldEventType.DebtMissed,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Quiet);
            world.Tick();
            TickDays(world, 30);

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(40));
            Assert.That(state.Knowledge.Relationships.Affection(Mira, Tom), Is.EqualTo(60));
        }

        [Test]
        public void BaselineIsFirstCaptureNotLatestShift()
        {
            World world = NewWorld((Mira, Ralf, 60, 55, "Steady company"));
            WorldState state = world.State;

            for (int i = 0; i < 3; i++)
            {
                state.Events.Append(state.Clock, Store, WorldEventType.Purchase,
                    ActorId.ForNpc(Ralf), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 1, 3);
                world.Tick();
            }
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(66));

            TickDays(world, 30);

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(60),
                "Decay targets the Content baseline (60), not the latest shifted value.");
        }

        [Test]
        public void ShiftOnStrangerPairCreatesPairWithStrangerBaseline()
        {
            World world = NewWorld();
            WorldState state = world.State;
            state.Knowledge.Register(Bessa);
            state.Knowledge.Register(Tom);

            state.Events.Append(state.Clock, Tavern, WorldEventType.Gift,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Bessa) }, EventVisibility.Quiet, Apple, 2);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Bessa, Tom), Is.EqualTo(51));
            Assert.That(state.Knowledge.Relationships.Affection(Bessa, Tom), Is.EqualTo(53));
            Assert.That(state.Knowledge.TryGetRelationshipBaseline(Bessa, Tom, out RelationshipBaseline baseline),
                Is.True);
            Assert.That((baseline.Trust, baseline.Affection), Is.EqualTo((50, 50)),
                "A new pair decays toward the stranger default.");

            TickDays(world, 10);
            Assert.That(state.Knowledge.Relationships.Trust(Bessa, Tom), Is.EqualTo(50));
            Assert.That(state.Knowledge.Relationships.Affection(Bessa, Tom), Is.EqualTo(50));
        }

        [Test]
        public void DynamicsStateRoundTripsThroughCaptureRestore()
        {
            World world = NewWorld((Mira, Tom, 40, 60, "Half-fond anyway"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Tavern, WorldEventType.DebtMissed,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Quiet);
            world.Tick();
            TickDays(world, 1); // Trust 37 -> 38; cursor, day and baseline are now captured.

            long cursor = state.Knowledge.CaptureDynamicsCursor();
            long day = state.Knowledge.CaptureDynamicsDay();
            var baselines = state.Knowledge.CaptureRelationshipBaselines();
            Assert.That(cursor, Is.GreaterThan(0));
            Assert.That(day, Is.EqualTo(state.Clock.Day));
            Assert.That(baselines, Has.Count.EqualTo(1));

            var fresh = new WorldState(7, new GameTime(0));
            fresh.Knowledge.Register(Mira);
            fresh.Knowledge.Register(Tom);
            fresh.Knowledge.RestoreDynamicsCursor(cursor);
            fresh.Knowledge.RestoreDynamicsDay(day);
            fresh.Knowledge.RestoreRelationshipBaselines(baselines);

            Assert.That(fresh.Knowledge.CaptureDynamicsCursor(), Is.EqualTo(cursor));
            Assert.That(fresh.Knowledge.CaptureDynamicsDay(), Is.EqualTo(day));
            Assert.That(fresh.Knowledge.TryGetRelationshipBaseline(Mira, Tom, out RelationshipBaseline restored),
                Is.True);
            Assert.That((restored.Trust, restored.Affection, restored.Reason),
                Is.EqualTo((40, 60, "Half-fond anyway")));

            Assert.Throws<ArgumentNullException>(() => fresh.Knowledge.RestoreRelationshipBaselines(null));
            Assert.Throws<ArgumentException>(() =>
                fresh.Knowledge.RestoreRelationshipBaselines(new[] { restored, restored }));
            Assert.Throws<ArgumentOutOfRangeException>(() => fresh.Knowledge.RestoreDynamicsCursor(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => fresh.Knowledge.RestoreDynamicsDay(-1));
        }

        [Test]
        public void BaselineValidationRejectsBadRecords()
        {
            Assert.Throws<ArgumentException>(() => new RelationshipBaseline(default, Ralf, 50, 50, "x"));
            Assert.Throws<ArgumentException>(() => new RelationshipBaseline(Mira, Mira, 50, 50, "self"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RelationshipBaseline(Mira, Ralf, 101, 50, "x"));
            Assert.Throws<ArgumentException>(() => new RelationshipBaseline(Mira, Ralf, 50, 50, "  "));
        }

        private static World NewWorld(params (NpcId from, NpcId to, int trust, int affection, string reason)[] pairs)
        {
            var state = new WorldState(42, new GameTime(0));
            var npcs = pairs.SelectMany(pair => new[] { pair.from, pair.to }).Distinct().ToList();
            foreach (NpcId npc in npcs) state.Knowledge.Register(npc);
            state.Knowledge.InitializeRelationships(pairs.Select(pair =>
                new Relationship(pair.from, pair.to, pair.trust, pair.affection, pair.reason)));
            var world = new World(state);
            world.RegisterSystem(new RelationshipDynamicsSystem());
            return world;
        }

        private static void Witness(WorldState state, NpcId witness, WorldEvent theft)
        {
            var claim = new BeliefClaim(BeliefClaimKind.TheftObserved, theft.Location,
                theft.ItemType, theft.Actor, theft.Quantity);
            var source = new BeliefSource(BeliefSourceKind.Seen, originEventId: theft.Id);
            state.Knowledge.Get(witness).Set(new Belief(claim, source, 80, state.Clock));
        }

        private static void TickDays(World world, int days)
        {
            for (long i = 0; i < days * Day; i++) world.Tick();
        }
    }
}
