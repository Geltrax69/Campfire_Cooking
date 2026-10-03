using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>Verifies the assembly-internal knowledge restore contracts used by Persistence:
    /// exact memory records (including partially decayed strength) and the perception event cursor.</summary>
    public sealed class KnowledgeRestoreTests
    {
        private const long Day = 1440;
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Awake = new NpcId("npc_awake");
        private static readonly LocationId Shop = new LocationId("loc_shop");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        [Test]
        public void PartiallyDecayedMemoriesRoundTripAndKeepDecaying()
        {
            var rules = ApprovedRules();
            var source = new MemoryStore(Mira);
            source.Remember(Claim(1), 30, new GameTime(0), new WorldEventId(1));
            source.Remember(Claim(2), 50, new GameTime(0), new WorldEventId(2));
            source.Decay(new GameTime(2 * Day), rules);
            Assert.That(source.Query().Select(memory => memory.Strength), Is.EqualTo(new[] { 28, 50 }));

            var restored = new MemoryStore(Mira);
            restored.Restore(source.Query());

            // Every field of every record matches, in deterministic claim order.
            Assert.That(restored.Query().Select(memory => memory.Claim),
                Is.EqualTo(source.Query().Select(memory => memory.Claim)));
            for (int i = 0; i < source.Query().Count; i++)
            {
                Memory expected = source.Query()[i];
                Memory actual = restored.Query()[i];
                Assert.That(actual, Is.SameAs(expected), "Restore must install the exact records, not copies.");
                Assert.That((actual.Importance, actual.FormedAt.TotalMinutes, actual.LastReinforcedAt.TotalMinutes,
                    actual.Strength, actual.OriginEventId),
                    Is.EqualTo((expected.Importance, expected.FormedAt.TotalMinutes,
                        expected.LastReinforcedAt.TotalMinutes, expected.Strength, expected.OriginEventId)));
            }
            Assert.That(restored.Query()[0].Strength, Is.EqualTo(28),
                "Restore must preserve decayed strength instead of resetting it to importance.");

            // Continued decay is identical on both stores.
            source.Decay(new GameTime(4 * Day), rules);
            restored.Decay(new GameTime(4 * Day), rules);
            Assert.That(restored.Query().Select(memory => memory.Strength),
                Is.EqualTo(source.Query().Select(memory => memory.Strength)));
            Assert.That(restored.Query().Select(memory => memory.Strength), Is.EqualTo(new[] { 26, 49 }));
        }

        [Test]
        public void DirectlyConstructedDecayedRecordRestoresExactly()
        {
            var decayed = new Memory(Claim(7), 70, new GameTime(10), new GameTime(20), 63, new WorldEventId(5));
            var store = new MemoryStore(Mira);
            store.Restore(new[] { decayed });

            Memory actual = store.Query().Single();
            Assert.That(actual, Is.SameAs(decayed));
            Assert.That((actual.Claim, actual.Importance, actual.FormedAt.TotalMinutes,
                actual.LastReinforcedAt.TotalMinutes, actual.Strength, actual.OriginEventId.Value.Value),
                Is.EqualTo((Claim(7), 70, 10L, 20L, 63, 5L)));
        }

        [Test]
        public void MalformedRecordsCannotBeConstructedForRestore()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Memory(Claim(1), 30, new GameTime(0), new GameTime(0), 31, null));
            Assert.Throws<ArgumentException>(() =>
                new Memory(Claim(1), 30, new GameTime(10), new GameTime(5), 30, null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Memory(Claim(1), 101, new GameTime(0), new GameTime(0), 100, null));
        }

        [Test]
        public void RestoreRejectsInvalidBatchesAtomically()
        {
            var store = new MemoryStore(Mira);
            store.Remember(Claim(1), 10, default, new WorldEventId(1));
            Memory kept = store.Query().Single();

            var duplicate = new Memory(Claim(2), 10, default, default, 10, null);
            Assert.Throws<ArgumentException>(() => store.Restore(new[] { duplicate, duplicate }),
                "Duplicate claims must be rejected.");
            Assert.Throws<ArgumentException>(() => store.Restore(new Memory[] { null }),
                "Null records must be rejected.");
            Assert.Throws<ArgumentNullException>(() => store.Restore(null));

            Assert.That(store.Query().Single(), Is.SameAs(kept),
                "A failed restore must leave the store unchanged.");
            Assert.That(store.Count, Is.EqualTo(1));
        }

        [Test]
        public void RestoreReplacesPriorContentsInClaimOrder()
        {
            var store = new MemoryStore(Mira);
            store.Remember(Claim(1), 10, default);
            var later = new Memory(Claim(9), 40, new GameTime(3), new GameTime(3), 40, null);
            var earlier = new Memory(Claim(2), 20, new GameTime(1), new GameTime(2), 20, new WorldEventId(9));

            store.Restore(new[] { later, earlier });

            Assert.That(store.Query().Select(memory => memory.Claim),
                Is.EqualTo(new[] { Claim(2), Claim(9) }));
            Assert.That(store.Query().Select(memory => memory.Strength), Is.EqualTo(new[] { 20, 40 }));
        }

        [Test]
        public void PerceptionCursorCaptureAndRestoreValidates()
        {
            var knowledge = new KnowledgeState();
            Assert.That(knowledge.CapturePerceptionCursor(), Is.Zero);

            knowledge.RestorePerceptionCursor(7);
            Assert.That(knowledge.CapturePerceptionCursor(), Is.EqualTo(7));

            Assert.Throws<ArgumentOutOfRangeException>(() => knowledge.RestorePerceptionCursor(-1));
            Assert.That(knowledge.CapturePerceptionCursor(), Is.EqualTo(7),
                "A rejected restore must leave the cursor unchanged.");

            Assert.DoesNotThrow(() => knowledge.RestorePerceptionCursor(0));
            Assert.That(knowledge.CapturePerceptionCursor(), Is.Zero);
        }

        [Test]
        public void PerceptionResumesAfterSavedCursorWithoutReplayingEvents()
        {
            var state = new WorldState(42, new GameTime(5));
            state.Knowledge.Register(Awake);
            var context = new TestContext(new[] { Awake });
            context.PresentAt[Awake] = Shop;
            var tuning = new TestTuning(100, 60);
            var system = new PerceptionSystem(context, tuning);
            state.Events.Append(default, Shop, WorldEventType.Theft, ActorId.Player, itemType: Apple, quantity: 6);

            system.Tick(state);
            long saved = state.Knowledge.CapturePerceptionCursor();
            Assert.That(saved, Is.EqualTo(1));
            Belief first = state.Knowledge.Get(Awake).Query().Single();

            // Simulate save/load: the saved cursor is restored, then a new event arrives later.
            state.Knowledge.RestorePerceptionCursor(saved);
            state.Clock = state.Clock.Advance(60);
            state.Events.Append(default, Shop, WorldEventType.Theft, ActorId.Player, itemType: Apple, quantity: 2);
            system.Tick(state);

            Assert.That(state.Knowledge.CapturePerceptionCursor(), Is.EqualTo(2));
            Assert.That(tuning.ChanceRequests, Has.Count.EqualTo(2),
                "Old events must not be re-perceived after the cursor is restored.");
            Assert.That(state.Knowledge.Get(Awake).Query().Single(belief => belief.Claim.Quantity == 6),
                Is.SameAs(first), "Replaying the old event would have rebuilt the old belief.");
            var expected = new SimRng(42);
            expected.NextInt(100);
            expected.NextInt(100);
            Assert.That(state.Rng.State, Is.EqualTo(expected.State));
        }

        private static BeliefClaim Claim(int quantity) =>
            new BeliefClaim(BeliefClaimKind.StockAvailable, Shop, Apple, quantity: quantity);

        private static MemoryRules ApprovedRules()
        {
            using var stream = File.OpenRead(ContentPath());
            return MemoryRules.Load(stream);
        }

        private static string ContentPath()
        {
            var root = new DirectoryInfo(NUnit.Framework.TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/social/social.json")))
                root = root.Parent;
            Assert.That(root, Is.Not.Null, "Find repository content above test output.");
            return Path.Combine(root.FullName, "Content/social/social.json");
        }

        private sealed class TestContext : IPerceptionContext
        {
            private readonly IReadOnlyList<NpcId> _candidates;
            public TestContext(IReadOnlyList<NpcId> candidates)
            {
                _candidates = candidates;
                Awake = new HashSet<NpcId>(candidates.Where(candidate => candidate.IsValid));
            }
            public Dictionary<NpcId, LocationId> PresentAt { get; } = new Dictionary<NpcId, LocationId>();
            public HashSet<NpcId> Awake { get; }
            public IEnumerable<NpcId> Candidates(WorldEvent worldEvent) => _candidates;
            public bool IsPresentAt(NpcId npc, LocationId location) =>
                PresentAt.TryGetValue(npc, out var actual) && actual == location;
            public bool IsAwake(NpcId npc) => Awake.Contains(npc);
        }

        private sealed class TestTuning : IPerceptionTuning
        {
            private readonly int _chance;
            private readonly int _confidence;
            public TestTuning(int chance, int confidence) { _chance = chance; _confidence = confidence; }
            public List<NpcId> ChanceRequests { get; } = new List<NpcId>();
            public int NoticeChancePercent(NpcId npc, WorldEvent worldEvent)
            {
                ChanceRequests.Add(npc);
                return _chance;
            }
            public int Confidence(NpcId npc, WorldEvent worldEvent) => _confidence;
        }
    }
}
