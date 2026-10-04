using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>
    /// Proves the dialogue session (P8-03) is a read-only entry point for
    /// talking to an NPC: it holds only a fact sheet and a phrasing engine,
    /// never world state — so dialogue can phrase facts but can neither change
    /// the world nor leak secrets the NPC does not know.
    /// </summary>
    public sealed class DialogueSessionTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Bram = new NpcId("npc_bram");
        private static readonly LocationId Shop = new LocationId("loc_general_store");
        private static readonly LocationId Home = new LocationId("loc_home");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly VillageId Millbrook = new VillageId("village_millbrook");
        private static readonly VillageId KingsRest = new VillageId("village_kings_rest");

        private static readonly DialogueIntent[] AllIntents =
            (DialogueIntent[])Enum.GetValues(typeof(DialogueIntent));

        private static NpcState MakeNpc(string id, string name, string occupation, int age)
        {
            var definition = new NpcDefinition(new NpcId(id), name, age, "woman", occupation,
                Home, Home, 0,
                new Dictionary<string, int>(),
                new NeedRates(6, 4, 4),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            return new NpcState(definition, 30, 80, 50);
        }

        private static WorldState WorldWith(params NpcState[] npcs)
        {
            var state = new WorldState(1, new GameTime(0));
            foreach (NpcState npc in npcs)
            {
                state.Npcs.Register(npc);
                state.Knowledge.Register(npc.Definition.Id);
            }
            return state;
        }

        private static void AddBelief(WorldState state, NpcId npc, BeliefClaim claim,
            int confidence, BeliefSourceKind sourceKind)
        {
            state.Knowledge.Get(npc).Set(
                new Belief(claim, new BeliefSource(sourceKind), confidence, state.Clock));
        }

        private static FactSheet RichSheet()
        {
            return new FactSheet(Mira, "Mira", "shopkeeper", 34, LifeStage.Adult, 85,
                new[]
                {
                    new FactSheetBelief(BeliefClaimKind.TheftObserved, Shop, Apple, 6, 80,
                        BeliefSourceKind.Seen),
                },
                new[]
                {
                    new FactSheetMemory(BeliefClaimKind.GiftFrom, Shop, Apple, 2, 60),
                },
                new[]
                {
                    new FactSheetNews(NewsKind.Festival, "Millbrook", "King's Rest", 40, true),
                },
                new[]
                {
                    new FactSheetHouseholdMember(Bram, "Bram"),
                });
        }

        private static FactSheet EmptySheet()
        {
            return new FactSheet(Mira, "Mira", "shopkeeper", 34, LifeStage.Adult, 50,
                Array.Empty<FactSheetBelief>(),
                Array.Empty<FactSheetMemory>(),
                Array.Empty<FactSheetNews>(),
                Array.Empty<FactSheetHouseholdMember>());
        }

        // All types reachable from the roots through instance field and
        // property types (transitively). Generic arguments are unwrapped so
        // e.g. IReadOnlyList<FactSheetBelief> contributes FactSheetBelief.
        private static HashSet<Type> ReachableMemberTypes(params Type[] roots)
        {
            var seen = new HashSet<Type>();
            var stack = new Stack<Type>(roots);
            while (stack.Count > 0)
            {
                Type type = stack.Pop();
                if (type == null || !seen.Add(type))
                    continue;
                if (IsSystemType(type) || type.IsEnum)
                {
                    if (type.IsGenericType)
                        foreach (Type arg in type.GetGenericArguments())
                            stack.Push(arg);
                    continue;
                }
                if (type.IsArray)
                {
                    stack.Push(type.GetElementType());
                    continue;
                }
                for (Type current = type;
                    current != null && current != typeof(object);
                    current = current.BaseType)
                {
                    foreach (FieldInfo field in current.GetFields(
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        stack.Push(field.FieldType);
                    foreach (PropertyInfo property in current.GetProperties(
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        stack.Push(property.PropertyType);
                }
                if (type.IsGenericType)
                    foreach (Type arg in type.GetGenericArguments())
                        stack.Push(arg);
            }
            return seen;
        }

        private static bool IsSystemType(Type type) =>
            type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            (type.Namespace != null &&
                (type.Namespace == "System" || type.Namespace.StartsWith("System.")));

        [Test]
        public void DialogueSessionHasNoWorldStateReference()
        {
            WorldState state = WorldWith(MakeNpc("npc_mira", "Mira", "shopkeeper", 34));
            var engine = new TemplatePhrasingEngine(42);
            var session = new DialogueSession(FactSheetBuilder.Build(state, Mira), engine);

            // The declared type graph of the session, plus the concrete engine
            // actually running behind the interface.
            HashSet<Type> reachable = ReachableMemberTypes(
                typeof(DialogueSession), engine.GetType());

            foreach (Type type in reachable)
            {
                if (IsSystemType(type) || type.IsEnum)
                    continue;
                Assert.That(type.Name, Is.Not.EqualTo("WorldState"),
                    "DialogueSession's type graph must not reach WorldState.");
                bool looksLikeMutableState =
                    type.Name.EndsWith("Store") ||
                    type.Name.EndsWith("System") ||
                    type.Name.EndsWith("State");
                Assert.That(looksLikeMutableState, Is.False,
                    "DialogueSession's type graph reaches '" + type.FullName +
                    "', which looks like mutable simulation state. The session may hold " +
                    "only the fact sheet and the phrasing engine.");
            }
        }

        [Test]
        public void DialogueSessionRejectsNullInputs()
        {
            FactSheet sheet = EmptySheet();
            IPhrasingEngine engine = new TemplatePhrasingEngine(1);

            Assert.Throws<ArgumentNullException>(() => new DialogueSession(null, engine));
            Assert.Throws<ArgumentNullException>(() => new DialogueSession(sheet, null));
        }

        [Test]
        public void DialogueNeverLeaksSecrets()
        {
            WorldState state = WorldWith(
                MakeNpc("npc_mira", "Mira", "shopkeeper", 34),
                MakeNpc("npc_bram", "Bram", "miller", 41));

            // Secret 1: a belief about the player held by Bram — not by Mira.
            // Distinctive quantity so any leak is unmistakable.
            const int secretQuantity = 31337;
            AddBelief(state, Bram,
                new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple, ActorId.Player, secretQuantity),
                90, BeliefSourceKind.Seen);

            // Secret 2: news published but not yet arrived anywhere — world
            // truth no NPC knows yet. The origin village ID is a distinctive
            // string that could never appear by coincidence.
            var secretOrigin = new VillageId("village_zzz_secrethold");
            state.News.Publish(secretOrigin, Millbrook, NewsKind.WolfAttack, dayCreated: 1,
                severity: 70, from: secretOrigin, to: Millbrook, arrivalDay: 99);

            FactSheet sheet = FactSheetBuilder.Build(state, Mira);
            Assert.That(sheet.BeliefsAboutPlayer, Is.Empty,
                "precondition: Mira holds no beliefs, so the secrets are not in her sheet");
            Assert.That(sheet.KnownNews, Is.Empty,
                "precondition: no news has arrived, so the in-transit news is not in her sheet");

            foreach (ulong seed in new ulong[] { 1, 7, 42 })
            {
                var session = new DialogueSession(sheet, new TemplatePhrasingEngine(seed));
                foreach (DialogueIntent intent in AllIntents)
                {
                    string line = session.Say(intent);
                    Assert.That(line, Does.Not.Contain(secretQuantity.ToString()),
                        "another NPC's private belief must not leak (intent " + intent +
                        ", seed " + seed + "): \"" + line + "\"");
                    Assert.That(line.ToLowerInvariant(), Does.Not.Contain("zzz"),
                        "unarrived news must not leak (intent " + intent +
                        ", seed " + seed + "): \"" + line + "\"");
                    Assert.That(line.ToLowerInvariant(), Does.Not.Contain("secrethold"),
                        "unarrived news must not leak (intent " + intent +
                        ", seed " + seed + "): \"" + line + "\"");
                }
            }
        }

        [Test]
        public void DialogueLeavesWorldUnchanged()
        {
            WorldState state = WorldWith(
                MakeNpc("npc_mira", "Mira", "shopkeeper", 34),
                MakeNpc("npc_bram", "Bram", "miller", 41));
            AddBelief(state, Mira,
                new BeliefClaim(BeliefClaimKind.TheftObserved, Shop, Apple, ActorId.Player, 6),
                80, BeliefSourceKind.Seen);

            string before = WorldDigest.Compute(state);

            int spoken = 0;
            foreach (NpcId npc in new[] { Mira, Bram })
            {
                FactSheet sheet = FactSheetBuilder.Build(state, npc);
                for (ulong seed = 0; seed < 10 && spoken < 100; seed++)
                {
                    var session = new DialogueSession(sheet, new TemplatePhrasingEngine(seed));
                    foreach (DialogueIntent intent in AllIntents)
                    {
                        Assert.That(session.Say(intent), Is.Not.Empty);
                        spoken++;
                    }
                }
            }

            Assert.That(spoken, Is.GreaterThanOrEqualTo(100),
                "the test should exercise at least 100 utterances");
            Assert.That(WorldDigest.Compute(state), Is.EqualTo(before),
                "100 utterances of dialogue must leave the world byte-identical");
        }

        [Test]
        public void DialogueHandlesDeceasedAndUnknownGracefully()
        {
            WorldState state = WorldWith(MakeNpc("npc_mira", "Mira", "shopkeeper", 34));
            state.Npcs[Mira].MarkDeceased();

            // The dead stay registered (P7); the builder still yields a sheet
            // and the session phrases whatever sheet it receives, by design.
            var dead = new DialogueSession(
                FactSheetBuilder.Build(state, Mira), new TemplatePhrasingEngine(3));
            foreach (DialogueIntent intent in AllIntents)
                Assert.That(dead.Say(intent), Is.Not.Null.And.Not.Empty,
                    "deceased NPC, intent " + intent);

            // An NPC nobody has heard of degrades to a placeholder sheet.
            var stranger = new DialogueSession(
                FactSheetBuilder.Build(state, new NpcId("npc_nobody_heard_of")),
                new TemplatePhrasingEngine(3));
            foreach (DialogueIntent intent in AllIntents)
                Assert.That(stranger.Say(intent), Is.Not.Null.And.Not.Empty,
                    "unknown NPC, intent " + intent);
        }

        [Test]
        public void DialogueSessionIsDeterministic()
        {
            WorldState state = WorldWith(MakeNpc("npc_mira", "Mira", "shopkeeper", 34));
            FactSheet sheet = FactSheetBuilder.Build(state, Mira);

            var first = new DialogueSession(sheet, new TemplatePhrasingEngine(99));
            var second = new DialogueSession(sheet, new TemplatePhrasingEngine(99));

            foreach (DialogueIntent intent in AllIntents)
            {
                string a = first.Say(intent);
                Assert.That(first.Say(intent), Is.EqualTo(a),
                    "same session must repeat identically (intent " + intent + ")");
                Assert.That(second.Say(intent), Is.EqualTo(a),
                    "same sheet and engine seed must phrase identically (intent " + intent + ")");
            }
        }

        [Test]
        public void DialogueSessionCoversAllIntents()
        {
            foreach (FactSheet sheet in new[] { RichSheet(), EmptySheet() })
            {
                var session = new DialogueSession(sheet, new TemplatePhrasingEngine(7));
                foreach (DialogueIntent intent in AllIntents)
                {
                    Assert.That(session.Say(intent), Is.Not.Null.And.Not.Empty,
                        "intent " + intent + " on " +
                        (sheet.BeliefsAboutPlayer.Count > 0 ? "rich" : "empty") + " sheet");
                }
            }
        }
    }
}
