using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>
    /// Proves the template phrasing engine (P8-02) turns fact sheets into speech
    /// without inventing facts: every proper noun and number in the output must
    /// already be in the sheet, wording is deterministic per (sheet, intent,
    /// seed), and phrasing never touches world state.
    /// </summary>
    public sealed class PhrasingTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly LocationId Shop = new LocationId("loc_general_store");
        private static readonly LocationId Bakery = new LocationId("loc_bakery");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly ItemTypeId Fish = new ItemTypeId("item_fish");

        // Every non-fact word any template may emit. If a template gains a word,
        // it must be added here deliberately — the no-invention test enforces it.
        private static readonly HashSet<string> TemplateVocabulary = new HashSet<string>(new[]
        {
            "leave", "me", "be", "what", "do", "you", "want", "oh", "it", "is",
            "hello", "afternoon", "there", "good", "day", "well", "met", "to",
            "see", "a", "fine", "wonderful", "ha", "welcome", "friend", "go",
            "on", "then", "goodbye", "around", "farewell", "take", "care",
            "until", "next", "time", "safe", "travels", "and", "may", "your",
            "road", "kind", "the", "work", "grind", "being", "wears", "down",
            "keeps", "busy", "another", "as", "life", "usual", "for", "i",
            "like", "days", "love", "best", "in", "village", "they", "say",
            "saw", "watched", "heard", "someone", "told", "reckon", "figure",
            "take", "from", "were", "behind", "missing", "knew", "about", "at",
            "wronged", "give", "trade", "fair", "with", "once", "remember",
            "not", "know", "yet", "word", "have", "food", "running", "short",
            "wolves", "attacked", "near", "will", "festival", "was", "fire",
            "thieves", "are", "merchant", "has", "come", "fever", "spreads",
            "drought", "grips", "mill", "wheel", "broke", "building", "bridge",
            "harvest", "no", "news", "reached", "just", "my", "house", "keep",
            "this", "share", "business", "we", "goods", "an",
        });

        private static FactSheet MakeSheet(
            string name = "Mira",
            string occupation = "shopkeeper",
            int mood = 50,
            IReadOnlyList<FactSheetBelief> beliefs = null,
            IReadOnlyList<FactSheetMemory> memories = null,
            IReadOnlyList<FactSheetNews> news = null,
            IReadOnlyList<FactSheetHouseholdMember> household = null)
        {
            return new FactSheet(
                Mira, name, occupation, 34, LifeStage.Adult, mood,
                beliefs ?? Array.Empty<FactSheetBelief>(),
                memories ?? Array.Empty<FactSheetMemory>(),
                news ?? Array.Empty<FactSheetNews>(),
                household ?? Array.Empty<FactSheetHouseholdMember>());
        }

        [TestCase("apple-stall owner", "an")]
        [TestCase("shopkeeper", "a")]
        public void SmalltalkUsesCorrectArticleWithoutChangingOccupation(string occupation, string article)
        {
            bool checkedArticle = false;
            foreach (int mood in new[] { 10, 30, 50, 70 })
                for (ulong seed = 0; seed < 32; seed++)
                {
                    string line = new TemplatePhrasingEngine(seed).Phrase(MakeSheet(occupation: occupation, mood: mood), DialogueIntent.Smalltalk);
                    Assert.That(line, Does.Contain(occupation));
                    if (line.StartsWith("The ") || line.StartsWith("I like the ")) continue;
                    Assert.That(line, Does.Contain(article + " " + occupation));
                    Assert.That(line, Does.Not.Contain((article == "an" ? "a" : "an") + " " + occupation));
                    checkedArticle = true;
                }
            Assert.That(checkedArticle, Is.True);
        }

        private static FactSheetBelief Belief(BeliefClaimKind kind, BeliefSourceKind source,
            ItemTypeId? item = null, int? quantity = null, LocationId? location = null)
        {
            return new FactSheetBelief(kind, location ?? Shop, item, quantity, 80, source);
        }

        private static FactSheet RichSheet()
        {
            return MakeSheet(
                mood: 85,
                beliefs: new[]
                {
                    Belief(BeliefClaimKind.TheftObserved, BeliefSourceKind.Seen, Apple, 6),
                    Belief(BeliefClaimKind.Presence, BeliefSourceKind.ToldBy, location: Bakery),
                },
                memories: new[]
                {
                    new FactSheetMemory(BeliefClaimKind.GiftFrom, Shop, Fish, 2, 60),
                },
                news: new[]
                {
                    new FactSheetNews(NewsKind.Festival, "Millbrook", "King's Rest", 40, true),
                },
                household: new[]
                {
                    new FactSheetHouseholdMember(new NpcId("npc_lida"), "Lida"),
                    new FactSheetHouseholdMember(new NpcId("npc_bram"), "Bram"),
                });
        }

        private static IEnumerable<string> Tokens(string text)
        {
            var current = new StringBuilder();
            foreach (char c in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    current.Append(c);
                }
                else if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
            }
            if (current.Length > 0) yield return current.ToString();
        }

        // Mirrors the engine's presentation rule: content IDs are "<prefix>_<words>".
        private static string DisplayName(string contentId)
        {
            int cut = contentId.IndexOf('_');
            string words = cut >= 0 ? contentId.Substring(cut + 1) : contentId;
            return words.Replace('_', ' ');
        }

        // Every word the sheet entitles the engine to emit.
        private static HashSet<string> FactWords(FactSheet sheet)
        {
            var words = new HashSet<string>();
            void Add(string text)
            {
                if (text == null) return;
                foreach (string token in Tokens(text)) words.Add(token);
            }
            Add(sheet.Name);
            Add(sheet.Occupation);
            Add(sheet.Age.ToString());
            Add(sheet.MoodBand);
            foreach (FactSheetBelief belief in sheet.BeliefsAboutPlayer)
            {
                Add(DisplayName(belief.Location.Value));
                Add(belief.Quantity?.ToString());
                if (belief.ItemType.HasValue)
                {
                    string item = DisplayName(belief.ItemType.Value.Value);
                    Add(item);
                    Add(item + "s");
                    Add(item + "es");
                }
            }
            foreach (FactSheetMemory memory in sheet.MemoriesAboutPlayer)
            {
                Add(DisplayName(memory.Location.Value));
                Add(memory.Quantity?.ToString());
                if (memory.ItemType.HasValue)
                {
                    string item = DisplayName(memory.ItemType.Value.Value);
                    Add(item);
                    Add(item + "s");
                    Add(item + "es");
                }
            }
            foreach (FactSheetNews item in sheet.KnownNews)
            {
                Add(item.OriginName);
                Add(item.AboutName);
                Add(item.Severity.ToString());
            }
            foreach (FactSheetHouseholdMember member in sheet.HouseholdMembers)
                Add(member.Name);
            return words;
        }

        private static readonly DialogueIntent[] AllIntents =
            (DialogueIntent[])Enum.GetValues(typeof(DialogueIntent));

        [Test]
        public void TemplateEngineImplementsContract()
        {
            IPhrasingEngine engine = new TemplatePhrasingEngine(42);
            Assert.That(engine, Is.InstanceOf<IPhrasingEngine>());

            Assert.Throws<ArgumentNullException>(() => engine.Phrase(null, DialogueIntent.Greeting));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => engine.Phrase(MakeSheet(), (DialogueIntent)999));
        }

        [Test]
        public void PhrasingCoversAllIntents()
        {
            var engine = new TemplatePhrasingEngine(7);
            foreach (DialogueIntent intent in AllIntents)
            {
                Assert.That(engine.Phrase(RichSheet(), intent), Is.Not.Null.And.Not.Empty,
                    "intent " + intent);
                Assert.That(engine.Phrase(MakeSheet(), intent), Is.Not.Null.And.Not.Empty,
                    "empty sheet, intent " + intent);
            }
        }

        [Test]
        public void PhrasingIsDeterministicForSameInputs()
        {
            var first = new TemplatePhrasingEngine(1234);
            var second = new TemplatePhrasingEngine(1234);
            FactSheet sheet = RichSheet();

            foreach (DialogueIntent intent in AllIntents)
            {
                string a = first.Phrase(sheet, intent);
                string b = first.Phrase(sheet, intent);
                string c = second.Phrase(sheet, intent);
                Assert.That(b, Is.EqualTo(a), "same engine, intent " + intent);
                Assert.That(c, Is.EqualTo(a), "same seed, intent " + intent);
            }
        }

        [Test]
        public void PhrasingVariesWithSeedButNotFacts()
        {
            // Seeds discovered empirically: 0 and 3 pick different greeting variants.
            // Deterministic forever because the engine is pure.
            var seed0 = new TemplatePhrasingEngine(0);
            var seed3 = new TemplatePhrasingEngine(3);
            FactSheet sheet = RichSheet();

            Assert.That(seed3.Phrase(sheet, DialogueIntent.Greeting),
                Is.Not.EqualTo(seed0.Phrase(sheet, DialogueIntent.Greeting)),
                "different seeds should vary wording");

            // But the facts phrased never change with the seed.
            HashSet<string> facts = FactWords(sheet);
            foreach (DialogueIntent intent in AllIntents)
            {
                var perSeed = new List<HashSet<string>>();
                for (ulong seed = 0; seed < 10; seed++)
                {
                    string output = new TemplatePhrasingEngine(seed).Phrase(sheet, intent);
                    perSeed.Add(new HashSet<string>(Tokens(output).Where(facts.Contains)));
                }
                for (int i = 1; i < perSeed.Count; i++)
                    Assert.That(perSeed[i], Is.EquivalentTo(perSeed[0]),
                        "facts must not vary with seed, intent " + intent);
            }
        }

        [Test]
        public void PhrasingNeverInventsFacts()
        {
            var sheets = new[] { RichSheet(), MakeSheet(), MakeSheet(news: new[]
            {
                new FactSheetNews(NewsKind.WolfAttack, "Oakhollow", "Millbrook", 70, false),
            }) };

            foreach (FactSheet sheet in sheets)
            {
                HashSet<string> allowed = new HashSet<string>(TemplateVocabulary);
                allowed.UnionWith(FactWords(sheet));
                foreach (DialogueIntent intent in AllIntents)
                {
                    foreach (ulong seed in new ulong[] { 1, 7, 42 })
                    {
                        string output = new TemplatePhrasingEngine(seed).Phrase(sheet, intent);
                        foreach (string token in Tokens(output))
                        {
                            Assert.That(allowed.Contains(token), Is.True,
                                $"invented word '{token}' in {intent} output: \"{output}\"");
                        }
                    }
                }
            }
        }

        [Test]
        public void PhrasingReflectsMoodBand()
        {
            var engine = new TemplatePhrasingEngine(5);
            var greetings = new Dictionary<string, string>();
            foreach (int mood in new[] { 5, 30, 50, 70, 95 })
            {
                FactSheet sheet = MakeSheet(mood: mood);
                greetings[sheet.MoodBand] = engine.Phrase(sheet, DialogueIntent.Greeting);
            }

            Assert.That(greetings.Keys, Is.EquivalentTo(
                new[] { "miserable", "low", "neutral", "content", "happy" }));
            Assert.That(greetings.Values.Distinct().Count(), Is.EqualTo(5),
                "each mood band should greet differently");
        }

        [Test]
        public void PhrasingHedgesBySourceKind()
        {
            var expectations = new[]
            {
                (BeliefSourceKind.Seen, "saw"),
                (BeliefSourceKind.ToldBy, "heard"),
                (BeliefSourceKind.Inferred, "reckon"),
            };
            foreach ((BeliefSourceKind source, string hedge) in expectations)
            {
                FactSheet sheet = MakeSheet(beliefs: new[]
                {
                    Belief(BeliefClaimKind.TheftObserved, source, Apple, 6),
                });
                string output = new TemplatePhrasingEngine(9)
                    .Phrase(sheet, DialogueIntent.AskAboutPlayer);
                Assert.That(output.ToLowerInvariant(), Does.Contain(hedge),
                    $"source {source} should hedge with '{hedge}': \"{output}\"");
                Assert.That(output, Does.Contain("6"),
                    "the quantity from the sheet must survive the hedge");
            }
        }

        [Test]
        public void PhrasingDoesNotMutateState()
        {
            var state = new WorldState(1, new GameTime(0));
            var definition = new NpcDefinition(new NpcId("npc_mira"), "Mira", 34, "woman",
                "shopkeeper", Shop, Shop, 0,
                new Dictionary<string, int>(), new NeedRates(6, 4, 4),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            var mira = new NpcState(definition, 30, 80, 50);
            state.Npcs.Register(mira);
            state.Knowledge.Register(mira.Definition.Id);

            string before = WorldDigest.Compute(state);
            FactSheet sheet = FactSheetBuilder.Build(state, Mira);
            foreach (DialogueIntent intent in AllIntents)
                foreach (ulong seed in new ulong[] { 1, 2, 3 })
                    new TemplatePhrasingEngine(seed).Phrase(sheet, intent);
            string after = WorldDigest.Compute(state);

            Assert.That(after, Is.EqualTo(before));
        }
    }
}
