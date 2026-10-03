using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>Verifies belief-only proof thresholds and event-logged bounded group standing.</summary>
    public sealed class SuspicionAndReputationTests
    {
        private static readonly NpcId Bram = new NpcId("npc_bram");
        private static readonly NpcId Lida = new NpcId("npc_lida");
        private static readonly NpcId Tom = new NpcId("npc_tom");
        private static readonly LocationId Shop = new LocationId("loc_shop");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly ReputationGroupId Guards = new ReputationGroupId("group_guards");
        private static readonly ReputationGroupId Merchants = new ReputationGroupId("group_merchants");

        [TestCase(69, false)]
        [TestCase(70, true)]
        public void EvidenceActsOnlyAtExplicitThreshold(int score, bool mayAct)
        {
            var state = State(Bram);
            AddBelief(state, Bram, TheftByPlayer(1), score, BeliefSourceKind.Seen);

            SuspicionResult result = SuspicionEvaluator.Evaluate(
                state.Knowledge, Bram, ActorId.Player, 70, new ConfidencePolicy());

            Assert.That((result.TotalEvidence, result.ProofThreshold, result.MayAct),
                Is.EqualTo((score, 70, mayAct)));
        }

        [Test]
        public void RumorAndMissingStockBelowThresholdDoNotAuthorizeAccusation()
        {
            var state = State(Bram);
            AddBelief(state, Bram, TheftByPlayer(6), 50, BeliefSourceKind.ToldBy);
            AddBelief(state, Bram,
                new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple, quantity: 6),
                90, BeliefSourceKind.Inferred);

            SuspicionResult result = SuspicionEvaluator.Evaluate(
                state.Knowledge, Bram, ActorId.Player, 70, new KindPolicy());

            Assert.That(result.TotalEvidence, Is.EqualTo(60));
            Assert.That(result.MayAct, Is.False);
        }

        [Test]
        public void DirectOwnEvidenceActsButEvidenceHeldOnlyByAnotherNpcDoesNot()
        {
            var state = State(Bram, Lida);
            AddBelief(state, Bram,
                new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple, quantity: 6),
                90, BeliefSourceKind.Inferred);
            AddBelief(state, Lida, TheftByPlayer(6), 80, BeliefSourceKind.Seen);

            SuspicionResult withoutOwnEvidence = SuspicionEvaluator.Evaluate(
                state.Knowledge, Bram, ActorId.Player, 70, new KindPolicy());
            Assert.That((withoutOwnEvidence.TotalEvidence, withoutOwnEvidence.MayAct), Is.EqualTo((0, false)));

            AddBelief(state, Bram, TheftByPlayer(6), 80, BeliefSourceKind.Seen);
            SuspicionResult withOwnEvidence = SuspicionEvaluator.Evaluate(
                state.Knowledge, Bram, ActorId.Player, 70, new DirectOnlyPolicy());
            Assert.That((withOwnEvidence.TotalEvidence, withOwnEvidence.MayAct), Is.EqualTo((80, true)));
        }

        [Test]
        public void NamedNonPlayerSuspectUsesOnlyBeliefsIdentifyingThatActor()
        {
            var state = State(Bram);
            ActorId tom = ActorId.ForNpc(Tom);
            AddBelief(state, Bram,
                new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple, tom, quantity: 2),
                75, BeliefSourceKind.Seen);

            SuspicionResult aboutTom = SuspicionEvaluator.Evaluate(
                state.Knowledge, Bram, tom, 70, new DirectOnlyPolicy());
            SuspicionResult aboutPlayer = SuspicionEvaluator.Evaluate(
                state.Knowledge, Bram, ActorId.Player, 70, new DirectOnlyPolicy());

            Assert.That((aboutTom.TotalEvidence, aboutTom.MayAct), Is.EqualTo((75, true)));
            Assert.That((aboutPlayer.TotalEvidence, aboutPlayer.MayAct), Is.EqualTo((0, false)));
        }

        [Test]
        public void EvidencePolicyReceivesGuardBeliefsInStableOrderAndInvalidValuesAreRejected()
        {
            var state = State(Bram);
            AddBelief(state, Bram, TheftByPlayer(3), 40, BeliefSourceKind.Seen);
            AddBelief(state, Bram, TheftByPlayer(1), 40, BeliefSourceKind.Seen);
            AddBelief(state, Bram, TheftByPlayer(2), 40, BeliefSourceKind.Seen);
            var policy = new RecordingPolicy();

            SuspicionResult result = SuspicionEvaluator.Evaluate(
                state.Knowledge, Bram, ActorId.Player, 70, policy);

            Assert.That(policy.Quantities, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(result.TotalEvidence, Is.EqualTo(30));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionEvaluator.Evaluate(state.Knowledge, Bram, ActorId.Player, 0, policy));
            Assert.Throws<ArgumentException>(() =>
                SuspicionEvaluator.Evaluate(state.Knowledge, Bram, default, 70, policy));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionEvaluator.Evaluate(state.Knowledge, Bram, ActorId.Player, 70, new FixedPolicy(101)));
        }

        [Test]
        public void ReputationAdjustsPerGroupClampsAndLogsActualAppliedDeltas()
        {
            var state = new WorldState(4, new GameTime(20));
            var reputation = new ReputationState(new[]
            {
                new ReputationStanding(Guards, 45),
                new ReputationStanding(Merchants, 95)
            });

            WorldEvent suspicion = ReputationAdjuster.Apply(state, reputation, Shop, Guards, -15);
            WorldEvent praise = ReputationAdjuster.Apply(state, reputation, Shop, Merchants, 10);
            WorldEvent noChange = ReputationAdjuster.Apply(state, reputation, Shop, Merchants, 10);
            WorldEvent floor = ReputationAdjuster.Apply(state, reputation, Shop, Guards, -100);

            Assert.That((reputation.Get(Guards), reputation.Get(Merchants)), Is.EqualTo((0, 100)));
            Assert.That(noChange, Is.Null);
            Assert.That(state.Events.Query(type: WorldEventType.ReputationChanged)
                .Select(worldEvent => (worldEvent.ReputationGroup, worldEvent.ReputationDelta)),
                Is.EqualTo(new[]
                {
                    ((ReputationGroupId?)Guards, (int?)-15),
                    ((ReputationGroupId?)Merchants, (int?)5),
                    ((ReputationGroupId?)Guards, (int?)-30)
                }));
            Assert.That((suspicion.Actor, praise.Actor, floor.Actor),
                Is.EqualTo(((ActorId?)ActorId.Player, (ActorId?)ActorId.Player, (ActorId?)ActorId.Player)));
        }

        [Test]
        public void ReconstructedReputationPreservesValuesAndDeterministicOrder()
        {
            var original = new ReputationState(new[]
            {
                new ReputationStanding(Merchants, 72),
                new ReputationStanding(Guards, 31)
            });
            var restored = new ReputationState(original.Standings);

            Assert.That(restored.Standings.Select(entry => entry.Group), Is.EqualTo(new[] { Guards, Merchants }));
            Assert.That(restored.Standings.Select(entry => entry.Value), Is.EqualTo(new[] { 31, 72 }));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<ReputationStanding>)restored.Standings).Clear());
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReputationStanding(Guards, 101));
            Assert.Throws<ArgumentException>(() => new ReputationState(new[]
            {
                new ReputationStanding(Guards, 1), new ReputationStanding(Guards, 2)
            }));
        }

        private static WorldState State(params NpcId[] npcs)
        {
            var state = new WorldState(1);
            foreach (NpcId npc in npcs) state.Knowledge.Register(npc);
            return state;
        }

        private static void AddBelief(WorldState state, NpcId npc, BeliefClaim claim, int confidence,
            BeliefSourceKind sourceKind)
        {
            BeliefSource source = sourceKind == BeliefSourceKind.ToldBy
                ? new BeliefSource(sourceKind, Lida)
                : new BeliefSource(sourceKind);
            state.Knowledge.Get(npc).Set(new Belief(claim, source, confidence, state.Clock));
        }

        private static BeliefClaim TheftByPlayer(int quantity) =>
            new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple, ActorId.Player, quantity);

        private sealed class ConfidencePolicy : IEvidencePolicy
        {
            public int Score(Belief belief, ActorId suspect) => belief.Claim.Subject == suspect
                ? belief.Confidence : 0;
        }

        private sealed class KindPolicy : IEvidencePolicy
        {
            public int Score(Belief belief, ActorId suspect) =>
                belief.Claim.Kind == BeliefClaimKind.StockMissing ? 10 :
                belief.Claim.Subject == suspect ? 50 : 0;
        }

        private sealed class DirectOnlyPolicy : IEvidencePolicy
        {
            public int Score(Belief belief, ActorId suspect) => belief.Source.Kind == BeliefSourceKind.Seen &&
                belief.Claim.Subject == suspect ? belief.Confidence : 0;
        }

        private sealed class FixedPolicy : IEvidencePolicy
        {
            private readonly int _score;
            public FixedPolicy(int score) { _score = score; }
            public int Score(Belief belief, ActorId suspect) => _score;
        }

        private sealed class RecordingPolicy : IEvidencePolicy
        {
            public List<int> Quantities { get; } = new List<int>();
            public int Score(Belief belief, ActorId suspect)
            {
                Quantities.Add(belief.Claim.Quantity.Value);
                return 10;
            }
        }
    }
}
