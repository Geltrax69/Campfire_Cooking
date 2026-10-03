using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>
    /// Verifies that rumor adoption follows the listener's directed trust in the speaker:
    /// a trusted friend's warning lands at high confidence, a distrusted stranger's story
    /// is rejected outright below the trust floor, and a believed wrongdoing rumor leaves
    /// a second-hand WrongedBy memory at half the witnessed importance.
    /// </summary>
    public sealed class RumorTrustFilterTests
    {
        private static readonly NpcId Bessa = new NpcId("npc_bessa");
        private static readonly NpcId Bram = new NpcId("npc_bram");
        private static readonly NpcId Tom = new NpcId("npc_tom");
        private static readonly LocationId Tavern = new LocationId("loc_tavern");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        [Test]
        public void TrustedSpeakerRumorAdoptedAtHighConfidence()
        {
            WorldState state = StateWith((Bram, Bessa, 80, 60));
            WorldEvent theft = state.Events.Append(state.Clock, Tavern, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Bram) }, EventVisibility.Normal, Apple, 6);
            SpeakSeenTheft(state, Bessa, theft, 80);

            ConversationContext context = RumorTrustFilter.ForConversation(
                state, Bessa, Bram, Tavern, 100, 0, 0);
            ConversationResult result = RumorExchange.Share(state, context, new AnyPolicy(), new NoDistortion());

            Assert.That(context.TrustPercent, Is.EqualTo(80));
            Assert.That(result.AdoptedBelief, Is.Not.Null);
            Assert.That(result.AdoptedBelief.Confidence, Is.EqualTo(64),
                "80 x 80 / 100: a trusted friend's warning lands.");
        }

        [Test]
        public void DistrustedSpeakerRumorRejectedOutright()
        {
            WorldState state = StateWith((Bram, Bessa, 10, 20));
            WorldEvent theft = state.Events.Append(state.Clock, Tavern, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Bram) }, EventVisibility.Normal, Apple, 6);
            SpeakSeenTheft(state, Bessa, theft, 80);

            ConversationContext context = RumorTrustFilter.ForConversation(
                state, Bessa, Bram, Tavern, 100, 0, 0);
            ConversationResult result = RumorExchange.Share(state, context, new AnyPolicy(), new NoDistortion());

            Assert.That(context.TrustPercent, Is.EqualTo(0), "Below the floor, trust counts as zero.");
            Assert.That(result.AdoptedBelief, Is.Null);
            Assert.That(state.Knowledge.Get(Bram).Query(), Is.Empty, "Nothing is learned.");
            Assert.That(result.Conversation, Is.Not.Null, "The conversation still happened.");
        }

        [Test]
        public void StrangerRumorHalfBelieved()
        {
            WorldState state = StateWith();
            state.Knowledge.Register(Bessa);
            state.Knowledge.Register(Bram);
            WorldEvent theft = state.Events.Append(state.Clock, Tavern, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Bram) }, EventVisibility.Normal, Apple, 6);
            SpeakSeenTheft(state, Bessa, theft, 80);

            ConversationContext context = RumorTrustFilter.ForConversation(
                state, Bessa, Bram, Tavern, 100, 0, 0);
            ConversationResult result = RumorExchange.Share(state, context, new AnyPolicy(), new NoDistortion());

            Assert.That(context.TrustPercent, Is.EqualTo(50), "Strangers start neutral.");
            Assert.That(result.AdoptedBelief.Confidence, Is.EqualTo(40));
        }

        [TestCase(0, 0)]
        [TestCase(10, 0)]
        [TestCase(24, 0)]
        [TestCase(25, 25)]
        [TestCase(80, 80)]
        public void TrustFloorDismissesOnlyTheDistrusted(int trust, int expected)
        {
            WorldState state = StateWith((Bram, Bessa, trust, 50));
            Assert.That(RumorTrustFilter.TrustPercentFor(state, Bram, Bessa), Is.EqualTo(expected));
        }

        [Test]
        public void BelievedWrongdoingRumorCreatesSecondHandMemory()
        {
            WorldState state = StateWith((Bram, Bessa, 90, 70));
            WorldEvent theft = state.Events.Append(state.Clock, Tavern, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Bessa) }, EventVisibility.Normal, Apple, 6);
            var wronged = new BeliefClaim(BeliefClaimKind.WrongedBy, Tavern, subject: ActorId.ForNpc(Tom));
            state.Knowledge.Get(Bessa).Set(new Belief(wronged,
                new BeliefSource(BeliefSourceKind.Seen, originEventId: theft.Id), 80, state.Clock));

            ConversationContext context = RumorTrustFilter.ForConversation(
                state, Bessa, Bram, Tavern, 100, 0, 0);
            ConversationResult result = RumorExchange.Share(state, context, new WrongedPolicy(), new NoDistortion());

            Assert.That(result.AdoptedBelief.Confidence, Is.EqualTo(72), "80 x 90 / 100.");
            Memory memory = state.Knowledge.GetMemories(Bram).Query().Single();
            Assert.That(memory.Claim.Kind, Is.EqualTo(BeliefClaimKind.WrongedBy));
            Assert.That(memory.Claim.Subject, Is.EqualTo(ActorId.ForNpc(Tom)));
            Assert.That(memory.Importance, Is.EqualTo(36),
                "Half the adopted confidence: second-hand, below the 40 minimum for witnessing.");
            Assert.That(state.Knowledge.TryGetAttributedMemory(Bram, memory.Claim, out AttributedMemory record),
                Is.True);
            Assert.That((record.OriginalTrustDelta, record.OriginalAffectionDelta), Is.EqualTo((0, 0)),
                "Hearsay is remembered but never stings on sight.");
            Assert.That(state.Knowledge.Relationships.Trust(Bram, Tom), Is.EqualTo(50),
                "No direct shift: the listener did not experience it.");
        }

        [Test]
        public void DisbelievedRumorLeavesNoMemory()
        {
            WorldState state = StateWith((Bram, Bessa, 10, 20));
            WorldEvent theft = state.Events.Append(state.Clock, Tavern, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Bessa) }, EventVisibility.Normal, Apple, 6);
            var wronged = new BeliefClaim(BeliefClaimKind.WrongedBy, Tavern, subject: ActorId.ForNpc(Tom));
            state.Knowledge.Get(Bessa).Set(new Belief(wronged,
                new BeliefSource(BeliefSourceKind.Seen, originEventId: theft.Id), 80, state.Clock));

            ConversationContext context = RumorTrustFilter.ForConversation(
                state, Bessa, Bram, Tavern, 100, 0, 0);
            ConversationResult result = RumorExchange.Share(state, context, new WrongedPolicy(), new NoDistortion());

            Assert.That(result.AdoptedBelief, Is.Null);
            Assert.That(state.Knowledge.GetMemories(Bram).Query(), Is.Empty);
        }

        [Test]
        public void WeaklyBelievedRumorAdoptedButNotRemembered()
        {
            WorldState state = StateWith(); // No relationship: neutral 50 trust.
            state.Knowledge.Register(Bessa);
            state.Knowledge.Register(Bram);
            WorldEvent theft = state.Events.Append(state.Clock, Tavern, WorldEventType.Theft,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Bessa) }, EventVisibility.Normal, Apple, 6);
            var wronged = new BeliefClaim(BeliefClaimKind.WrongedBy, Tavern, subject: ActorId.ForNpc(Tom));
            state.Knowledge.Get(Bessa).Set(new Belief(wronged,
                new BeliefSource(BeliefSourceKind.Seen, originEventId: theft.Id), 80, state.Clock));

            ConversationContext context = RumorTrustFilter.ForConversation(
                state, Bessa, Bram, Tavern, 100, 0, 0);
            ConversationResult result = RumorExchange.Share(state, context, new WrongedPolicy(), new NoDistortion());

            Assert.That(result.AdoptedBelief.Confidence, Is.EqualTo(40), "80 x 50 / 100: adopted, but weakly.");
            Assert.That(state.Knowledge.GetMemories(Bram).Query(), Is.Empty,
                "Below the high-confidence bar, a rumor leaves no attributed memory.");
        }

        private static WorldState StateWith(params (NpcId from, NpcId to, int trust, int affection)[] pairs)
        {
            var state = new WorldState(42, new GameTime(0));
            foreach (NpcId npc in pairs.SelectMany(pair => new[] { pair.from, pair.to }).Distinct())
                state.Knowledge.Register(npc);
            state.Knowledge.InitializeRelationships(pairs.Select(pair =>
                new Relationship(pair.from, pair.to, pair.trust, pair.affection, "test")));
            return state;
        }

        private static void SpeakSeenTheft(WorldState state, NpcId speaker, WorldEvent theft, int confidence)
        {
            var claim = new BeliefClaim(BeliefClaimKind.TheftObserved, theft.Location,
                theft.ItemType, theft.Actor, theft.Quantity);
            state.Knowledge.Get(speaker).Set(new Belief(claim,
                new BeliefSource(BeliefSourceKind.Seen, originEventId: theft.Id), confidence, state.Clock));
        }

        private sealed class AnyPolicy : IRumorSelectionPolicy
        {
            public bool IsEligible(Belief belief, ConversationContext context) => true;
            public int Salience(Belief belief, ConversationContext context) => belief.Confidence;
        }

        private sealed class WrongedPolicy : IRumorSelectionPolicy
        {
            public bool IsEligible(Belief belief, ConversationContext context) =>
                belief.Claim.Kind == BeliefClaimKind.WrongedBy;
            public int Salience(Belief belief, ConversationContext context) => belief.Confidence;
        }

        private sealed class NoDistortion : IRumorDistortionPolicy
        {
            public BeliefClaim Distort(BeliefClaim claim, ConversationContext context) => claim;
        }
    }
}
