using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Recall accounting for one attributed interaction memory: the relationship shift that
    /// formed the memory (the "original shift") and how far recalling the memory has moved
    /// the relationship since. The anti-runaway bound is enforced per axis: total
    /// recall-driven movement may never exceed the original shift's magnitude, so the
    /// combined effect of a shift and all its recalls is at most double the original shift.
    /// </summary>
    public sealed class AttributedMemory
    {
        internal AttributedMemory(NpcId owner, BeliefClaim claim, int originalTrustDelta,
            int originalAffectionDelta)
        {
            if (!owner.IsValid) throw new ArgumentException("An attributed memory needs a valid owner.", nameof(owner));
            if (claim == null) throw new ArgumentNullException(nameof(claim));
            if (!IsAttributedKind(claim.Kind))
                throw new ArgumentException("Only attributed interaction claims can back an attributed memory.", nameof(claim));
            if (!claim.Subject.HasValue || claim.Subject.Value.IsPlayer || !claim.Subject.Value.Npc.HasValue)
                throw new ArgumentException("An attributed memory names a specific NPC actor.", nameof(claim));

            Owner = owner;
            Claim = claim;
            OriginalTrustDelta = originalTrustDelta;
            OriginalAffectionDelta = originalAffectionDelta;
        }

        public NpcId Owner { get; }
        public BeliefClaim Claim { get; }

        /// <summary>The NPC the memory is about (the claim's subject).</summary>
        public NpcId Subject => Claim.Subject.Value.Npc.Value;

        /// <summary>How far the forming shift moved trust; the recall bound for this axis.</summary>
        public int OriginalTrustDelta { get; }

        /// <summary>How far the forming shift moved affection; the recall bound for this axis.</summary>
        public int OriginalAffectionDelta { get; }

        /// <summary>Total trust movement caused by recalls of this memory so far.</summary>
        public int RecalledTrustDelta { get; private set; }

        /// <summary>Total affection movement caused by recalls of this memory so far.</summary>
        public int RecalledAffectionDelta { get; private set; }

        /// <summary>
        /// Records actual recall-driven movement. Only the recall system calls this, after it
        /// has checked the per-axis bound, so the invariant |recalled| &lt;= |original| holds.
        /// </summary>
        internal void AddRecall(int trustMoved, int affectionMoved)
        {
            RecalledTrustDelta += trustMoved;
            RecalledAffectionDelta += affectionMoved;
        }

        internal static bool IsAttributedKind(BeliefClaimKind kind) =>
            kind == BeliefClaimKind.WrongedBy || kind == BeliefClaimKind.GiftFrom ||
            kind == BeliefClaimKind.FairTradeWith;
    }
}
