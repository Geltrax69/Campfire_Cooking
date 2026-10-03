using System;
using System.IO;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>
    /// Verifies attributed interaction memories: significant relationship shifts (magnitude
    /// >= 2) record a memory about the actor (WrongedBy / GiftFrom / FairTradeWith) with
    /// importance scaling by shift magnitude; recalling the memory when its subject is
    /// perceived nudges the relationship ±1 in the original direction while the memory is
    /// strong; and total recall-driven movement per memory can never exceed the original
    /// shift (no runaway feedback).
    /// </summary>
    public sealed class AttributedMemoryTests
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
        public void WitnessedTheftCreatesWrongedByMemory()
        {
            World world = NewWorld(
                (Mira, Tom, 80, 60, "Trusted friend"),
                (Bessa, Tom, 10, 20, "Barely knows him"));
            WorldState state = world.State;

            WorldEvent theft = state.Events.Append(state.Clock, Store, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
            Witness(state, Mira, theft);
            Witness(state, Bessa, theft);
            world.Tick();

            // Drop = 1 + 80 * 4 / 100 = 4: a significant shift, so Mira remembers.
            Memory memory = state.Knowledge.GetMemories(Mira).Query().Single();
            Assert.That(memory.Claim.Kind, Is.EqualTo(BeliefClaimKind.WrongedBy));
            Assert.That(memory.Claim.Subject, Is.EqualTo(ActorId.ForNpc(Tom)));
            Assert.That(memory.Claim.Location, Is.EqualTo(Store));
            Assert.That(memory.Importance, Is.EqualTo(80), "Importance = 20 x shift magnitude.");
            WorldEvent shift = state.Events.Query(type: WorldEventType.RelationshipShift)
                .Single(e => e.Actor.Equals(ActorId.ForNpc(Mira)));
            Assert.That(memory.OriginEventId, Is.EqualTo(shift.Id),
                "The memory's origin is the RelationshipShift event, not the theft.");

            Assert.That(state.Knowledge.TryGetAttributedMemory(Mira, memory.Claim, out AttributedMemory record),
                Is.True);
            Assert.That((record.OriginalTrustDelta, record.OriginalAffectionDelta), Is.EqualTo((-4, 0)));
            Assert.That((record.RecalledTrustDelta, record.RecalledAffectionDelta), Is.EqualTo((0, 0)));

            // Bessa's drop was 1 + 10 * 4 / 100 = 1: below the threshold, no memory.
            Assert.That(state.Knowledge.GetMemories(Bessa).Query(), Is.Empty);
        }

        [Test]
        public void GiftCreatesGiftFromMemory()
        {
            World world = NewWorld(
                (Mira, Ralf, 60, 55, "Steady company"),
                (Ralf, Mira, 30, 90, "Fond of his landlady"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Tavern, WorldEventType.Gift,
                ActorId.ForNpc(Ralf), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Quiet, Apple, 2);
            world.Tick();

            Memory memory = state.Knowledge.GetMemories(Mira).Query().Single();
            Assert.That(memory.Claim.Kind, Is.EqualTo(BeliefClaimKind.GiftFrom));
            Assert.That(memory.Claim.Subject, Is.EqualTo(ActorId.ForNpc(Ralf)));
            Assert.That(memory.Importance, Is.EqualTo(60), "Magnitude = max(1 trust, 3 affection) = 3.");
            Assert.That(state.Knowledge.TryGetAttributedMemory(Mira, memory.Claim, out AttributedMemory record),
                Is.True);
            Assert.That((record.OriginalTrustDelta, record.OriginalAffectionDelta), Is.EqualTo((1, 3)));

            Assert.That(state.Knowledge.GetMemories(Ralf).Query(), Is.Empty,
                "The giver holds no memory; only the shifted NPC remembers.");
        }

        [Test]
        public void HonestTradeCreatesFairTradeWithMemoriesBothWays()
        {
            World world = NewWorld(
                (Mira, Ralf, 60, 55, "Steady company"),
                (Ralf, Mira, 30, 90, "Fond of his landlady"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Store, WorldEventType.Purchase,
                ActorId.ForNpc(Ralf), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 3, 9);
            world.Tick();

            Memory miraMemory = state.Knowledge.GetMemories(Mira).Query().Single();
            Assert.That(miraMemory.Claim.Kind, Is.EqualTo(BeliefClaimKind.FairTradeWith));
            Assert.That(miraMemory.Claim.Subject, Is.EqualTo(ActorId.ForNpc(Ralf)));
            Assert.That(miraMemory.Importance, Is.EqualTo(40));

            Memory ralfMemory = state.Knowledge.GetMemories(Ralf).Query().Single();
            Assert.That(ralfMemory.Claim.Kind, Is.EqualTo(BeliefClaimKind.FairTradeWith));
            Assert.That(ralfMemory.Claim.Subject, Is.EqualTo(ActorId.ForNpc(Mira)));
        }

        [Test]
        public void SmallShiftBelowThresholdCreatesNoMemory()
        {
            World world = NewWorld((Mira, Ralf, 60, 55, "Steady company"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Tavern, WorldEventType.Conversation,
                ActorId.ForNpc(Mira), new[] { ActorId.ForNpc(Ralf) }, EventVisibility.Quiet);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Affection(Mira, Ralf), Is.EqualTo(56));
            Assert.That(state.Knowledge.GetMemories(Mira).Query(), Is.Empty);
            Assert.That(state.Knowledge.GetMemories(Ralf).Query(), Is.Empty);
            Assert.That(state.Knowledge.CaptureAttributedMemories(), Is.Empty);
        }

        [Test]
        public void ClampedNoOpShiftCreatesNoMemory()
        {
            World world = NewWorld((Mira, Ralf, 100, 55, "Steady company"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Store, WorldEventType.Purchase,
                ActorId.ForNpc(Ralf), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 1, 3);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(100));
            Assert.That(state.Knowledge.GetMemories(Mira).Query(), Is.Empty,
                "Nothing moved, so there is nothing to remember.");
        }

        [Test]
        public void RecallNudgesTrustWhileMemoryIsStrong()
        {
            World world = NewWorld((Mira, Tom, 80, 60, "Trusted friend"));
            WorldState state = world.State;

            WorldEvent theft = state.Events.Append(state.Clock, Store, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
            Witness(state, Mira, theft);
            world.Tick();
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(76));

            PerceiveArrival(state, Mira, Tom, Square);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(75),
                "Recalling the fresh betrayal stings: trust nudges down by 1.");
            Memory memory = state.Knowledge.GetMemories(Mira).Query().Single();
            Assert.That(state.Knowledge.TryGetAttributedMemory(Mira, memory.Claim, out AttributedMemory record),
                Is.True);
            Assert.That(record.RecalledTrustDelta, Is.EqualTo(-1));
            Assert.That(state.Knowledge.GetMemories(Mira).Query(), Has.Count.EqualTo(1),
                "Reinforcement never creates a new attributed memory.");
            Assert.That(state.Events.Query(type: WorldEventType.RelationshipShift), Has.Count.EqualTo(1),
                "Recall is silent: it logs no shift event.");
        }

        [Test]
        public void RecallNeedsPerceptionNotWorldTruth()
        {
            World world = NewWorld((Mira, Tom, 80, 60, "Trusted friend"));
            WorldState state = world.State;

            WorldEvent theft = state.Events.Append(state.Clock, Store, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
            Witness(state, Mira, theft);
            world.Tick();

            // Tom truly arrives at the square, but Mira never notices him.
            state.Events.Append(state.Clock, Square, WorldEventType.Arrival, ActorId.ForNpc(Tom));
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(76),
                "World truth alone triggers no recall; only perceived actors resurface memories.");
        }

        [Test]
        public void RecallDoesNothingAfterMemoryDecays()
        {
            World world = NewWorldWithMemoryDecay((Mira, Tom, 25, 60, "Wary tolerance"));
            WorldState state = world.State;

            // Drop = 1 + 25 * 4 / 100 = 2: importance 40, Notable, pruned after 30 days.
            WorldEvent theft = state.Events.Append(state.Clock, Store, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
            Witness(state, Mira, theft);
            world.Tick();
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(23));

            TickDays(world, 35);
            Assert.That(state.Knowledge.GetMemories(Mira).Query(), Is.Empty,
                "The Notable memory was pruned after its 30-day retention.");
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(25),
                "Decay healed the pair back to baseline in the meantime.");

            PerceiveArrival(state, Mira, Tom, Square);
            world.Tick();

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(25),
                "A faded memory barely nudges: with the memory gone, recall moves nothing.");
        }

        [Test]
        public void NoRunawayAcrossThirtyDaysOfRecall()
        {
            World world = NewWorld((Mira, Tom, 80, 60, "Trusted friend"));
            WorldState state = world.State;

            WorldEvent theft = state.Events.Append(state.Clock, Store, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
            Witness(state, Mira, theft);
            world.Tick();

            // Thirty days of Mira perceiving Tom: without a bound this would move trust by -30.
            for (int day = 0; day < 30; day++)
            {
                PerceiveArrival(state, Mira, Tom, Square);
                TickDays(world, 1);
            }

            Memory memory = state.Knowledge.GetMemories(Mira).Query().Single();
            Assert.That(state.Knowledge.TryGetAttributedMemory(Mira, memory.Claim, out AttributedMemory record),
                Is.True);
            Assert.That(record.RecalledTrustDelta, Is.EqualTo(record.OriginalTrustDelta),
                "Recall-driven movement stopped exactly at the original shift's magnitude.");
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Tom), Is.EqualTo(80),
                "Recalls moved trust by 4 total (the bound), then daily decay healed the pair " +
                "back to baseline: the past cannot hold the present hostage forever.");
            Assert.That(state.Knowledge.GetMemories(Mira).Query(), Has.Count.EqualTo(1),
                "Thirty recalls created no new attributed memories.");
            Assert.That(state.Events.Query(type: WorldEventType.RelationshipShift), Has.Count.EqualTo(1),
                "Recall logged no shift events across thirty days.");
        }

        [Test]
        public void GiftRecallMovesBothAxesWithinTheirBounds()
        {
            World world = NewWorld((Mira, Ralf, 60, 55, "Steady company"));
            WorldState state = world.State;

            state.Events.Append(state.Clock, Tavern, WorldEventType.Gift,
                ActorId.ForNpc(Ralf), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Quiet, Apple, 2);
            world.Tick();
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(61));
            Assert.That(state.Knowledge.Relationships.Affection(Mira, Ralf), Is.EqualTo(58));

            // Five recalls inside a single game day (no daily decay interferes).
            for (int i = 0; i < 5; i++)
            {
                PerceiveArrival(state, Mira, Ralf, Square);
                world.Tick();
            }

            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(62),
                "Trust recalled +1: the original gift moved trust by exactly 1.");
            Assert.That(state.Knowledge.Relationships.Affection(Mira, Ralf), Is.EqualTo(61),
                "Affection recalled +3: the original gift moved affection by exactly 3.");
            Memory memory = state.Knowledge.GetMemories(Mira).Query().Single();
            Assert.That(state.Knowledge.TryGetAttributedMemory(Mira, memory.Claim, out AttributedMemory record),
                Is.True);
            Assert.That((record.RecalledTrustDelta, record.RecalledAffectionDelta), Is.EqualTo((1, 3)));
        }

        [Test]
        public void ReinforcingShiftKeepsFirstCaptureBound()
        {
            World world = NewWorld((Mira, Tom, 80, 60, "Trusted friend"));
            WorldState state = world.State;

            for (int i = 0; i < 2; i++)
            {
                WorldEvent theft = state.Events.Append(state.Clock, Store, WorldEventType.Theft,
                    ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
                Witness(state, Mira, theft);
                world.Tick();
            }

            // One memory (same claim reinforced), but the ledger keeps the first shift's bound.
            Memory memory = state.Knowledge.GetMemories(Mira).Query().Single();
            Assert.That(state.Knowledge.TryGetAttributedMemory(Mira, memory.Claim, out AttributedMemory record),
                Is.True);
            Assert.That(record.OriginalTrustDelta, Is.EqualTo(-4),
                "First capture wins: the reinforced memory does not move its own bound.");
        }

        [Test]
        public void AttributedClaimValidation()
        {
            var claim = new BeliefClaim(BeliefClaimKind.WrongedBy, Store, subject: ActorId.ForNpc(Tom));
            Assert.That(claim.Subject, Is.EqualTo(ActorId.ForNpc(Tom)));

            Assert.Throws<ArgumentException>(() => new BeliefClaim(BeliefClaimKind.WrongedBy, Store),
                "Attributed claims must name their actor.");
            Assert.Throws<ArgumentException>(() =>
                new BeliefClaim(BeliefClaimKind.GiftFrom, Store, Apple, ActorId.ForNpc(Ralf), 2),
                "Attributed claims cannot contain item details.");
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BeliefClaim((BeliefClaimKind)999, Store, subject: ActorId.ForNpc(Tom)));
        }

        [Test]
        public void AttributedMemoryValidation()
        {
            var claim = new BeliefClaim(BeliefClaimKind.WrongedBy, Store, subject: ActorId.ForNpc(Tom));
            var record = new AttributedMemory(Mira, claim, -4, 0);
            Assert.That(record.Subject, Is.EqualTo(Tom));

            var stockClaim = new BeliefClaim(BeliefClaimKind.StockMissing, Store, Apple, quantity: 6);
            Assert.Throws<ArgumentException>(() => new AttributedMemory(Mira, stockClaim, -4, 0),
                "Only attributed claims can back an attributed memory.");
            Assert.Throws<ArgumentException>(() => new AttributedMemory(default, claim, -4, 0));
        }

        [Test]
        public void AttributedMemoryRestoreRoundTrips()
        {
            World world = NewWorld((Mira, Tom, 80, 60, "Trusted friend"));
            WorldState state = world.State;

            WorldEvent theft = state.Events.Append(state.Clock, Store, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
            Witness(state, Mira, theft);
            world.Tick();
            PerceiveArrival(state, Mira, Tom, Square);
            world.Tick();

            var snapshot = state.Knowledge.CaptureAttributedMemories();
            Assert.That(snapshot, Has.Count.EqualTo(1));

            var fresh = new WorldState(7, new GameTime(0));
            fresh.Knowledge.Register(Mira);
            fresh.Knowledge.Register(Tom);
            fresh.Knowledge.RestoreAttributedMemories(snapshot);

            Memory memory = state.Knowledge.GetMemories(Mira).Query().Single();
            Assert.That(fresh.Knowledge.TryGetAttributedMemory(Mira, memory.Claim, out AttributedMemory restored),
                Is.True);
            Assert.That((restored.OriginalTrustDelta, restored.RecalledTrustDelta), Is.EqualTo((-4, -1)));

            Assert.Throws<ArgumentNullException>(() => fresh.Knowledge.RestoreAttributedMemories(null));
            Assert.Throws<ArgumentException>(() =>
                fresh.Knowledge.RestoreAttributedMemories(new[] { restored, restored }));
            Assert.Throws<ArgumentOutOfRangeException>(() => fresh.Knowledge.RestoreRecallCursor(-1));
        }

        private static World NewWorld(params (NpcId from, NpcId to, int trust, int affection, string reason)[] pairs)
        {
            var state = new WorldState(42, new GameTime(0));
            var npcs = pairs.SelectMany(pair => new[] { pair.from, pair.to }).Distinct().ToList();
            foreach (NpcId npc in npcs) state.Knowledge.Register(npc);
            state.Knowledge.InitializeRelationships(pairs.Select(pair =>
                new Relationship(pair.from, pair.to, pair.trust, pair.affection, pair.reason)));
            var world = new World(state);
            // Recall sorts before dynamics inside the Social phase by system id.
            world.RegisterSystem(new MemoryRecallSystem());
            world.RegisterSystem(new RelationshipDynamicsSystem());
            return world;
        }

        private static World NewWorldWithMemoryDecay(
            params (NpcId from, NpcId to, int trust, int affection, string reason)[] pairs)
        {
            World world = NewWorld(pairs);
            world.RegisterSystem(new MemorySystem(ApprovedRules()));
            return world;
        }

        private static void Witness(WorldState state, NpcId witness, WorldEvent theft)
        {
            var claim = new BeliefClaim(BeliefClaimKind.TheftObserved, theft.Location,
                theft.ItemType, theft.Actor, theft.Quantity);
            var source = new BeliefSource(BeliefSourceKind.Seen, originEventId: theft.Id);
            state.Knowledge.Get(witness).Set(new Belief(claim, source, 80, state.Clock));
        }

        private static void PerceiveArrival(WorldState state, NpcId perceiver, NpcId subject, LocationId location)
        {
            WorldEvent arrival = state.Events.Append(state.Clock, location, WorldEventType.Arrival,
                ActorId.ForNpc(subject));
            var claim = new BeliefClaim(BeliefClaimKind.Presence, location, subject: ActorId.ForNpc(subject));
            var source = new BeliefSource(BeliefSourceKind.Seen, originEventId: arrival.Id);
            state.Knowledge.Get(perceiver).Set(new Belief(claim, source, 80, state.Clock));
        }

        private static void TickDays(World world, int days)
        {
            for (long i = 0; i < days * Day; i++) world.Tick();
        }

        private static MemoryRules ApprovedRules()
        {
            using var stream = File.OpenRead(ContentPath());
            return MemoryRules.Load(stream);
        }

        private static string ContentPath()
        {
            var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/social/social.json")))
                root = root.Parent;
            Assert.That(root, Is.Not.Null, "Find repository content above test output.");
            return Path.Combine(root.FullName, "Content/social/social.json");
        }
    }
}
