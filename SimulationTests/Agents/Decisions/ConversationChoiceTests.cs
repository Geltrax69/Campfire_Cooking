using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Verifies affection-weighted conversation-partner choice: friends seek each other
    /// out far more often than chance, but it is never a hard script — even the least
    /// liked candidate can be picked, and a lonely NPC talks to whoever is there.
    /// </summary>
    public sealed class ConversationChoiceTests
    {
        private static readonly NpcId Decider = new NpcId("npc_d");
        private static readonly NpcId Amy = new NpcId("npc_a");
        private static readonly NpcId Ben = new NpcId("npc_b");
        private static readonly NpcId Cal = new NpcId("npc_c");

        [Test]
        public void HighAffectionPartnerChosenMoreOftenThanChance()
        {
            // Weights: Amy 91, Ben 51, Cal 11 (total 153); chance would be 100 each of 300.
            WorldState state = StateWith(42,
                (Decider, Amy, 50, 90), (Decider, Ben, 50, 50), (Decider, Cal, 50, 10));
            var counts = new Dictionary<NpcId, int> { [Amy] = 0, [Ben] = 0, [Cal] = 0 };
            for (int i = 0; i < 300; i++)
                counts[ConversationChoice.ChoosePartner(state, Decider, new[] { Amy, Ben, Cal })]++;

            Assert.That(counts[Amy], Is.GreaterThan(counts[Ben]),
                "Amy (affection 90) is sought out more than Ben (affection 50).");
            Assert.That(counts[Ben], Is.GreaterThan(counts[Cal]),
                "Ben (affection 50) is sought out more than Cal (affection 10).");
            Assert.That(counts[Cal], Is.GreaterThan(0),
                "Even the least liked candidate can be picked: never a hard script.");
        }

        [Test]
        public void SameSeedAlwaysPicksTheSamePartners()
        {
            WorldState first = StateWith(7, (Decider, Amy, 50, 90), (Decider, Ben, 50, 50), (Decider, Cal, 50, 10));
            WorldState second = StateWith(7, (Decider, Amy, 50, 90), (Decider, Ben, 50, 50), (Decider, Cal, 50, 10));
            for (int i = 0; i < 50; i++)
                Assert.That(
                    ConversationChoice.ChoosePartner(first, Decider, new[] { Amy, Ben, Cal }),
                    Is.EqualTo(ConversationChoice.ChoosePartner(second, Decider, new[] { Amy, Ben, Cal })));
        }

        [Test]
        public void LonelyNpcTalksToWhoeverIsThere()
        {
            // No relationships: everyone is neutral 50, all weights equal at 51.
            WorldState state = StateWith(42);
            foreach (NpcId npc in new[] { Decider, Amy, Ben, Cal }) state.Knowledge.Register(npc);
            var counts = new Dictionary<NpcId, int> { [Amy] = 0, [Ben] = 0, [Cal] = 0 };
            for (int i = 0; i < 300; i++)
                counts[ConversationChoice.ChoosePartner(state, Decider, new[] { Amy, Ben, Cal })]++;

            foreach (NpcId candidate in new[] { Amy, Ben, Cal })
                Assert.That(counts[candidate], Is.GreaterThan(0).And.LessThan(200),
                    "With no friends, company is spread roughly evenly.");
        }

        [Test]
        public void SingleCandidateAlwaysChosen()
        {
            WorldState state = StateWith(42, (Decider, Amy, 50, 90));
            for (int i = 0; i < 10; i++)
                Assert.That(ConversationChoice.ChoosePartner(state, Decider, new[] { Amy }), Is.EqualTo(Amy));
        }

        [Test]
        public void DeciderIsNeverChosen()
        {
            WorldState state = StateWith(42, (Decider, Amy, 50, 90));
            for (int i = 0; i < 50; i++)
                Assert.That(ConversationChoice.ChoosePartner(state, Decider, new[] { Decider, Amy }),
                    Is.EqualTo(Amy));
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            WorldState state = StateWith(42);
            state.Knowledge.Register(Decider);
            state.Knowledge.Register(Amy);
            Assert.Throws<ArgumentNullException>(() => ConversationChoice.ChoosePartner(null, Decider, new[] { Amy }));
            Assert.Throws<ArgumentException>(() => ConversationChoice.ChoosePartner(state, default, new[] { Amy }));
            Assert.Throws<ArgumentNullException>(() => ConversationChoice.ChoosePartner(state, Decider, null));
            Assert.Throws<ArgumentException>(() => ConversationChoice.ChoosePartner(state, Decider, new NpcId[0]));
            Assert.Throws<ArgumentException>(() =>
                ConversationChoice.ChoosePartner(state, Decider, new[] { Amy, default }));
            Assert.Throws<InvalidOperationException>(() =>
                ConversationChoice.ChoosePartner(state, Decider, new[] { Decider }));
        }

        [TestCase(-5, 1)]
        [TestCase(0, 1)]
        [TestCase(50, 51)]
        [TestCase(100, 101)]
        [TestCase(150, 101)]
        public void WeightsAreOnePlusClampedAffection(int affection, int expected)
        {
            Assert.That(ConversationChoice.WeightFor(affection), Is.EqualTo(expected));
        }

        private static WorldState StateWith(
            ulong seed,
            params (NpcId from, NpcId to, int trust, int affection)[] pairs)
        {
            var state = new WorldState(seed, new GameTime(0));
            foreach (NpcId npc in pairs.SelectMany(pair => new[] { pair.from, pair.to }).Distinct())
                state.Knowledge.Register(npc);
            state.Knowledge.InitializeRelationships(pairs.Select(pair =>
                new Relationship(pair.from, pair.to, pair.trust, pair.affection, "test")));
            return state;
        }
    }
}
