using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// One immutable piece of inter-village news (P6-03): what happened, where,
    /// and how much the road talks about it. This is world truth; each village's
    /// copy may be distorted in transit.
    /// </summary>
    public sealed class VillageNews
    {
        public const int MinSeverity = 0;
        public const int MaxSeverity = 100;

        public VillageNews(NewsId id, VillageId origin, VillageId about, NewsKind kind,
            long dayCreated, int severity)
        {
            if (!id.IsValid) throw new ArgumentException("News needs a valid ID.", nameof(id));
            if (!origin.IsValid) throw new ArgumentException("News needs a valid origin village.", nameof(origin));
            if (!about.IsValid) throw new ArgumentException("News needs a valid subject village.", nameof(about));
            if (dayCreated < 1) throw new ArgumentOutOfRangeException(nameof(dayCreated),
                "Game days are one-based.");
            if (severity < MinSeverity || severity > MaxSeverity)
                throw new ArgumentOutOfRangeException(nameof(severity), "Severity is 0-100.");

            Id = id;
            Origin = origin;
            About = about;
            Kind = kind;
            DayCreated = dayCreated;
            Severity = severity;
        }

        public NewsId Id { get; }
        public VillageId Origin { get; }
        public VillageId About { get; }
        public NewsKind Kind { get; }
        public long DayCreated { get; }
        public int Severity { get; }
    }
}
