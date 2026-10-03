using System;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>Verifies traceable, deterministic rumor exchange without knowledge teleportation.</summary>
    public sealed class RumorExchangeTests
    {
        private static readonly NpcId Lida = new NpcId("npc_lida");
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Bessa = new NpcId("npc_bessa");
        private static readonly NpcId Bram = new NpcId("npc_bram");
        private static readonly LocationId Tavern = new LocationId("loc_tavern");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        [Test]
        public void TrustedListenerReceivesLowerConfidenceBeliefWithCompleteSourceChain()
        {
            var state = State(42, Bessa, Bram);
            WorldEvent theft = state.Events.Append(default, Tavern, WorldEventType.Theft, ActorId.Player,
                itemType: Apple, quantity: 6);
            var claim = TheftClaim(6, ActorId.Player);
            state.Knowledge.Get(Bessa).Set(new Belief(claim,
                new BeliefSource(BeliefSourceKind.ToldBy, Mira, theft.Id, new[] { Lida, Mira }),
                80, state.Clock));
            var context = new ConversationContext(Bessa, Bram, Tavern, 80, 75, 5, 0);

            ConversationResult result = RumorExchange.Share(state, context, new AllEqualPolicy(), new VaguePolicy());

            Belief heard = state.Knowledge.Get(Bram).Query().Single();
            Assert.That(heard.Claim, Is.SameAs(claim));
            Assert.That(heard.Confidence, Is.EqualTo(43));
            Assert.That(heard.Source.Kind, Is.EqualTo(BeliefSourceKind.ToldBy));
            Assert.That(heard.Source.Speaker, Is.EqualTo(Bessa));
            Assert.That(heard.Source.OriginEventId, Is.EqualTo(theft.Id));
            Assert.That(heard.Source.SourceChain, Is.EqualTo(new[] { Lida, Mira, Bessa }));
            Assert.That(heard.LearnedAt, Is.EqualTo(state.Clock));
            Assert.That(result.AdoptedBelief, Is.SameAs(heard));
            Assert.That((result.Conversation.Type, result.Conversation.Actor, result.Conversation.Location,
                result.Conversation.Targets.Single()),
                Is.EqualTo((WorldEventType.Conversation, (ActorId?)ActorId.ForNpc(Bessa), Tavern,
                    ActorId.ForNpc(Bram))));
        }

        [Test]
        public void DistrustedListenerRejectsRumorAndBystanderLearnsNothing()
        {
            var state = State(42, Bessa, Bram, Mira);
            AddSeenBelief(state, Bessa, TheftClaim(6), 70);
            var context = new ConversationContext(Bessa, Bram, Tavern, 20, 20, 10, 0);

            ConversationResult result = RumorExchange.Share(state, context, new AllEqualPolicy(), new VaguePolicy());

            Assert.That(result.Conversation, Is.Not.Null);
            Assert.That(result.AdoptedBelief, Is.Null);
            Assert.That(state.Knowledge.Get(Bram).Query(), Is.Empty);
            Assert.That(state.Knowledge.Get(Mira).Query(), Is.Empty);
            Assert.That(result.Conversation.Targets, Is.EqualTo(new[] { ActorId.ForNpc(Bram) }));
        }

        [TestCase(0UL, true)]
        [TestCase(1UL, false)]
        public void FixedSeedReproducesMutationAndStableBeliefSelection(ulong seed, bool mutated)
        {
            var first = ShareOne(seed);
            var second = ShareOne(seed);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.mutated, Is.EqualTo(mutated));
            Assert.That(first.selectedQuantity, Is.EqualTo(1));
            Assert.That(first.heardQuantity, Is.EqualTo(mutated ? null : (int?)1));
        }

        [Test]
        public void CallerEligibilityAndSalienceChooseFromStableSpeakerBeliefs()
        {
            var state = State(3, Bessa, Bram);
            AddSeenBelief(state, Bessa, TheftClaim(3), 80);
            AddSeenBelief(state, Bessa, TheftClaim(1), 80);
            AddSeenBelief(state, Bessa, TheftClaim(2), 80);
            var context = new ConversationContext(Bessa, Bram, Tavern, 100, 100, 1, 0);

            ConversationResult result = RumorExchange.Share(state, context,
                new QuantityPolicy(), new VaguePolicy());

            Assert.That(result.SelectedBelief.Claim.Quantity, Is.EqualTo(2));
            Assert.That(result.AdoptedBelief.Claim.Quantity, Is.EqualTo(2));
        }

        [Test]
        public void DistortionMayRemoveDetailButCannotInventActorOrFact()
        {
            var state = State(0, Bessa, Bram);
            AddSeenBelief(state, Bessa, TheftClaim(6, ActorId.Player), 80);
            var context = new ConversationContext(Bessa, Bram, Tavern, 100, 100, 1, 100);

            ConversationResult vague = RumorExchange.Share(state, context, new AllEqualPolicy(), new VaguePolicy());

            Assert.That((vague.AdoptedBelief.Claim.Kind, vague.AdoptedBelief.Claim.Location,
                vague.AdoptedBelief.Claim.ItemType, vague.AdoptedBelief.Claim.Subject,
                vague.AdoptedBelief.Claim.Quantity),
                Is.EqualTo((BeliefClaimKind.TheftObserved, Tavern, (ItemTypeId?)null,
                    (ActorId?)null, (int?)null)));

            var rejected = State(0, Bessa, Bram);
            AddSeenBelief(rejected, Bessa, TheftClaim(6), 80);
            Assert.Throws<InvalidOperationException>(() => RumorExchange.Share(rejected, context,
                new AllEqualPolicy(), new InventActorPolicy()));
            Assert.That(rejected.Knowledge.Get(Bram).Query(), Is.Empty);
            Assert.That(rejected.Events.Query(type: WorldEventType.Conversation), Is.Empty);
        }

        [Test]
        public void CyclesAreSkippedAndRepeatedSharingDoesNotAmplifyConfidence()
        {
            var cyclic = State(8, Bessa, Bram);
            cyclic.Knowledge.Get(Bessa).Set(new Belief(TheftClaim(6),
                new BeliefSource(BeliefSourceKind.ToldBy, Bram, sourceChain: new[] { Bram }),
                50, cyclic.Clock));
            var context = new ConversationContext(Bessa, Bram, Tavern, 100, 100, 10, 0);
            ConversationResult blocked = RumorExchange.Share(cyclic, context,
                new AllEqualPolicy(), new VaguePolicy());
            Assert.That(blocked.Conversation, Is.Null);
            Assert.That(cyclic.Knowledge.Get(Bram).Query(), Is.Empty);

            var repeated = State(8, Bessa, Bram);
            AddSeenBelief(repeated, Bessa, TheftClaim(6), 80);
            ConversationResult first = RumorExchange.Share(repeated, context,
                new AllEqualPolicy(), new VaguePolicy());
            Belief learned = repeated.Knowledge.Get(Bram).Query().Single();
            ConversationResult second = RumorExchange.Share(repeated, context,
                new AllEqualPolicy(), new VaguePolicy());
            Assert.That(learned.Confidence, Is.EqualTo(70));
            Assert.That(repeated.Knowledge.Get(Bram).Query().Single(), Is.SameAs(learned));
            Assert.That(first.AdoptedBelief, Is.SameAs(learned));
            Assert.That(second.AdoptedBelief, Is.Null);

            var returnContext = new ConversationContext(Bram, Bessa, Tavern, 100, 100, 0, 0);
            ConversationResult returned = RumorExchange.Share(repeated, returnContext,
                new AllEqualPolicy(), new VaguePolicy());
            Assert.That(returned.Conversation, Is.Null);
            Assert.That(repeated.Knowledge.Get(Bessa).Query().Single().Confidence, Is.EqualTo(80));
        }

        [Test]
        public void InvalidContextCollaboratorsAndMissingStoresAreRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new ConversationContext(Bessa, Bessa, Tavern, 1, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ConversationContext(Bessa, Bram, Tavern, 101, 1, 1, 1));
            var state = State(0, Bessa);
            AddSeenBelief(state, Bessa, TheftClaim(1), 50);
            var context = new ConversationContext(Bessa, Bram, Tavern, 100, 100, 0, 0);
            Assert.Throws<InvalidOperationException>(() =>
                RumorExchange.Share(state, context, new AllEqualPolicy(), new VaguePolicy()));
            Assert.Throws<ArgumentNullException>(() =>
                RumorExchange.Share(null, context, new AllEqualPolicy(), new VaguePolicy()));
            Assert.Throws<ArgumentNullException>(() =>
                RumorExchange.Share(state, context, null, new VaguePolicy()));
            Assert.Throws<ArgumentNullException>(() =>
                RumorExchange.Share(state, context, new AllEqualPolicy(), null));
        }

        private static (bool mutated, int? selectedQuantity, int? heardQuantity, ulong rng) ShareOne(ulong seed)
        {
            var state = State(seed, Bessa, Bram);
            AddSeenBelief(state, Bessa, TheftClaim(2), 80);
            AddSeenBelief(state, Bessa, TheftClaim(1), 80);
            var context = new ConversationContext(Bessa, Bram, Tavern, 100, 100, 1, 50);
            ConversationResult result = RumorExchange.Share(state, context,
                new AllEqualPolicy(), new VaguePolicy());
            return (result.WasMutated, result.SelectedBelief.Claim.Quantity,
                result.AdoptedBelief.Claim.Quantity, state.Rng.State);
        }

        private static WorldState State(ulong seed, params NpcId[] npcs)
        {
            var state = new WorldState(seed, new GameTime(5));
            foreach (NpcId npc in npcs) state.Knowledge.Register(npc);
            return state;
        }

        private static void AddSeenBelief(WorldState state, NpcId npc, BeliefClaim claim, int confidence)
        {
            WorldEvent truth = state.Events.Append(state.Clock, Tavern, WorldEventType.Theft,
                itemType: Apple, quantity: claim.Quantity);
            state.Knowledge.Get(npc).Set(new Belief(claim,
                new BeliefSource(BeliefSourceKind.Seen, originEventId: truth.Id), confidence, state.Clock));
        }

        private static BeliefClaim TheftClaim(int quantity, ActorId? actor = null) =>
            new BeliefClaim(BeliefClaimKind.TheftObserved, Tavern, Apple, actor, quantity);

        private sealed class AllEqualPolicy : IRumorSelectionPolicy
        {
            public bool IsEligible(Belief belief, ConversationContext context) => true;
            public int Salience(Belief belief, ConversationContext context) => 1;
        }

        private sealed class VaguePolicy : IRumorDistortionPolicy
        {
            public BeliefClaim Distort(BeliefClaim claim, ConversationContext context) =>
                new BeliefClaim(claim.Kind, claim.Location);
        }

        private sealed class QuantityPolicy : IRumorSelectionPolicy
        {
            public bool IsEligible(Belief belief, ConversationContext context) => belief.Claim.Quantity >= 2;
            public int Salience(Belief belief, ConversationContext context) =>
                belief.Claim.Quantity == 2 ? 10 : 5;
        }

        private sealed class InventActorPolicy : IRumorDistortionPolicy
        {
            public BeliefClaim Distort(BeliefClaim claim, ConversationContext context) =>
                new BeliefClaim(claim.Kind, claim.Location, subject: ActorId.ForNpc(Mira));
        }
    }
}
