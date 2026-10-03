using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Scenarios
{
    /// <summary>Proves every Phase-1 Apple Test exit condition in both perception branches.</summary>
    public sealed class AppleTestAcceptanceTests
    {
        private const ulong WitnessSeed = 42;
        private const ulong NoWitnessSeed = 987654321;

        [Test]
        public void WitnessBranchSpreadsTraceableRumorButDoesNotReachProof()
        {
            AppleTestResult result = AppleTestHarness.Run(WitnessSeed);

            AssertCommonOutcomes(result);
            Assert.That(result.WitnessObservedTheft, Is.True);
            AppleBeliefView lida = TheftBelief(result, ScenarioVillage.Witness);
            AppleBeliefView bessa = TheftBelief(result, ScenarioVillage.Contact);
            AppleBeliefView bram = TheftBelief(result, ScenarioVillage.Guard);
            Assert.That((lida.SourceKind, lida.SourceChain),
                Is.EqualTo((BeliefSourceKind.Seen, new NpcId[0])));
            Assert.That((bessa.SourceKind, bessa.Speaker, bessa.SourceChain), Is.EqualTo(
                (BeliefSourceKind.ToldBy, (NpcId?)ScenarioVillage.Witness,
                    new[] { ScenarioVillage.Witness })));
            Assert.That((bram.SourceKind, bram.Speaker, bram.SourceChain), Is.EqualTo(
                (BeliefSourceKind.ToldBy, (NpcId?)ScenarioVillage.Contact,
                    new[] { ScenarioVillage.Witness, ScenarioVillage.Contact })));
            Assert.That((result.GuardSuspicion.TotalEvidence, result.GuardSuspicion.ProofThreshold,
                result.GuardSuspicion.MayAct), Is.EqualTo((50, 70, false)));
            Assert.That(result.StoryReport, Does.Contain("Day 1 14:00 — The player stole 6 apples"));
            Assert.That(result.StoryReport, Does.Contain("Knowledge — Lida saw the theft"));
            Assert.That(result.StoryReport, Does.Contain("Day 2 morning — A customer bought the last 4 apples"));
            Assert.That(result.StoryReport, Does.Contain("Day 3 08:00 — Corvin produced and delivered 45 apples"));
            Assert.That(result.StoryReport, Does.Contain("Guard conclusion — Bram had 50 of 70 evidence"));
            TestContext.Progress.WriteLine(result.StoryReport);
        }

        [Test]
        public void NoWitnessBranchCreatesNoPlayerTheftKnowledgeOrSuspicion()
        {
            AppleTestResult result = AppleTestHarness.Run(NoWitnessSeed);

            AssertCommonOutcomes(result);
            Assert.That(result.WitnessObservedTheft, Is.False);
            Assert.That(result.Beliefs.Any(belief => belief.Kind == BeliefClaimKind.TheftObserved
                && belief.Subject == ActorId.Player), Is.False);
            Assert.That(result.Beliefs.Any(belief => belief.Kind == BeliefClaimKind.TheftObserved
                && belief.SourceKind == BeliefSourceKind.ToldBy), Is.False);
            Assert.That(result.Beliefs.Any(belief => belief.Subject == ActorId.Player), Is.False);
            Assert.That((result.GuardSuspicion.TotalEvidence, result.GuardSuspicion.ProofThreshold,
                result.GuardSuspicion.MayAct), Is.EqualTo((0, 70, false)));
        }

        [TestCase(WitnessSeed)]
        [TestCase(NoWitnessSeed)]
        public void SameSeedProducesByteIdenticalStoryAndOutcomeSnapshot(ulong seed)
        {
            AppleTestResult first = AppleTestHarness.Run(seed);
            AppleTestResult second = AppleTestHarness.Run(seed);

            Assert.That(second.StoryReport, Is.EqualTo(first.StoryReport));
            Assert.That(second.Snapshot, Is.EqualTo(first.Snapshot));
        }

        private static void AssertCommonOutcomes(AppleTestResult result)
        {
            Assert.That(result.Events.Single(entry => entry.Type == WorldEventType.Theft).Quantity, Is.EqualTo(6));
            Assert.That(result.Events.Any(entry => entry.Type == WorldEventType.PartialPurchase), Is.True);
            Assert.That(result.Events.Any(entry => entry.Type == WorldEventType.FailedPurchase), Is.True);
            AppleBeliefView missing = result.Beliefs.Single(belief => belief.Knower == ScenarioVillage.Mira
                && belief.Kind == BeliefClaimKind.StockMissing);
            Assert.That((missing.Quantity, missing.Subject), Is.EqualTo(((int?)6, (ActorId?)null)));
            Assert.That((result.FinalShopStock, result.FinalRetailCopper), Is.EqualTo((45, 4)));
            Assert.That(result.InitialCopperTotal, Is.EqualTo(result.FinalCopperTotal));
        }

        private static AppleBeliefView TheftBelief(AppleTestResult result, NpcId knower) =>
            result.Beliefs.Single(belief => belief.Knower == knower
                && belief.Kind == BeliefClaimKind.TheftObserved && belief.Subject == ActorId.Player);
    }
}
