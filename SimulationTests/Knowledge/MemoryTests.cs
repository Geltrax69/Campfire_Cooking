using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>Verifies approved memory loading, reinforcement, deterministic decay and truth retention.</summary>
    public sealed class MemoryTests
    {
        private const long Day = 1440;
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Lida = new NpcId("npc_lida");
        private static readonly LocationId Shop = new LocationId("loc_shop");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        [Test]
        public void LoadsApprovedRulesAsImmutableAndLeavesStreamOpen()
        {
            using var stream = File.OpenRead(ContentPath());
            var rules = MemoryRules.Load(stream);
            Assert.That(rules.Levels.Select(level => (level.Id, level.MinimumImportance,
                level.MaximumImportance, level.RetentionDays)), Is.EqualTo(new[] {
                    ("memory_trivia", 0, 20, 3), ("memory_minor", 20, 40, 7),
                    ("memory_notable", 40, 60, 30), ("memory_important", 60, 80, 90),
                    ("memory_major", 80, 100, 720) }));
            Assert.That((rules.TriviaMinorPerDay, rules.NotablePer3Days, rules.ImportantPer10Days),
                Is.EqualTo((1, 1, 1)));
            Assert.That(rules.MajorNote, Is.Not.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<MemoryLevel>)rules.Levels).Clear());
            Assert.That(stream.CanRead, Is.True);
            stream.Position = 0;
            Assert.That(stream.ReadByte(), Is.EqualTo((int)'{'));
        }

        [TestCase("2", ValidLevels, ValidDecay)]
        [TestCase("1", "[]", ValidDecay)]
        [TestCase("1", "[{\"id\":\"memory_trivia\",\"min\":0,\"max\":100,\"retentionDays\":3}]", ValidDecay)]
        [TestCase("1", ValidLevels, "{\"triviaMinorPerDay\":0,\"notablePer3Days\":1,\"importantPer10Days\":1,\"majorNote\":\"note\"}")]
        public void RejectsWrongVersionMalformedCoverageAndInvalidDecay(string version, string levels, string decay)
        {
            string json = "{\"version\":" + version + ",\"memory\":{\"levels\":" + levels
                + ",\"decay\":" + decay + "},\"future\":true}";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Assert.Catch(() => MemoryRules.Load(stream));
            Assert.That(stream.CanRead, Is.True);
        }

        [Test]
        public void StoreRefreshesExplicitlyRejectsBackdatingAndOrdersClaims()
        {
            var store = new MemoryStore(Mira);
            var laterClaim = Claim(9);
            var falseClaim = Claim(1);
            store.Remember(laterClaim, 30, new GameTime(10), new WorldEventId(2));
            store.Remember(falseClaim, 10, new GameTime(5), new WorldEventId(1));
            Assert.That(store.Query().Select(memory => memory.Claim), Is.EqualTo(new[] { falseClaim, laterClaim }));
            Assert.That(store.Query()[0].Strength, Is.EqualTo(10), "Truth is not consulted when storing a claim.");

            store.Remember(falseClaim, 20, new GameTime(20), new WorldEventId(3));
            var refreshed = store.Query()[0];
            Assert.That((refreshed.Importance, refreshed.Strength, refreshed.FormedAt.TotalMinutes,
                refreshed.LastReinforcedAt.TotalMinutes, refreshed.OriginEventId.Value.Value),
                Is.EqualTo((20, 20, 5L, 20L, 3L)));
            Assert.Throws<InvalidOperationException>(() =>
                store.Remember(falseClaim, 100, new GameTime(19), new WorldEventId(4)));
            Assert.That(store.Query()[0], Is.SameAs(refreshed));
        }

        [Test]
        public void ApprovedCadencesDecayWholeDaysAndMajorOnlyExpiresAtRetention()
        {
            var rules = ApprovedRules();
            var store = new MemoryStore(Mira);
            store.Remember(Claim(1), 10, default);
            store.Remember(Claim(2), 30, default);
            store.Remember(Claim(3), 50, default);
            store.Remember(Claim(4), 70, default);
            store.Remember(Claim(5), 90, default);

            store.Decay(new GameTime(Day - 1), rules);
            Assert.That(store.Query().Select(memory => memory.Strength), Is.EqualTo(new[] { 10, 30, 50, 70, 90 }));
            store.Decay(new GameTime(Day), rules);
            Assert.That(store.Query().Select(memory => memory.Strength), Is.EqualTo(new[] { 9, 29, 50, 70, 90 }));
            store.Decay(new GameTime(Day), rules);
            Assert.That(store.Query().Select(memory => memory.Strength), Is.EqualTo(new[] { 9, 29, 50, 70, 90 }));
            store.Decay(new GameTime(3 * Day), rules);
            Assert.That(store.Query().Select(memory => memory.Strength), Is.EqualTo(new[] { 27, 49, 70, 90 }));
            store.Decay(new GameTime(10 * Day), rules);
            Assert.That(store.Query().Select(memory => memory.Strength), Is.EqualTo(new[] { 47, 69, 90 }));
            store.Decay(new GameTime(719 * Day), rules);
            Assert.That(store.Query().Single().Strength, Is.EqualTo(90));
            store.Decay(new GameTime(720 * Day), rules);
            Assert.That(store.Query(), Is.Empty);
        }

        [Test]
        public void ReinforcementResetsClockWithoutAmplification()
        {
            var store = new MemoryStore(Mira);
            var claim = Claim(1);
            store.Remember(claim, 10, default);
            store.Decay(new GameTime(2 * Day), ApprovedRules());
            Assert.That(store.Query().Single().Strength, Is.EqualTo(8));
            store.Remember(claim, 20, new GameTime(2 * Day));
            store.Decay(new GameTime(3 * Day - 1), ApprovedRules());
            Assert.That(store.Query().Single().Strength, Is.EqualTo(20));
            store.Decay(new GameTime(3 * Day), ApprovedRules());
            Assert.That(store.Query().Single().Strength, Is.EqualTo(19));
        }

        [Test]
        public void KnowledgeRegistersIsolatedMemoryStoresInNpcOrder()
        {
            var knowledge = new KnowledgeState();
            knowledge.Register(Mira);
            knowledge.Register(Lida);
            knowledge.GetMemories(Lida).Remember(Claim(1), 10, default);
            Assert.That(knowledge.MemoryStores.Select(store => store.Owner), Is.EqualTo(new[] { Lida, Mira }));
            Assert.That(knowledge.GetMemories(Mira).Count, Is.Zero);
            Assert.That(knowledge.GetMemories(Lida).Count, Is.EqualTo(1));
        }

        [Test]
        public void MemorySystemUsesMemoryPhaseAndDropsExpiredMemoryOnlyReferences()
        {
            var state = new WorldState(42, new GameTime(Day - 1));
            var beliefs = state.Knowledge.Register(Mira);
            beliefs.Set(new Belief(Claim(2), new BeliefSource(BeliefSourceKind.Seen,
                originEventId: new WorldEventId(2)), 50, default));
            state.Knowledge.GetMemories(Mira).Remember(Claim(1), 1, default, new WorldEventId(1));
            Assert.That(state.Knowledge.RetainedEventIds(),
                Is.EqualTo(new[] { new WorldEventId(1), new WorldEventId(2) }));

            var system = new MemorySystem(ApprovedRules());
            Assert.That(system.Phase, Is.EqualTo(SimulationPhase.Memory));
            var world = new World(state);
            world.RegisterSystem(system);
            world.Tick();
            Assert.That(state.Knowledge.GetMemories(Mira).Query(), Is.Empty);
            Assert.That(state.Knowledge.RetainedEventIds(), Is.EqualTo(new[] { new WorldEventId(2) }));
        }

        [Test]
        public void ExtremeElapsedTimeSaturatesDecayAndSystemRejectsNulls()
        {
            var store = new MemoryStore(Mira);
            store.Remember(Claim(1), 10, default);
            Assert.DoesNotThrow(() => store.Decay(new GameTime(long.MaxValue), ApprovedRules()));
            Assert.That(store.Query(), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => new MemorySystem(null));
            Assert.Throws<ArgumentNullException>(() => new MemorySystem(ApprovedRules()).Tick(null));
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
            var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/social/social.json"))) root = root.Parent;
            Assert.That(root, Is.Not.Null, "Find repository content above test output.");
            return Path.Combine(root.FullName, "Content/social/social.json");
        }

        private const string ValidLevels = "[{\"id\":\"memory_trivia\",\"min\":0,\"max\":20,\"retentionDays\":3},"
            + "{\"id\":\"memory_minor\",\"min\":20,\"max\":40,\"retentionDays\":7},"
            + "{\"id\":\"memory_notable\",\"min\":40,\"max\":60,\"retentionDays\":30},"
            + "{\"id\":\"memory_important\",\"min\":60,\"max\":80,\"retentionDays\":90},"
            + "{\"id\":\"memory_major\",\"min\":80,\"max\":100,\"retentionDays\":720}]";
        private const string ValidDecay = "{\"triviaMinorPerDay\":1,\"notablePer3Days\":1,"
            + "\"importantPer10Days\":1,\"majorNote\":\"note\"}";
    }
}
