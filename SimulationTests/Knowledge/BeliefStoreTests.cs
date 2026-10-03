using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>Protects per-NPC knowledge from invalid provenance and accidental access to truth.</summary>
    public sealed class BeliefStoreTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Lida = new NpcId("npc_lida");
        private static readonly LocationId Shop = new LocationId("loc_shop");
        private static readonly LocationId Square = new LocationId("loc_square");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        [Test]
        public void ClaimsValidateKindSpecificFieldsAndKeepUnknownThiefUnknown()
        {
            Assert.Throws<ArgumentException>(() => new BeliefClaim(BeliefClaimKind.StockAvailable, Shop));
            Assert.Throws<ArgumentException>(() => new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple));
            Assert.Throws<ArgumentException>(() => new BeliefClaim(BeliefClaimKind.Presence, Shop));
            Assert.Throws<ArgumentException>(() => new BeliefClaim(BeliefClaimKind.Presence, Shop, subject: default(ActorId)));
            Assert.Throws<ArgumentException>(() => new BeliefClaim(BeliefClaimKind.Presence, Shop, Apple, ActorId.Player));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, quantity: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeliefClaim((BeliefClaimKind)99, Shop));

            var unknown = new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple, quantity: 6);
            Assert.That(unknown.Subject, Is.Null);
            Assert.That(unknown.Subject, Is.Not.EqualTo(ActorId.Player));
            Assert.That(unknown.ItemType, Is.EqualTo(Apple));
            Assert.That(unknown.Quantity, Is.EqualTo(6));
        }

        [Test]
        public void ClaimsHaveDeterministicValueEqualityAndDistinctDetails()
        {
            var claim = new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple, quantity: 6);
            Assert.That(claim, Is.EqualTo(new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple, quantity: 6)));
            Assert.That(claim.GetHashCode(), Is.EqualTo(new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple, quantity: 6).GetHashCode()));
            Assert.That(claim, Is.Not.EqualTo(new BeliefClaim(BeliefClaimKind.StockMissing, Shop, new ItemTypeId("item_bread"), quantity: 6)));
            Assert.That(claim, Is.Not.EqualTo(new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple, quantity: 5)));
            Assert.That(new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple, ActorId.Player, 6),
                Is.Not.EqualTo(new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple, ActorId.ForNpc(Lida), 6)));
        }

        [Test]
        public void SourcesValidateAndDefensivelyCopyChains()
        {
            Assert.Throws<ArgumentException>(() => new BeliefSource(BeliefSourceKind.ToldBy));
            Assert.Throws<ArgumentException>(() => new BeliefSource(BeliefSourceKind.Seen, Lida));
            Assert.Throws<ArgumentException>(() => new BeliefSource(BeliefSourceKind.Inferred, originEventId: default(WorldEventId)));
            Assert.Throws<ArgumentException>(() => new BeliefSource(BeliefSourceKind.ToldBy, Lida, sourceChain: new[] { Lida, default(NpcId) }));
            Assert.Throws<ArgumentException>(() => new BeliefSource(BeliefSourceKind.ToldBy, Lida, sourceChain: new[] { Lida, Lida }));

            var chain = new[] { Mira, Lida };
            var source = new BeliefSource(BeliefSourceKind.ToldBy, Lida, new WorldEventId(3), chain);
            chain[0] = new NpcId("npc_bram");
            Assert.That(source.SourceChain, Is.EqualTo(new[] { Mira, Lida }));
            Assert.Throws<NotSupportedException>(() => ((IList<NpcId>)source.SourceChain).Clear());
        }

        [Test]
        public void BeliefsValidateConfidenceAndExposeImmutableValues()
        {
            var claim = new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple, quantity: 6);
            var source = new BeliefSource(BeliefSourceKind.Inferred, originEventId: new WorldEventId(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Belief(claim, source, -1, default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Belief(claim, source, 101, default));
            var belief = new Belief(claim, source, 90, new GameTime(1140));
            Assert.That(belief.Claim, Is.EqualTo(claim));
            Assert.That(belief.Source, Is.SameAs(source));
            Assert.That(belief.Confidence, Is.EqualTo(90));
            Assert.That(belief.LearnedAt, Is.EqualTo(new GameTime(1140)));
        }

        [Test]
        public void SetUpdatesSameClaimWithoutAmplificationAndRejectsBackdatingAtomically()
        {
            var store = new BeliefStore(Mira);
            var claim = new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple, quantity: 6);
            var first = new Belief(claim, new BeliefSource(BeliefSourceKind.Inferred), 90, new GameTime(10));
            store.Set(first);
            var replacement = new Belief(claim, new BeliefSource(BeliefSourceKind.ToldBy, Lida), 35, new GameTime(12));
            store.Set(replacement);
            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.Query(), Is.EqualTo(new[] { replacement }));

            var backdated = new Belief(claim, new BeliefSource(BeliefSourceKind.Seen), 100, new GameTime(11));
            Assert.Throws<InvalidOperationException>(() => store.Set(backdated));
            Assert.That(store.Query(), Is.EqualTo(new[] { replacement }));
        }

        [Test]
        public void QueriesFilterAndReturnStableClaimOrder()
        {
            var store = new BeliefStore(Mira);
            var theft = new Belief(new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple, quantity: 6),
                new BeliefSource(BeliefSourceKind.Seen), 70, new GameTime(5));
            var presence = new Belief(new BeliefClaim(BeliefClaimKind.Presence, Square, subject: ActorId.Player),
                new BeliefSource(BeliefSourceKind.Seen), 80, new GameTime(4));
            var stock = new Belief(new BeliefClaim(BeliefClaimKind.StockAvailable, Shop, Apple, quantity: 10),
                new BeliefSource(BeliefSourceKind.Inferred), 90, new GameTime(6));
            store.Set(stock);
            store.Set(presence);
            store.Set(theft);

            Assert.That(store.Query(), Is.EqualTo(new[] { stock, theft, presence }));
            Assert.That(store.Query(kind: BeliefClaimKind.TheftObserved), Is.EqualTo(new[] { theft }));
            Assert.That(store.Query(location: Shop, itemType: Apple), Is.EqualTo(new[] { stock, theft }));
            Assert.That(store.Query(subject: ActorId.Player), Is.EqualTo(new[] { presence }));
            Assert.That(store.Query(quantity: 6), Is.EqualTo(new[] { theft }));
            Assert.Throws<NotSupportedException>(() => ((IList<Belief>)store.Query()).Clear());
        }

        [Test]
        public void KnowledgeKeepsNpcStoresIsolatedAndEnumeratesByNpcId()
        {
            var knowledge = new KnowledgeState();
            var lida = knowledge.Register(Lida);
            var mira = knowledge.Register(Mira);
            Assert.Throws<InvalidOperationException>(() => knowledge.Register(Mira));
            var claim = new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple, ActorId.Player, 6);
            lida.Set(new Belief(claim, new BeliefSource(BeliefSourceKind.Seen), 70, default));

            Assert.That(knowledge.Stores, Is.EqualTo(new[] { lida, mira }));
            Assert.That(knowledge.Get(Lida).Count, Is.EqualTo(1));
            Assert.That(knowledge.Get(Mira).Count, Is.Zero);
            Assert.That(knowledge.TryGet(new NpcId("npc_unknown"), out _), Is.False);
        }

        [Test]
        public void RetainedEventIdsAreSortedDistinctAndTruthDoesNotCreateKnowledge()
        {
            var world = new WorldState(42);
            var store = world.Knowledge.Register(Mira);
            world.Events.Append(default, Shop, WorldEventType.Theft, ActorId.Player, itemType: Apple, quantity: 6);
            Assert.That(store.Query(), Is.Empty);
            Assert.That(world.Knowledge.RetainedEventIds(), Is.Empty);

            var claimA = new BeliefClaim(BeliefClaimKind.StockMissing, Shop, Apple, quantity: 6);
            var claimB = new BeliefClaim(BeliefClaimKind.Presence, Square, subject: ActorId.Player);
            store.Set(new Belief(claimA, new BeliefSource(BeliefSourceKind.Inferred, originEventId: new WorldEventId(2)), 90, default));
            store.Set(new Belief(claimB, new BeliefSource(BeliefSourceKind.ToldBy, Lida, new WorldEventId(1)), 40, default));
            var other = world.Knowledge.Register(Lida);
            other.Set(new Belief(claimA, new BeliefSource(BeliefSourceKind.Seen, originEventId: new WorldEventId(2)), 50, default));
            Assert.That(world.Knowledge.RetainedEventIds(), Is.EqualTo(new[] { new WorldEventId(1), new WorldEventId(2) }));
        }
    }
}
