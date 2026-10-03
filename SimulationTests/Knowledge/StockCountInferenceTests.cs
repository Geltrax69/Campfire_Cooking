using System;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>Verifies that physical counts create deficit beliefs without inventing a thief.</summary>
    public sealed class StockCountInferenceTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Bram = new NpcId("npc_bram");
        private static readonly LocationId Shop = new LocationId("loc_shop");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        [Test]
        public void TwentyExpectedAndFourteenObservedCreatesSixMissingForCounterOnly()
        {
            var state = new WorldState(42, new GameTime(1140));
            state.Knowledge.Register(Mira);
            state.Knowledge.Register(Bram);
            WorldEvent theft = state.Events.Append(new GameTime(840), Shop, WorldEventType.Theft,
                ActorId.Player, itemType: Apple, quantity: 6);

            WorldEvent counted = StockCountInference.Record(state,
                new StockCountSnapshot(Mira, Shop, Apple, 20, 14), 90);

            Assert.That((counted.Type, counted.Time, counted.Location, counted.Actor, counted.ItemType,
                counted.Quantity, counted.Copper, counted.Targets.Count),
                Is.EqualTo((WorldEventType.StockCounted, state.Clock, Shop, (ActorId?)ActorId.ForNpc(Mira),
                    (ItemTypeId?)Apple, 14, (int?)null, 0)));
            Belief belief = state.Knowledge.Get(Mira).Query().Single();
            Assert.That((belief.Claim.Kind, belief.Claim.Location, belief.Claim.ItemType,
                belief.Claim.Quantity, belief.Claim.Subject),
                Is.EqualTo((BeliefClaimKind.StockMissing, Shop, (ItemTypeId?)Apple, 6, (ActorId?)null)));
            Assert.That((belief.Source.Kind, belief.Source.OriginEventId, belief.Confidence, belief.LearnedAt),
                Is.EqualTo((BeliefSourceKind.Inferred, (WorldEventId?)counted.Id, 90, state.Clock)));
            Assert.That(belief.Source.OriginEventId, Is.Not.EqualTo(theft.Id));
            Assert.That(state.Knowledge.Get(Bram).Query(), Is.Empty);
            Assert.That(state.Knowledge.RetainedEventIds(), Is.EqualTo(new[] { counted.Id }));
        }

        [TestCase(20, 20)]
        [TestCase(20, 22)]
        public void EqualOrSurplusCountsRecordTruthWithoutMissingBelief(int expected, int observed)
        {
            var state = StateWithMira();
            WorldEvent counted = StockCountInference.Record(state,
                new StockCountSnapshot(Mira, Shop, Apple, expected, observed), 90);
            Assert.That((counted.Type, counted.Quantity), Is.EqualTo((WorldEventType.StockCounted, observed)));
            Assert.That(state.Knowledge.Get(Mira).Query(), Is.Empty);
            Assert.That(state.Knowledge.RetainedEventIds(), Is.Empty);
        }

        [Test]
        public void LaterCountExplicitlyReplacesSameDeficitBeliefWithoutAmplifyingConfidence()
        {
            var state = StateWithMira();
            StockCountInference.Record(state, new StockCountSnapshot(Mira, Shop, Apple, 20, 14), 90);
            state.Clock = new GameTime(1200);
            WorldEvent latest = StockCountInference.Record(state,
                new StockCountSnapshot(Mira, Shop, Apple, 18, 12), 35);

            Belief belief = state.Knowledge.Get(Mira).Query().Single();
            Assert.That(belief.Confidence, Is.EqualTo(35));
            Assert.That(belief.Source.OriginEventId, Is.EqualTo(latest.Id));
            Assert.That(belief.LearnedAt, Is.EqualTo(new GameTime(1200)));
        }

        [Test]
        public void LaterDifferentDeficitReplacesPriorMissingClaimButKeepsUnrelatedBeliefs()
        {
            var state = StateWithMira();
            StockCountInference.Record(state, new StockCountSnapshot(Mira, Shop, Apple, 20, 14), 90);
            Belief originalMissing = state.Knowledge.Get(Mira).Query(BeliefClaimKind.StockMissing).Single();
            state.Knowledge.GetMemories(Mira).Remember(originalMissing.Claim, 80, state.Clock,
                originalMissing.Source.OriginEventId);
            Memory historicalMemory = state.Knowledge.GetMemories(Mira).Query().Single();
            var unrelated = new Belief(
                new BeliefClaim(BeliefClaimKind.Presence, Shop, subject: ActorId.ForNpc(Bram)),
                new BeliefSource(BeliefSourceKind.Inferred), 40, state.Clock);
            state.Knowledge.Get(Mira).Set(unrelated);
            state.Clock = new GameTime(1200);

            StockCountInference.Record(state, new StockCountSnapshot(Mira, Shop, Apple, 20, 16), 75);

            var missing = state.Knowledge.Get(Mira).Query(BeliefClaimKind.StockMissing);
            Assert.That(missing.Select(belief => belief.Claim.Quantity), Is.EqualTo(new int?[] { 4 }));
            Assert.That(state.Knowledge.Get(Mira).Query(BeliefClaimKind.Presence).Single(), Is.SameAs(unrelated));
            Assert.That(state.Knowledge.GetMemories(Mira).Query().Single(), Is.SameAs(historicalMemory));
        }

        [TestCase(20, 20)]
        [TestCase(20, 22)]
        public void ResolvedOrSurplusCountClearsPriorMissingClaim(int expected, int observed)
        {
            var state = StateWithMira();
            StockCountInference.Record(state, new StockCountSnapshot(Mira, Shop, Apple, 20, 14), 90);
            state.Clock = new GameTime(1200);

            StockCountInference.Record(state, new StockCountSnapshot(Mira, Shop, Apple, expected, observed), 90);

            Assert.That(state.Knowledge.Get(Mira).Query(BeliefClaimKind.StockMissing), Is.Empty);
        }

        [Test]
        public void InvalidInputsAndMissingStoreLeaveTruthAndKnowledgeUnchanged()
        {
            Assert.Throws<ArgumentException>(() => new StockCountSnapshot(default, Shop, Apple, 20, 14));
            Assert.Throws<ArgumentException>(() => new StockCountSnapshot(Mira, default, Apple, 20, 14));
            Assert.Throws<ArgumentException>(() => new StockCountSnapshot(Mira, Shop, default, 20, 14));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StockCountSnapshot(Mira, Shop, Apple, -1, 14));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StockCountSnapshot(Mira, Shop, Apple, 20, -1));

            var state = StateWithMira();
            var snapshot = new StockCountSnapshot(Mira, Shop, Apple, 20, 14);
            Assert.Throws<ArgumentOutOfRangeException>(() => StockCountInference.Record(state, snapshot, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => StockCountInference.Record(state, snapshot, 101));
            Assert.That(state.Events.Query(), Is.Empty);
            Assert.That(state.Knowledge.Get(Mira).Query(), Is.Empty);

            var missingStore = new WorldState(42);
            Assert.Throws<InvalidOperationException>(() => StockCountInference.Record(missingStore, snapshot, 90));
            Assert.That(missingStore.Events.Query(), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => StockCountInference.Record(null, snapshot, 90));
            Assert.Throws<ArgumentNullException>(() => StockCountInference.Record(state, null, 90));
        }

        [Test]
        public void BackdatedReconciliationIsRejectedBeforeCountTruthIsAppended()
        {
            var state = StateWithMira();
            var claim = new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple, quantity: 5);
            Belief future = new Belief(claim, new BeliefSource(BeliefSourceKind.Inferred), 50, new GameTime(10));
            state.Knowledge.Get(Mira).Set(future);

            Assert.Throws<InvalidOperationException>(() => StockCountInference.Record(state,
                new StockCountSnapshot(Mira, Shop, Apple, 20, 14), 90));

            Assert.That(state.Events.Query(), Is.Empty);
            Assert.That(state.Knowledge.Get(Mira).Query().Single(), Is.SameAs(future));
        }

        private static WorldState StateWithMira()
        {
            var state = new WorldState(42);
            state.Knowledge.Register(Mira);
            return state;
        }
    }
}
