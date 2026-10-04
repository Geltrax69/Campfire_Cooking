using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// One belief the NPC holds about the player, as dialogue may phrase it (P8-01).
    /// This is knowledge, not truth: the claim may be false.
    /// </summary>
    public sealed class FactSheetBelief : IEquatable<FactSheetBelief>
    {
        public FactSheetBelief(BeliefClaimKind kind, LocationId location, ItemTypeId? itemType,
            int? quantity, int confidence, BeliefSourceKind sourceKind)
        {
            Kind = kind;
            Location = location;
            ItemType = itemType;
            Quantity = quantity;
            Confidence = confidence;
            SourceKind = sourceKind;
        }

        public BeliefClaimKind Kind { get; }
        public LocationId Location { get; }
        public ItemTypeId? ItemType { get; }
        public int? Quantity { get; }
        public int Confidence { get; }
        public BeliefSourceKind SourceKind { get; }

        public bool Equals(FactSheetBelief other) =>
            other != null && Kind == other.Kind && Location == other.Location &&
            ItemType == other.ItemType && Quantity == other.Quantity &&
            Confidence == other.Confidence && SourceKind == other.SourceKind;

        public override bool Equals(object obj) => Equals(obj as FactSheetBelief);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 31 + Location.GetHashCode();
                hash = hash * 31 + (ItemType.HasValue ? ItemType.Value.GetHashCode() : 0);
                hash = hash * 31 + (Quantity.HasValue ? Quantity.Value : 0);
                hash = hash * 31 + Confidence;
                hash = hash * 31 + (int)SourceKind;
                return hash;
            }
        }
    }

    /// <summary>One attributed memory involving the player, most recent first (P8-01).</summary>
    public sealed class FactSheetMemory : IEquatable<FactSheetMemory>
    {
        public FactSheetMemory(BeliefClaimKind kind, LocationId location, ItemTypeId? itemType,
            int? quantity, int importance)
        {
            Kind = kind;
            Location = location;
            ItemType = itemType;
            Quantity = quantity;
            Importance = importance;
        }

        public BeliefClaimKind Kind { get; }
        public LocationId Location { get; }
        public ItemTypeId? ItemType { get; }
        public int? Quantity { get; }
        public int Importance { get; }

        public bool Equals(FactSheetMemory other) =>
            other != null && Kind == other.Kind && Location == other.Location &&
            ItemType == other.ItemType && Quantity == other.Quantity &&
            Importance == other.Importance;

        public override bool Equals(object obj) => Equals(obj as FactSheetMemory);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 31 + Location.GetHashCode();
                hash = hash * 31 + (ItemType.HasValue ? ItemType.Value.GetHashCode() : 0);
                hash = hash * 31 + (Quantity.HasValue ? Quantity.Value : 0);
                hash = hash * 31 + Importance;
                return hash;
            }
        }
    }

    /// <summary>
    /// One piece of inter-village news the NPC's village has heard (P8-01).
    /// The severity is the distorted copy the village received, not the truth.
    /// </summary>
    public sealed class FactSheetNews : IEquatable<FactSheetNews>
    {
        public FactSheetNews(NewsKind kind, string originName, string aboutName,
            int severity, bool isGoodNews)
        {
            Kind = kind;
            OriginName = originName ?? throw new ArgumentNullException(nameof(originName));
            AboutName = aboutName ?? throw new ArgumentNullException(nameof(aboutName));
            Severity = severity;
            IsGoodNews = isGoodNews;
        }

        public NewsKind Kind { get; }
        public string OriginName { get; }
        public string AboutName { get; }
        public int Severity { get; }
        public bool IsGoodNews { get; }

        public bool Equals(FactSheetNews other) =>
            other != null && Kind == other.Kind && OriginName == other.OriginName &&
            AboutName == other.AboutName && Severity == other.Severity &&
            IsGoodNews == other.IsGoodNews;

        public override bool Equals(object obj) => Equals(obj as FactSheetNews);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 31 + OriginName.GetHashCode();
                hash = hash * 31 + AboutName.GetHashCode();
                hash = hash * 31 + Severity;
                hash = hash * 31 + (IsGoodNews ? 1 : 0);
                return hash;
            }
        }
    }

    /// <summary>One member of the NPC's own household (P8-01).</summary>
    public sealed class FactSheetHouseholdMember : IEquatable<FactSheetHouseholdMember>
    {
        public FactSheetHouseholdMember(NpcId npcId, string name)
        {
            NpcId = npcId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public NpcId NpcId { get; }
        public string Name { get; }

        public bool Equals(FactSheetHouseholdMember other) =>
            other != null && NpcId == other.NpcId && Name == other.Name;

        public override bool Equals(object obj) => Equals(obj as FactSheetHouseholdMember);

        public override int GetHashCode() =>
            unchecked(NpcId.GetHashCode() * 31 + Name.GetHashCode());
    }

    /// <summary>
    /// Everything dialogue is allowed to phrase about one NPC (P8-01): identity,
    /// mood, what they believe and remember about the player, news their village
    /// has heard, and their household. Built only from knowledge stores — never
    /// from world truth the NPC has no access to. A plain data object: no
    /// behavior, no RNG.
    /// </summary>
    public sealed class FactSheet : IEquatable<FactSheet>
    {
        public FactSheet(NpcId npcId, string name, string occupation, int age,
            LifeStage lifeStage, int mood,
            IReadOnlyList<FactSheetBelief> beliefsAboutPlayer,
            IReadOnlyList<FactSheetMemory> memoriesAboutPlayer,
            IReadOnlyList<FactSheetNews> knownNews,
            IReadOnlyList<FactSheetHouseholdMember> householdMembers)
        {
            NpcId = npcId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Occupation = occupation ?? throw new ArgumentNullException(nameof(occupation));
            Age = age;
            LifeStage = lifeStage;
            Mood = mood;
            MoodBand = MoodBandFor(mood);
            BeliefsAboutPlayer = beliefsAboutPlayer ?? throw new ArgumentNullException(nameof(beliefsAboutPlayer));
            MemoriesAboutPlayer = memoriesAboutPlayer ?? throw new ArgumentNullException(nameof(memoriesAboutPlayer));
            KnownNews = knownNews ?? throw new ArgumentNullException(nameof(knownNews));
            HouseholdMembers = householdMembers ?? throw new ArgumentNullException(nameof(householdMembers));
        }

        public NpcId NpcId { get; }
        public string Name { get; }
        public string Occupation { get; }
        public int Age { get; }
        public LifeStage LifeStage { get; }
        public int Mood { get; }
        public string MoodBand { get; }
        public IReadOnlyList<FactSheetBelief> BeliefsAboutPlayer { get; }
        public IReadOnlyList<FactSheetMemory> MemoriesAboutPlayer { get; }
        public IReadOnlyList<FactSheetNews> KnownNews { get; }
        public IReadOnlyList<FactSheetHouseholdMember> HouseholdMembers { get; }

        /// <summary>Qualitative mood band for the 0-100 happiness scale.</summary>
        public static string MoodBandFor(int mood)
        {
            if (mood < 0 || mood > 100) throw new ArgumentOutOfRangeException(nameof(mood), "Mood is 0-100.");
            if (mood < 20) return "miserable";
            if (mood < 40) return "low";
            if (mood < 60) return "neutral";
            if (mood < 80) return "content";
            return "happy";
        }

        private static bool ListsEqual<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
            where T : IEquatable<T>
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!a[i].Equals(b[i])) return false;
            return true;
        }

        public bool Equals(FactSheet other) =>
            other != null && NpcId == other.NpcId && Name == other.Name &&
            Occupation == other.Occupation && Age == other.Age &&
            LifeStage == other.LifeStage && Mood == other.Mood &&
            ListsEqual(BeliefsAboutPlayer, other.BeliefsAboutPlayer) &&
            ListsEqual(MemoriesAboutPlayer, other.MemoriesAboutPlayer) &&
            ListsEqual(KnownNews, other.KnownNews) &&
            ListsEqual(HouseholdMembers, other.HouseholdMembers);

        public override bool Equals(object obj) => Equals(obj as FactSheet);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = NpcId.GetHashCode();
                hash = hash * 31 + Name.GetHashCode();
                hash = hash * 31 + Mood;
                hash = hash * 31 + BeliefsAboutPlayer.Count;
                hash = hash * 31 + MemoriesAboutPlayer.Count;
                hash = hash * 31 + KnownNews.Count;
                return hash;
            }
        }
    }
}
