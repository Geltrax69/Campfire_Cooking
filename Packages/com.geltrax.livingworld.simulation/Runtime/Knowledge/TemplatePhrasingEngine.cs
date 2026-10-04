using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Deterministic template phrasing for NPC dialogue (P8-02): the
    /// simulation-side stand-in for the future AI model. Fills hand-written
    /// templates from the fact sheet only.
    ///
    /// Safety rules, enforced by construction:
    /// <list type="bullet">
    /// <item>Every template interpolates sheet fields (name, occupation, mood
    /// band, belief/memory/news/household facts) or generic conversational
    /// words. No template contains a proper noun or number of its own.</item>
    /// <item>Content IDs ("item_apple", "loc_general_store") are rendered as
    /// words by stripping the conventional prefix. That is presentation of
    /// sheet data, not a new fact.</item>
    /// <item>Wording varies with the constructor seed, but only among
    /// same-fact variants: the set of fact words in the output is identical
    /// for every seed.</item>
    /// </list>
    ///
    /// Pure: Phrase never touches world state and allocates nothing shared.
    /// </summary>
    public sealed class TemplatePhrasingEngine : IPhrasingEngine
    {
        private readonly ulong _seed;

        /// <summary>
        /// Creates the engine. The seed selects among same-fact wording
        /// variants; it is part of the input, so (sheet, intent, seed) always
        /// phrases identically.
        /// </summary>
        public TemplatePhrasingEngine(ulong seed)
        {
            _seed = seed;
        }

        public ulong Seed => _seed;

        public string Phrase(FactSheet sheet, DialogueIntent intent)
        {
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));
            switch (intent)
            {
                case DialogueIntent.Greeting:
                    return Pick(sheet, intent, GreetingsFor(sheet.MoodBand));
                case DialogueIntent.Farewell:
                    return Pick(sheet, intent, FarewellsFor(sheet.MoodBand));
                case DialogueIntent.Smalltalk:
                    return Pick(sheet, intent, SmalltalkFor(sheet));
                case DialogueIntent.AskAboutPlayer:
                    return Pick(sheet, intent, AboutPlayerFor(sheet));
                case DialogueIntent.ShareNews:
                    return Pick(sheet, intent, NewsFor(sheet));
                case DialogueIntent.AskAboutFamily:
                    return Pick(sheet, intent, FamilyFor(sheet));
                default:
                    throw new ArgumentOutOfRangeException(nameof(intent),
                        "Unknown dialogue intent: " + intent);
            }
        }

        // Picks one variant with a per-(seed, npc, intent) draw, so wording can
        // differ by seed and by speaker while staying deterministic.
        private string Pick(FactSheet sheet, DialogueIntent intent, IReadOnlyList<string> variants)
        {
            if (variants.Count == 1) return variants[0];
            string npc = sheet.NpcId.Value ?? string.Empty;
            ulong mixed = _seed ^ Fnv1a64(npc) ^ ((ulong)(int)intent * 0x9E3779B97F4A7C15UL);
            return variants[new SimRng(mixed).NextInt(variants.Count)];
        }

        private static ulong Fnv1a64(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char c in text) hash = (hash ^ c) * 1099511628211UL;
                return hash;
            }
        }

        // Content IDs are "<prefix>_<words>" by project convention; rendering
        // the words is presentation of sheet data, not invention.
        private static string DisplayName(string contentId)
        {
            int cut = contentId.IndexOf('_');
            string words = cut >= 0 ? contentId.Substring(cut + 1) : contentId;
            return words.Replace('_', ' ');
        }

        // Crude plural for counts ("6 apples", "2 fishes"). Documented as crude:
        // templates never need perfect grammar, only sheet-faithful nouns.
        private static string Pluralize(string name, int quantity)
        {
            if (quantity == 1) return name;
            if (name.EndsWith("s", StringComparison.Ordinal) ||
                name.EndsWith("x", StringComparison.Ordinal) ||
                name.EndsWith("sh", StringComparison.Ordinal) ||
                name.EndsWith("ch", StringComparison.Ordinal))
                return name + "es";
            return name + "s";
        }

        private static string[] GreetingsFor(string moodBand)
        {
            switch (moodBand)
            {
                case "miserable": return new[] { "Leave me be.", "What do you want?", "Oh. It is you." };
                case "low": return new[] { "Hello.", "Afternoon.", "Oh, hello." };
                case "neutral": return new[] { "Hello there.", "Good day.", "Well met." };
                case "content": return new[] { "Good to see you.", "Well hello.", "A fine day to you." };
                default: return new[] { "Wonderful to see you.", "Ha. Good day.", "Welcome, friend." };
            }
        }

        private static string[] FarewellsFor(string moodBand)
        {
            switch (moodBand)
            {
                case "miserable": return new[] { "Go on, then.", "Fine. Goodbye." };
                case "low": return new[] { "Goodbye.", "See you around." };
                case "neutral": return new[] { "Farewell.", "Take care.", "Until next time." };
                case "content": return new[] { "Safe travels.", "Goodbye, and take care." };
                default: return new[] { "Farewell, friend.", "May your road be kind." };
            }
        }

        private static string[] SmalltalkFor(FactSheet sheet)
        {
            string occupation = sheet.Occupation;
            switch (sheet.MoodBand)
            {
                case "miserable": return new[]
                {
                    "The " + occupation + " work is a grind.",
                    "Being a " + occupation + " wears me down.",
                };
                case "low": return new[]
                {
                    "The " + occupation + " work keeps me busy.",
                    "Another day as a " + occupation + ".",
                };
                case "neutral": return new[]
                {
                    "The " + occupation + " life keeps me busy.",
                    "Business as usual for a " + occupation + ".",
                };
                case "content": return new[]
                {
                    "I like the " + occupation + " life.",
                    "Good days, being a " + occupation + ".",
                };
                default: return new[]
                {
                    "I love the " + occupation + " life.",
                    "Best " + occupation + " in the village, they say.",
                };
            }
        }

        // What the NPC thinks of the player: first belief wins, else first
        // memory, else an honest fallback. Beliefs are hedged by source kind:
        // seen ("I saw"), told ("I heard"), inferred ("I reckon").
        private static string[] AboutPlayerFor(FactSheet sheet)
        {
            if (sheet.BeliefsAboutPlayer.Count > 0)
            {
                FactSheetBelief belief = sheet.BeliefsAboutPlayer[0];
                string claim = ClaimPhrase(belief);
                switch (belief.SourceKind)
                {
                    case BeliefSourceKind.Seen:
                        return new[] { "I saw you " + claim + ".", "I watched you " + claim + "." };
                    case BeliefSourceKind.ToldBy:
                        return new[] { "I heard you " + claim + ".", "Someone told me you " + claim + "." };
                    default:
                        return new[] { "I reckon you " + claim + ".", "I figure you " + claim + "." };
                }
            }
            if (sheet.MemoriesAboutPlayer.Count > 0)
                return new[] { MemoryPhrase(sheet.MemoriesAboutPlayer[0]) };
            return new[] { "I do not know you well yet." };
        }

        private static string ItemPhrase(FactSheetBelief belief)
        {
            if (!belief.ItemType.HasValue) return "goods";
            string name = DisplayName(belief.ItemType.Value.Value);
            return belief.Quantity.HasValue
                ? belief.Quantity.Value + " " + Pluralize(name, belief.Quantity.Value)
                : name;
        }

        private static string ClaimPhrase(FactSheetBelief belief)
        {
            string location = DisplayName(belief.Location.Value);
            string item = ItemPhrase(belief);
            switch (belief.Kind)
            {
                case BeliefClaimKind.TheftObserved: return "take " + item + " from the " + location;
                case BeliefClaimKind.StockMissing: return "were behind the missing " + item + " at the " + location;
                case BeliefClaimKind.StockAvailable: return "knew about the " + item + " at the " + location;
                case BeliefClaimKind.Presence: return "were at the " + location;
                case BeliefClaimKind.WrongedBy: return "wronged me at the " + location;
                case BeliefClaimKind.GiftFrom: return "give me " + item;
                default: return "trade fair with me at the " + location;
            }
        }

        private static string MemoryPhrase(FactSheetMemory memory)
        {
            string location = DisplayName(memory.Location.Value);
            string item = memory.ItemType.HasValue ? DisplayName(memory.ItemType.Value.Value) : "goods";
            switch (memory.Kind)
            {
                case BeliefClaimKind.WrongedBy: return "You wronged me once.";
                case BeliefClaimKind.GiftFrom: return "You gave me " + item + " once, I remember.";
                case BeliefClaimKind.FairTradeWith: return "We traded fair once.";
                default: return "I remember you at the " + location + ".";
            }
        }

        private static string[] NewsFor(FactSheet sheet)
        {
            if (sheet.KnownNews.Count == 0)
                return new[] { "No news has reached me." };
            FactSheetNews news = sheet.KnownNews[0];
            string phrase = NewsPhrase(news);
            string capped = char.ToUpperInvariant(phrase[0]) + phrase.Substring(1);
            return new[]
            {
                "Word from " + news.OriginName + ": " + phrase + ".",
                "Have you heard the news from " + news.OriginName + "? " + capped + ".",
            };
        }

        private static string NewsPhrase(FactSheetNews news)
        {
            string about = news.AboutName;
            switch (news.Kind)
            {
                case NewsKind.FoodShortage: return "food is running short in " + about;
                case NewsKind.WolfAttack: return "wolves attacked near " + about;
                case NewsKind.Festival: return "there will be a festival at " + about;
                case NewsKind.Fire: return "there was a fire at " + about;
                case NewsKind.TheftWave: return "thieves are busy around " + about;
                case NewsKind.MerchantArrival: return "a merchant has come to " + about;
                case NewsKind.Fever: return "fever spreads in " + about;
                case NewsKind.Drought: return "drought grips " + about;
                case NewsKind.WheelFailure: return "the mill wheel broke at " + about;
                case NewsKind.BridgeProject: return "they are building a bridge near " + about;
                default: return "the harvest was good in " + about;
            }
        }

        private static string[] FamilyFor(FactSheet sheet)
        {
            if (sheet.HouseholdMembers.Count == 0)
                return new[] { "It is just me in my house." };
            var names = new List<string>();
            foreach (FactSheetHouseholdMember member in sheet.HouseholdMembers)
                names.Add(member.Name);
            string list = string.Join(", ", names);
            string andList = names.Count == 1
                ? names[0]
                : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
            return new[]
            {
                list + " and I share this house.",
                "I share this house with " + andList + ".",
            };
        }
    }
}
