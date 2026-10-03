using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Recalls attributed interaction memories ("Ralf cheated me", "Bessa gave me bread") when
    /// their subject is perceived again, and lets the past nudge the present: each recall moves
    /// trust/affection by ±1 in the memory's original direction, scaled by the memory's
    /// current (decayed) strength — a fresh betrayal stings, a faded one barely nudges.
    ///
    /// Recall trigger (documented choice): perception. When an NPC holds a Seen belief tracing
    /// back to a newly processed event and that belief's claim names the memory's subject,
    /// the NPC has perceived the actor and the memory resurfaces. Rumor-based recall
    /// ("considering" the actor without seeing them) is a later task.
    ///
    /// Ordering: this system id ("knowledge.memory-recall") sorts before
    /// "knowledge.relationship-dynamics" inside the Social phase, so each tick it only
    /// recalls memories formed on earlier ticks. A fresh shift is remembered this tick and
    /// can first be re-felt when its subject is perceived on a later tick — never twice
    /// for the same event.
    ///
    /// No runaway feedback: the nudge is silent (no event logged) and never creates a new
    /// attributed memory, so reinforcement cannot amplify itself. Total recall-driven
    /// movement per axis is bounded by the original shift's magnitude (first capture wins),
    /// so shift + recalls move a pair at most double the original shift.
    /// </summary>
    public sealed class MemoryRecallSystem : IWorldSystem
    {
        public string Id => "knowledge.memory-recall";
        public SimulationPhase Phase => SimulationPhase.Social;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (WorldEvent worldEvent in state.Events.Query())
            {
                if (worldEvent.Id.Value <= state.Knowledge.RecallCursor) continue;
                RecallForEvent(state, worldEvent);
                state.Knowledge.RecallCursor = worldEvent.Id.Value;
            }
        }

        private static void RecallForEvent(WorldState state, WorldEvent worldEvent)
        {
            // Reason: stores iterate in deterministic NPC order and memories in claim order.
            foreach (MemoryStore memories in state.Knowledge.MemoryStores)
            {
                NpcId owner = memories.Owner;
                if (!state.Knowledge.TryGet(owner, out BeliefStore beliefs)) continue;
                foreach (Memory memory in memories.Query())
                {
                    if (!AttributedMemory.IsAttributedKind(memory.Claim.Kind)) continue;
                    if (!TryMemorySubject(memory.Claim, out NpcId subject)) continue;
                    if (!PerceivedSubject(beliefs, subject, worldEvent.Id)) continue;
                    ApplyRecall(state, owner, memory);
                }
            }
        }

        private static bool TryMemorySubject(BeliefClaim claim, out NpcId subject)
        {
            subject = default;
            if (!claim.Subject.HasValue) return false;
            ActorId actor = claim.Subject.Value;
            if (actor.IsPlayer || !actor.Npc.HasValue) return false;
            subject = actor.Npc.Value;
            return true;
        }

        private static bool PerceivedSubject(BeliefStore beliefs, NpcId subject, WorldEventId eventId)
        {
            // Reason: recall is triggered by the NPC's own perception (a Seen belief tracing
            // to this event that names the subject), never by reading world truth: an NPC
            // cannot be reminded of someone they did not notice.
            foreach (Belief belief in beliefs.Query())
            {
                if (belief.Source.Kind != BeliefSourceKind.Seen) continue;
                if (!belief.Source.OriginEventId.HasValue ||
                    belief.Source.OriginEventId.Value != eventId) continue;
                if (!belief.Claim.Subject.HasValue) continue;
                ActorId actor = belief.Claim.Subject.Value;
                if (!actor.IsPlayer && actor.Npc.HasValue && actor.Npc.Value == subject) return true;
            }
            return false;
        }

        private static void ApplyRecall(WorldState state, NpcId owner, Memory memory)
        {
            // Reason: strength-scaled recall — a memory nudges only while it still holds at
            // least half its importance; a faded (or pruned) memory does not resurface.
            if (memory.Strength * 2 < memory.Importance) return;
            if (!state.Knowledge.TryGetAttributedMemory(owner, memory.Claim, out AttributedMemory record))
                return;
            int trustNudge = NudgeStep(record.OriginalTrustDelta, record.RecalledTrustDelta);
            int affectionNudge = NudgeStep(record.OriginalAffectionDelta, record.RecalledAffectionDelta);
            if (trustNudge == 0 && affectionNudge == 0) return; // Recall bound reached: the past has said its piece.
            RelationshipRegistry registry = state.Knowledge.Relationships;
            if (!registry.TryGet(owner, record.Subject, out Relationship current)) return;
            int newTrust = Clamp(current.Trust + trustNudge);
            int newAffection = Clamp(current.Affection + affectionNudge);
            int trustMoved = newTrust - current.Trust;
            int affectionMoved = newAffection - current.Affection;
            if (trustMoved == 0 && affectionMoved == 0) return; // Clamped at the edge: nothing moved.
            registry.Set(new Relationship(owner, record.Subject, newTrust, newAffection, current.Reason));
            state.Knowledge.AddRecallMovement(owner, memory.Claim, trustMoved, affectionMoved);
        }

        private static int NudgeStep(int original, int recalled)
        {
            // Reason: each recall moves one point toward the bound, never past it. From
            // recalled = 0 this keeps |recalled| <= |original| by induction, which is the
            // whole anti-runaway proof: recalls alone can at most double the original shift.
            if (original > 0 && recalled < original) return 1;
            if (original < 0 && recalled > original) return -1;
            return 0;
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));
    }
}
