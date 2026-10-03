using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Moves directed NPC trust/affection by small, bounded, explainable rules driven only
    /// by world events (never by script): honest trades, gifts, tavern conversations,
    /// witnessed thefts and missed debt repayments shift pairs, and every pair slowly
    /// decays back toward its Content baseline. Runs in the Social phase, after Perception,
    /// so witness beliefs from this tick are already recorded. Every interaction shift
    /// appends a quiet RelationshipShift event; decay is silent background equilibration.
    /// Event conventions this system reads (emitters own the events, this system only reacts):
    /// Purchase = completed honest trade, actor buyer, targets[0] seller;
    /// Gift = actor giver, targets[0] receiver (no emitter yet — the rule waits for one);
    /// Conversation at the tavern = actor speaker, targets[0] listener;
    /// Theft = actor perpetrator, witnesses found via Seen perception records;
    /// DebtMissed = actor debtor, targets[0] creditor (P2-10 debts will emit this).
    /// </summary>
    public sealed class RelationshipDynamicsSystem : IWorldSystem
    {
        // The village's evening room, from the approved Content/world/locations.json.
        private static readonly LocationId Tavern = new LocationId("loc_tavern");

        // Shift magnitudes: every interaction shift stays within ±1..5 per the P2-02 brief.
        private const int HonestTradeTrustGain = 2; // buyer->seller and seller->buyer
        private const int GiftAffectionGain = 3; // receiver->giver
        private const int GiftTrustGain = 1; // receiver->giver
        private const int TavernChatAffectionGain = 1; // both directions
        private const int DebtMissedTrustLoss = 3; // creditor->debtor
        // Witnessed theft: drop = TheftBaseDrop + priorTrust * TheftTrustFactor / 100,
        // so a stranger's theft costs 1 trust and a trusted friend's costs 5.
        private const int TheftBaseDrop = 1;
        private const int TheftTrustFactor = 4;
        // Decay: each in-game day, every shifted axis moves this many points toward baseline.
        private const int DecayPointsPerDay = 1;

        private const string StrangerBaselineReason = "Strangers";
        private const string LatelySeparator = " — lately: ";

        public string Id => "knowledge.relationship-dynamics";
        public SimulationPhase Phase => SimulationPhase.Social;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            IReadOnlyList<WorldEvent> events = state.Events.Query();
            foreach (WorldEvent worldEvent in events)
            {
                if (worldEvent.Id.Value <= state.Knowledge.DynamicsCursor) continue;
                ApplyEventRule(state, worldEvent);
                state.Knowledge.DynamicsCursor = worldEvent.Id.Value;
            }
            ApplyDailyDecay(state);
        }

        private static void ApplyEventRule(WorldState state, WorldEvent worldEvent)
        {
            // Reason: one dispatch per event kind keeps every rule independently testable.
            switch (worldEvent.Type)
            {
                case WorldEventType.Purchase:
                    ApplyHonestTrade(state, worldEvent);
                    break;
                case WorldEventType.Gift:
                    ApplyGift(state, worldEvent);
                    break;
                case WorldEventType.Conversation:
                    ApplyTavernConversation(state, worldEvent);
                    break;
                case WorldEventType.Theft:
                    ApplyWitnessedTheft(state, worldEvent);
                    break;
                case WorldEventType.DebtMissed:
                    ApplyDebtMissed(state, worldEvent);
                    break;
            }
        }

        private static void ApplyHonestTrade(WorldState state, WorldEvent trade)
        {
            // Reason: completing a trade honestly proves both sides keep their word.
            // Only completed (full) purchases count; partial and failed ones do not.
            if (!TryNpcPair(trade.Actor, FirstTarget(trade), out NpcId buyer, out NpcId seller)) return;
            string cause = "honest trade at " + trade.Location.Value;
            ApplyShift(state, trade, buyer, seller, HonestTradeTrustGain, 0, cause);
            ApplyShift(state, trade, seller, buyer, HonestTradeTrustGain, 0, cause);
        }

        private static void ApplyGift(WorldState state, WorldEvent gift)
        {
            // Reason: a freely given gift warms the receiver toward the giver.
            if (!TryNpcPair(gift.Actor, FirstTarget(gift), out NpcId giver, out NpcId receiver)) return;
            ApplyShift(state, gift, receiver, giver, GiftTrustGain, GiftAffectionGain,
                "received a gift from " + giver.Value);
        }

        private static void ApplyTavernConversation(WorldState state, WorldEvent conversation)
        {
            // Reason: shared time over ale builds fondness; the tavern is the village's social room.
            if (conversation.Location != Tavern) return;
            if (!TryNpcPair(conversation.Actor, FirstTarget(conversation),
                out NpcId speaker, out NpcId listener)) return;
            string cause = "evening talk at the tavern with ";
            ApplyShift(state, conversation, speaker, listener, 0, TavernChatAffectionGain, cause + listener.Value);
            ApplyShift(state, conversation, listener, speaker, 0, TavernChatAffectionGain, cause + speaker.Value);
        }

        private static void ApplyWitnessedTheft(WorldState state, WorldEvent theft)
        {
            // Reason: trust falls only for NPCs who actually perceived the theft (Seen belief
            // tracing back to this event), never from world truth alone. Betrayal by a
            // trusted friend hurts more than by a stranger: the drop scales with prior trust.
            if (!theft.Actor.HasValue || theft.Actor.Value.IsPlayer) return;
            NpcId thief = theft.Actor.Value.Npc.Value;
            ActorId thiefActor = theft.Actor.Value;
            foreach (BeliefStore store in state.Knowledge.Stores)
            {
                NpcId witness = store.Owner;
                if (witness == thief || !SawTheft(store, thiefActor, theft.Id)) continue;
                int priorTrust = state.Knowledge.Relationships.Trust(witness, thief);
                int drop = TheftBaseDrop + priorTrust * TheftTrustFactor / 100;
                ApplyShift(state, theft, witness, thief, -drop, 0,
                    "saw " + thief.Value + " steal at " + theft.Location.Value);
            }
        }

        private static void ApplyDebtMissed(WorldState state, WorldEvent missed)
        {
            // Reason: an unpaid debt breaks the lender's trust in the borrower.
            if (!TryNpcPair(missed.Actor, FirstTarget(missed), out NpcId debtor, out NpcId creditor)) return;
            ApplyShift(state, missed, creditor, debtor, -DebtMissedTrustLoss, 0,
                debtor.Value + " missed a debt repayment");
        }

        private static bool SawTheft(BeliefStore store, ActorId thief, WorldEventId theftId)
        {
            foreach (Belief belief in store.Query(kind: BeliefClaimKind.TheftObserved, subject: thief))
                if (belief.Source.Kind == BeliefSourceKind.Seen &&
                    belief.Source.OriginEventId.HasValue &&
                    belief.Source.OriginEventId.Value == theftId)
                    return true;
            return false;
        }

        private static void ApplyShift(WorldState state, WorldEvent trigger, NpcId from, NpcId to,
            int trustDelta, int affectionDelta, string cause)
        {
            // Reason: shifts are clamped to 0–100 and always explain themselves in the reason.
            EnsureBaseline(state, from, to);
            state.Knowledge.TryGetRelationshipBaseline(from, to, out RelationshipBaseline baseline);
            RelationshipRegistry registry = state.Knowledge.Relationships;
            int newTrust = Clamp(registry.Trust(from, to) + trustDelta);
            int newAffection = Clamp(registry.Affection(from, to) + affectionDelta);
            if (newTrust == registry.Trust(from, to) && newAffection == registry.Affection(from, to))
                return; // Clamped at the edge: nothing moved, so nothing is logged.
            registry.Set(new Relationship(from, to, newTrust, newAffection,
                baseline.Reason + LatelySeparator + cause));
            // The shift happens now: the event log requires non-decreasing times, and the
            // trigger's time can predate later log entries, so the shift is stamped with the
            // current clock (which never precedes any logged event time).
            state.Events.Append(state.Clock, trigger.Location, WorldEventType.RelationshipShift,
                ActorId.ForNpc(from), new[] { ActorId.ForNpc(to) }, EventVisibility.Quiet);
        }

        private static void EnsureBaseline(WorldState state, NpcId from, NpcId to)
        {
            // Reason: the first sight of a pair is its Content value (or the stranger
            // default for a new pair) — that is what decay drifts back toward.
            if (state.Knowledge.TryGetRelationshipBaseline(from, to, out _)) return;
            RelationshipRegistry registry = state.Knowledge.Relationships;
            if (registry.TryGet(from, to, out Relationship existing))
                state.Knowledge.CaptureRelationshipBaseline(from, to,
                    existing.Trust, existing.Affection, existing.Reason);
            else
                state.Knowledge.CaptureRelationshipBaseline(from, to,
                    RelationshipRegistry.StrangerTrust, RelationshipRegistry.StrangerAffection,
                    StrangerBaselineReason);
        }

        private static void ApplyDailyDecay(WorldState state)
        {
            // Reason: feelings cool or heal with time when nothing reinforces them; decay is
            // silent equilibration, so it logs no events — the interaction shifts carry the story.
            long day = state.Clock.Day;
            if (state.Knowledge.LastDynamicsDay == 0)
            {
                state.Knowledge.LastDynamicsDay = day;
                return;
            }
            if (day <= state.Knowledge.LastDynamicsDay) return;
            foreach (RelationshipBaseline baseline in state.Knowledge.CaptureRelationshipBaselines())
            {
                RelationshipRegistry registry = state.Knowledge.Relationships;
                if (!registry.TryGet(baseline.From, baseline.To, out Relationship current)) continue;
                int trust = MoveToward(current.Trust, baseline.Trust, DecayPointsPerDay);
                int affection = MoveToward(current.Affection, baseline.Affection, DecayPointsPerDay);
                if (trust == current.Trust && affection == current.Affection) continue;
                bool fullyDecayed = trust == baseline.Trust && affection == baseline.Affection;
                registry.Set(new Relationship(baseline.From, baseline.To, trust, affection,
                    fullyDecayed ? baseline.Reason : current.Reason));
            }
            state.Knowledge.LastDynamicsDay = day;
        }

        private static int MoveToward(int value, int target, int step)
        {
            if (value < target) return Math.Min(target, value + step);
            if (value > target) return Math.Max(target, value - step);
            return value;
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));

        private static ActorId? FirstTarget(WorldEvent worldEvent) =>
            worldEvent.Targets.Count > 0 ? (ActorId?)worldEvent.Targets[0] : null;

        private static bool TryNpcPair(ActorId? first, ActorId? second, out NpcId from, out NpcId to)
        {
            // Reason: relationships only track NPC↔NPC feelings; the player is not in the registry.
            from = default;
            to = default;
            if (!first.HasValue || first.Value.IsPlayer || !second.HasValue || second.Value.IsPlayer)
                return false;
            from = first.Value.Npc.Value;
            to = second.Value.Npc.Value;
            return from != to;
        }
    }
}
