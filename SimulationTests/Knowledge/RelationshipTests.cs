using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>
    /// Verifies directed NPC relationship state: content loading, validation,
    /// the stranger default for unknown pairs, and the capture/restore contract
    /// P2-12 persistence will use.
    /// </summary>
    public sealed class RelationshipTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira_holt");
        private static readonly NpcId Ralf = new NpcId("npc_ralf_hale");
        private static readonly NpcId Corvin = new NpcId("npc_corvin_alder");
        private static readonly NpcId Bessa = new NpcId("npc_bessa_marlowe");
        private static readonly NpcId Tom = new NpcId("npc_tom_fenn");

        [Test]
        public void MiraRelationshipsLoadFromRealContentWithExactValues()
        {
            string path = ContentPath();
            IReadOnlyList<Relationship> loaded;
            using (Stream stream = File.OpenRead(path))
                loaded = RelationshipContentLoader.Load(stream, KnownNpcIds(path));

            var mira = loaded.Where(relationship => relationship.From == Mira).ToList();
            Assert.That(mira, Has.Count.EqualTo(4), "Mira must keep exactly her 4 approved relationships.");

            AssertPair(mira, Ralf, 60, 55, "Lodger of two years; pays in meat, steady company");
            AssertPair(mira, Corvin, 70, 50, "Apple supplier; haggles hard but always delivers");
            AssertPair(mira, Bessa, 65, 70, "Evening confidante at the tavern");
            AssertPair(mira, Tom, 40, 60, "Suspects the boy takes windfalls; half-fond anyway");

            // Every loaded pair is ordered by (from, to) in ordinal NPC ID order.
            var ordered = loaded.OrderBy(r => r.From.Value, StringComparer.Ordinal)
                .ThenBy(r => r.To.Value, StringComparer.Ordinal).ToList();
            Assert.That(loaded.Select(r => r.From.Value + "->" + r.To.Value),
                Is.EqualTo(ordered.Select(r => r.From.Value + "->" + r.To.Value)));
        }

        [Test]
        public void DirectedPairsAreIndependent()
        {
            var registry = new RelationshipRegistry();
            registry.Set(new Relationship(Mira, Ralf, 80, 20, "Owes him money"));
            registry.Set(new Relationship(Ralf, Mira, 30, 90, "Fond of his landlady"));

            Assert.That(registry.Trust(Mira, Ralf), Is.EqualTo(80));
            Assert.That(registry.Affection(Mira, Ralf), Is.EqualTo(20));
            Assert.That(registry.Trust(Ralf, Mira), Is.EqualTo(30));
            Assert.That(registry.Affection(Ralf, Mira), Is.EqualTo(90));
        }

        [Test]
        public void UnknownPairReturnsStrangerDefault()
        {
            var registry = new RelationshipRegistry();
            registry.Set(new Relationship(Mira, Ralf, 80, 20, "Owes him money"));

            // Strangers start neutral: the 50 midpoint of the 0–100 scale.
            Assert.That(registry.Trust(Mira, Tom), Is.EqualTo(50));
            Assert.That(registry.Affection(Mira, Tom), Is.EqualTo(50));
            Assert.That(registry.TryGet(Mira, Tom, out Relationship missing), Is.False);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void QueryReturnsOrdinalPairOrderRegardlessOfInsertionOrder()
        {
            var registry = new RelationshipRegistry();
            registry.Set(new Relationship(Ralf, Mira, 30, 90, "Fond of his landlady"));
            registry.Set(new Relationship(Mira, Tom, 40, 60, "Half-fond anyway"));
            registry.Set(new Relationship(Mira, Ralf, 60, 55, "Steady company"));

            Assert.That(registry.Query().Select(r => r.From.Value + "->" + r.To.Value),
                Is.EqualTo(new[]
                {
                    "npc_mira_holt->npc_ralf_hale",
                    "npc_mira_holt->npc_tom_fenn",
                    "npc_ralf_hale->npc_mira_holt",
                }));
        }

        [Test]
        public void SetOverwritesExistingPair()
        {
            var registry = new RelationshipRegistry();
            registry.Set(new Relationship(Mira, Ralf, 60, 55, "Steady company"));
            registry.Set(new Relationship(Mira, Ralf, 75, 65, "Grew closer"));

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That((registry.Trust(Mira, Ralf), registry.Affection(Mira, Ralf)), Is.EqualTo((75, 65)));
        }

        [Test]
        public void InvalidRecordsAreRejectedAtConstruction()
        {
            Assert.Throws<ArgumentException>(() => new Relationship(default, Ralf, 50, 50, "x"));
            Assert.Throws<ArgumentException>(() => new Relationship(Mira, default, 50, 50, "x"));
            Assert.Throws<ArgumentException>(() => new Relationship(Mira, Mira, 50, 50, "self"),
                "An NPC cannot have a relationship with itself.");
            Assert.Throws<ArgumentOutOfRangeException>(() => new Relationship(Mira, Ralf, 101, 50, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Relationship(Mira, Ralf, 50, -1, "x"));
            Assert.Throws<ArgumentException>(() => new Relationship(Mira, Ralf, 50, 50, "  "));
            Assert.Throws<ArgumentNullException>(() => new RelationshipRegistry().Set(null));
            Assert.Throws<ArgumentException>(() => new RelationshipRegistry().Trust(default, Ralf));
        }

        [Test]
        public void LoaderRejectsUnknownNpcId()
        {
            var known = new HashSet<NpcId> { Mira, Ralf };
            var failure = Assert.Throws<SerializationException>(() =>
                RelationshipContentLoader.Load(Json(Row(Mira, With(new NpcId("npc_ghost"), 60, 55, "x"))), known));
            Assert.That(failure.Message, Does.Contain("npc_ghost"));
        }

        [Test]
        public void LoaderRejectsUnknownRowId()
        {
            var known = new HashSet<NpcId> { Mira, Ralf };
            var failure = Assert.Throws<SerializationException>(() =>
                RelationshipContentLoader.Load(Json(Row(new NpcId("npc_ghost"), With(Ralf, 60, 55, "x"))), known));
            Assert.That(failure.Message, Does.Contain("npc_ghost"));
        }

        [Test]
        public void LoaderRejectsSelfPair()
        {
            var known = new HashSet<NpcId> { Mira };
            var failure = Assert.Throws<SerializationException>(() =>
                RelationshipContentLoader.Load(Json(Row(Mira, With(Mira, 60, 55, "self"))), known));
            Assert.That(failure.Message, Does.Contain("itself"));
        }

        [Test]
        public void LoaderRejectsOutOfRangeValues()
        {
            var known = new HashSet<NpcId> { Mira, Ralf };
            var tooTrusty = Assert.Throws<SerializationException>(() =>
                RelationshipContentLoader.Load(Json(Row(Mira, With(Ralf, 101, 55, "x"))), known));
            Assert.That(tooTrusty.Message, Does.Contain("101").Or.Contain("0–100"));
            Assert.Throws<SerializationException>(() =>
                RelationshipContentLoader.Load(Json(Row(Mira, With(Ralf, 60, -1, "x"))), known));
        }

        [Test]
        public void LoaderRejectsDuplicatePair()
        {
            var known = new HashSet<NpcId> { Mira, Ralf };
            var failure = Assert.Throws<SerializationException>(() =>
                RelationshipContentLoader.Load(Json(Row(Mira,
                    With(Ralf, 60, 55, "first"),
                    With(Ralf, 70, 65, "second"))), known));
            Assert.That(failure.Message, Does.Contain("Duplicate"));
        }

        [Test]
        public void LoaderRejectsNullsAndAcceptsMissingRelationships()
        {
            var known = new HashSet<NpcId> { Mira, Ralf };
            Assert.Throws<ArgumentNullException>(() => RelationshipContentLoader.Load(null, known));
            Assert.Throws<ArgumentNullException>(() => RelationshipContentLoader.Load(Json(Row(Mira)), null));

            IReadOnlyList<Relationship> loaded;
            using (Stream stream = Json(Row(Mira)))
                loaded = RelationshipContentLoader.Load(stream, known);
            Assert.That(loaded, Is.Empty, "An NPC without a relationships array has no pairs.");
        }

        [Test]
        public void CaptureRestoreRoundTripsExactly()
        {
            var registry = new RelationshipRegistry();
            registry.Set(new Relationship(Ralf, Mira, 30, 90, "Fond of his landlady"));
            registry.Set(new Relationship(Mira, Tom, 40, 60, "Half-fond anyway"));
            registry.Set(new Relationship(Mira, Ralf, 60, 55, "Steady company"));

            IReadOnlyList<Relationship> snapshot = registry.Capture();
            Assert.That(snapshot, Has.Count.EqualTo(3));

            // Later changes to the source must not leak into the captured snapshot.
            registry.Set(new Relationship(Mira, Ralf, 10, 10, "Fell out"));

            var restored = new RelationshipRegistry();
            restored.Restore(snapshot);

            Assert.That(restored.Query().Select(r => (r.From.Value, r.To.Value, r.Trust, r.Affection, r.Reason)),
                Is.EqualTo(snapshot.Select(r => (r.From.Value, r.To.Value, r.Trust, r.Affection, r.Reason))));
            Assert.That(restored.Trust(Mira, Ralf), Is.EqualTo(60),
                "Restore must install the captured values, not the later mutation.");
        }

        [Test]
        public void RestoreRejectsInvalidBatchesAtomically()
        {
            var registry = new RelationshipRegistry();
            var kept = new Relationship(Mira, Ralf, 60, 55, "Steady company");
            registry.Set(kept);

            var duplicate = new Relationship(Mira, Ralf, 70, 65, "Duplicate");
            Assert.Throws<ArgumentException>(() => registry.Restore(new[] { duplicate, duplicate }));
            Assert.Throws<ArgumentException>(() => registry.Restore(new Relationship[] { null }));
            Assert.Throws<ArgumentNullException>(() => registry.Restore(null));

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryGet(Mira, Ralf, out Relationship actual), Is.True);
            Assert.That(actual, Is.SameAs(kept), "A failed restore must leave the registry unchanged.");
        }

        [Test]
        public void InitializeRelationshipsRequiresRegisteredNpcsAndIsOnceOnly()
        {
            var state = new WorldState(42, new GameTime(0));
            state.Knowledge.Register(Mira);
            state.Knowledge.Register(Ralf);

            var failure = Assert.Throws<InvalidOperationException>(() =>
                state.Knowledge.InitializeRelationships(new[]
                {
                    new Relationship(Mira, Tom, 40, 60, "Tom is not registered"),
                }));
            Assert.That(failure.Message, Does.Contain("npc_tom_fenn"));
            Assert.That(state.Knowledge.Relationships.Count, Is.Zero,
                "A rejected initialization must install nothing.");

            state.Knowledge.InitializeRelationships(new[]
            {
                new Relationship(Mira, Ralf, 60, 55, "Steady company"),
            });
            Assert.That(state.Knowledge.Relationships.Trust(Mira, Ralf), Is.EqualTo(60));

            Assert.Throws<InvalidOperationException>(() =>
                state.Knowledge.InitializeRelationships(Array.Empty<Relationship>()),
                "Relationships install exactly once; persistence restores, never re-initializes.");
        }

        [Test]
        public void KnowledgeCaptureRestoreRelationshipsRoundTrips()
        {
            var state = new WorldState(42, new GameTime(0));
            state.Knowledge.Register(Mira);
            state.Knowledge.Register(Ralf);
            state.Knowledge.InitializeRelationships(new[]
            {
                new Relationship(Mira, Ralf, 60, 55, "Steady company"),
                new Relationship(Ralf, Mira, 30, 90, "Fond of his landlady"),
            });

            IReadOnlyList<Relationship> snapshot = state.Knowledge.CaptureRelationships();

            var fresh = new WorldState(42, new GameTime(0));
            fresh.Knowledge.Register(Mira);
            fresh.Knowledge.Register(Ralf);
            fresh.Knowledge.RestoreRelationships(snapshot);

            Assert.That(fresh.Knowledge.Relationships.Query()
                    .Select(r => (r.From.Value, r.To.Value, r.Trust, r.Affection, r.Reason)),
                Is.EqualTo(state.Knowledge.Relationships.Query()
                    .Select(r => (r.From.Value, r.To.Value, r.Trust, r.Affection, r.Reason))));
        }

        private static void AssertPair(List<Relationship> pairs, NpcId to,
            int trust, int affection, string reason)
        {
            Relationship actual = pairs.SingleOrDefault(pair => pair.To == to);
            Assert.That(actual, Is.Not.Null, "Missing relationship to " + to.Value + ".");
            Assert.That((actual.Trust, actual.Affection, actual.Reason),
                Is.EqualTo((trust, affection, reason)));
        }

        private static string Row(NpcId id, params string[] entries) =>
            "{\"id\":\"" + id.Value + "\",\"relationships\":[" + string.Join(",", entries) + "]}";

        private static string With(NpcId to, int trust, int affection, string reason) =>
            "{\"with\":\"" + to.Value + "\",\"trust\":" + trust +
            ",\"affection\":" + affection + ",\"reason\":\"" + reason + "\"}";

        private static string Row(NpcId id) => "{\"id\":\"" + id.Value + "\"}";

        private static Stream Json(string body)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write("{\"version\":1,\"npcs\":[" + body + "]}");
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        private static HashSet<NpcId> KnownNpcIds(string path)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var ids = new HashSet<NpcId>();
            foreach (JsonElement row in document.RootElement.GetProperty("npcs").EnumerateArray())
                ids.Add(new NpcId(row.GetProperty("id").GetString()));
            return ids;
        }

        private static string ContentPath()
        {
            var root = new DirectoryInfo(NUnit.Framework.TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/npcs/npcs.json")))
                root = root.Parent;
            Assert.That(root, Is.Not.Null, "Find repository content above test output.");
            return Path.Combine(root.FullName, "Content/npcs/npcs.json");
        }
    }
}
