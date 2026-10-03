using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Assigns caller-owned evidence weight to one belief in stable evaluation order.</summary>
    public interface IEvidencePolicy
    {
        int Score(Belief belief, ActorId suspect);
    }

    /// <summary>Immutable bounded evidence total, explicit proof threshold and authorization outcome.</summary>
    public sealed class SuspicionResult
    {
        public SuspicionResult(int totalEvidence, int proofThreshold)
        {
            if (totalEvidence < 0 || totalEvidence > 100)
                throw new ArgumentOutOfRangeException(nameof(totalEvidence));
            if (proofThreshold < 1 || proofThreshold > 100)
                throw new ArgumentOutOfRangeException(nameof(proofThreshold));
            TotalEvidence = totalEvidence;
            ProofThreshold = proofThreshold;
        }

        public int TotalEvidence { get; }
        public int ProofThreshold { get; }
        public bool MayAct => TotalEvidence >= ProofThreshold;
    }

    /// <summary>Evaluates a named guard using only that guard's ordered belief store.</summary>
    public static class SuspicionEvaluator
    {
        public static SuspicionResult Evaluate(KnowledgeState knowledge, NpcId guard, ActorId suspect,
            int proofThreshold, IEvidencePolicy evidencePolicy)
        {
            if (knowledge == null) throw new ArgumentNullException(nameof(knowledge));
            if (!guard.IsValid) throw new ArgumentException("Suspicion needs a valid guard.", nameof(guard));
            if (!suspect.IsValid) throw new ArgumentException("Suspicion needs a valid suspect.", nameof(suspect));
            if (proofThreshold < 1 || proofThreshold > 100)
                throw new ArgumentOutOfRangeException(nameof(proofThreshold));
            if (evidencePolicy == null) throw new ArgumentNullException(nameof(evidencePolicy));
            if (!knowledge.TryGet(guard, out var store))
                throw new InvalidOperationException("The named guard needs a belief store.");

            var beliefs = store.Query();
            bool identifiesSuspect = false;
            foreach (Belief belief in beliefs)
                if (belief.Claim.Subject == suspect)
                {
                    identifiesSuspect = true;
                    break;
                }
            long total = 0;
            foreach (Belief belief in beliefs)
            {
                int score = evidencePolicy.Score(belief, suspect);
                if (score < -100 || score > 100)
                    throw new ArgumentOutOfRangeException(nameof(score),
                        "Evidence scores must be between -100 and 100.");
                total += score;
            }
            if (!identifiesSuspect) return new SuspicionResult(0, proofThreshold);
            int boundedTotal = total <= 0 ? 0 : total >= 100 ? 100 : (int)total;
            return new SuspicionResult(boundedTotal, proofThreshold);
        }
    }
}
