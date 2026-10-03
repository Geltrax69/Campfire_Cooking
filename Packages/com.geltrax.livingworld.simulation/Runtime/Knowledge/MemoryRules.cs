using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Defines one approved importance band and its maximum retention age.</summary>
    public sealed class MemoryLevel
    {
        internal MemoryLevel(string id, int minimumImportance, int maximumImportance, int retentionDays)
        {
            Id = id;
            MinimumImportance = minimumImportance;
            MaximumImportance = maximumImportance;
            RetentionDays = retentionDays;
        }

        public string Id { get; }
        public int MinimumImportance { get; }
        public int MaximumImportance { get; }
        public int RetentionDays { get; }
    }

    /// <summary>Immutable approved importance bands and decay rates loaded from social content.</summary>
    public sealed class MemoryRules
    {
        private const int SupportedVersion = 1;
        private static readonly string[] ExpectedIds = { "memory_trivia", "memory_minor", "memory_notable",
            "memory_important", "memory_major" };

        private MemoryRules(IReadOnlyList<MemoryLevel> levels, int triviaMinorPerDay,
            int notablePer3Days, int importantPer10Days, string majorNote)
        {
            Levels = levels;
            TriviaMinorPerDay = triviaMinorPerDay;
            NotablePer3Days = notablePer3Days;
            ImportantPer10Days = importantPer10Days;
            MajorNote = majorNote;
        }

        public IReadOnlyList<MemoryLevel> Levels { get; }
        public int TriviaMinorPerDay { get; }
        public int NotablePer3Days { get; }
        public int ImportantPer10Days { get; }
        public string MajorNote { get; }

        public static MemoryRules Load(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            var serializer = new DataContractJsonSerializer(typeof(SocialDocumentDto));
            var document = (SocialDocumentDto)serializer.ReadObject(stream);
            if (document == null || document.Version != SupportedVersion)
                throw new SerializationException("Unsupported or missing social content version.");
            if (document.Memory == null || document.Memory.Levels == null || document.Memory.Decay == null)
                throw new SerializationException("Memory levels and decay rules are required.");
            if (document.Memory.Levels.Length != ExpectedIds.Length)
                throw new SerializationException("Exactly five memory levels are required.");

            var levels = new List<MemoryLevel>(ExpectedIds.Length);
            int previousMaximum = 0;
            for (int i = 0; i < document.Memory.Levels.Length; i++)
            {
                LevelDto row = document.Memory.Levels[i];
                if (row == null || row.Id != ExpectedIds[i] || !row.Min.HasValue || !row.Max.HasValue
                    || !row.RetentionDays.HasValue)
                    throw new SerializationException("Memory level IDs and numeric fields must match the approved schema.");
                int minimum = row.Min.Value;
                int maximum = row.Max.Value;
                if (minimum != (i == 0 ? 0 : previousMaximum) || maximum <= minimum || maximum > 100
                    || row.RetentionDays.Value <= 0)
                    throw new SerializationException("Memory levels must provide ordered coverage from 0 through 100.");
                levels.Add(new MemoryLevel(row.Id, minimum, maximum, row.RetentionDays.Value));
                previousMaximum = maximum;
            }
            if (previousMaximum != 100) throw new SerializationException("Memory levels must end at 100.");

            DecayDto decay = document.Memory.Decay;
            if (!decay.TriviaMinorPerDay.HasValue || decay.TriviaMinorPerDay <= 0
                || !decay.NotablePer3Days.HasValue || decay.NotablePer3Days <= 0
                || !decay.ImportantPer10Days.HasValue || decay.ImportantPer10Days <= 0
                || string.IsNullOrWhiteSpace(decay.MajorNote))
                throw new SerializationException("Approved positive decay rates and the Major note are required.");
            return new MemoryRules(new ReadOnlyCollection<MemoryLevel>(levels), decay.TriviaMinorPerDay.Value,
                decay.NotablePer3Days.Value, decay.ImportantPer10Days.Value, decay.MajorNote);
        }

        internal MemoryLevel LevelFor(int importance)
        {
            if (importance < 0 || importance > 100) throw new ArgumentOutOfRangeException(nameof(importance));
            for (int i = Levels.Count - 1; i >= 0; i--)
                if (importance >= Levels[i].MinimumImportance) return Levels[i];
            throw new InvalidOperationException("Memory rules do not cover the importance value.");
        }

        internal int StrengthLoss(int importance, long elapsedDays)
        {
            MemoryLevel level = LevelFor(importance);
            if (level.Id == ExpectedIds[0] || level.Id == ExpectedIds[1])
                return SaturatingLoss(elapsedDays, TriviaMinorPerDay);
            if (level.Id == ExpectedIds[2])
                return SaturatingLoss(elapsedDays / 3, NotablePer3Days);
            if (level.Id == ExpectedIds[3])
                return SaturatingLoss(elapsedDays / 10, ImportantPer10Days);
            return 0;
        }

        private static int SaturatingLoss(long periods, int rate)
        {
            if (periods <= 0) return 0;
            return periods > int.MaxValue / rate ? int.MaxValue : (int)periods * rate;
        }

        [DataContract]
        private sealed class SocialDocumentDto
        {
            [DataMember(Name = "version", IsRequired = true)] public int? Version { get; set; }
            [DataMember(Name = "memory", IsRequired = true)] public MemoryDto Memory { get; set; }
        }

        [DataContract]
        private sealed class MemoryDto
        {
            [DataMember(Name = "levels", IsRequired = true)] public LevelDto[] Levels { get; set; }
            [DataMember(Name = "decay", IsRequired = true)] public DecayDto Decay { get; set; }
        }

        [DataContract]
        private sealed class LevelDto
        {
            [DataMember(Name = "id", IsRequired = true)] public string Id { get; set; }
            [DataMember(Name = "min", IsRequired = true)] public int? Min { get; set; }
            [DataMember(Name = "max", IsRequired = true)] public int? Max { get; set; }
            [DataMember(Name = "retentionDays", IsRequired = true)] public int? RetentionDays { get; set; }
        }

        [DataContract]
        private sealed class DecayDto
        {
            [DataMember(Name = "triviaMinorPerDay", IsRequired = true)] public int? TriviaMinorPerDay { get; set; }
            [DataMember(Name = "notablePer3Days", IsRequired = true)] public int? NotablePer3Days { get; set; }
            [DataMember(Name = "importantPer10Days", IsRequired = true)] public int? ImportantPer10Days { get; set; }
            [DataMember(Name = "majorNote", IsRequired = true)] public string MajorNote { get; set; }
        }
    }
}
