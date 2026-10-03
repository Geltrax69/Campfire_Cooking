using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Describes how an NPC acquired a belief.</summary>
    public enum BeliefSourceKind { Seen, ToldBy, Inferred }

    /// <summary>Immutable provenance with an optional truth reference and defensively copied rumor chain.</summary>
    public sealed class BeliefSource
    {
        public BeliefSource(BeliefSourceKind kind, NpcId? speaker = null, WorldEventId? originEventId = null,
            IEnumerable<NpcId> sourceChain = null)
        {
            if (kind < BeliefSourceKind.Seen || kind > BeliefSourceKind.Inferred)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (speaker.HasValue && !speaker.Value.IsValid) throw new ArgumentException("Invalid speaker.", nameof(speaker));
            if (originEventId.HasValue && !originEventId.Value.IsValid)
                throw new ArgumentException("Invalid origin event.", nameof(originEventId));
            if (kind == BeliefSourceKind.ToldBy && !speaker.HasValue)
                throw new ArgumentException("ToldBy provenance requires a speaker.", nameof(speaker));
            if (kind != BeliefSourceKind.ToldBy && speaker.HasValue)
                throw new ArgumentException("Only ToldBy provenance has a speaker.", nameof(speaker));

            var copiedChain = new List<NpcId>();
            var unique = new HashSet<NpcId>();
            if (sourceChain != null)
                foreach (var npc in sourceChain)
                {
                    if (!npc.IsValid) throw new ArgumentException("Source chains need valid NPC IDs.", nameof(sourceChain));
                    if (!unique.Add(npc)) throw new ArgumentException("Source chains cannot repeat an NPC.", nameof(sourceChain));
                    copiedChain.Add(npc);
                }
            Kind = kind;
            Speaker = speaker;
            OriginEventId = originEventId;
            SourceChain = copiedChain.AsReadOnly();
        }

        public BeliefSourceKind Kind { get; }
        public NpcId? Speaker { get; }
        public WorldEventId? OriginEventId { get; }
        public IReadOnlyList<NpcId> SourceChain { get; }
    }

    /// <summary>Immutable claim, provenance, explicit confidence and acquisition time.</summary>
    public sealed class Belief
    {
        public Belief(BeliefClaim claim, BeliefSource source, int confidence, GameTime learnedAt)
        {
            if (claim == null) throw new ArgumentNullException(nameof(claim));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (confidence < 0 || confidence > 100) throw new ArgumentOutOfRangeException(nameof(confidence));
            Claim = claim;
            Source = source;
            Confidence = confidence;
            LearnedAt = learnedAt;
        }

        public BeliefClaim Claim { get; }
        public BeliefSource Source { get; }
        public int Confidence { get; }
        public GameTime LearnedAt { get; }
    }
}
