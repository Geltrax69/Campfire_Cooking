using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// The village's evening room: once a day at 20:00, every NPC whose schedule has
    /// them socializing at the tavern meets someone. Each NPC still unmet tonight picks
    /// one partner with <see cref="ConversationChoice"/> (affection-weighted, so friends
    /// seek each other out) and shares their most newsworthy belief through
    /// <see cref="RumorExchange"/> with the relationship-based
    /// <see cref="RumorTrustFilter"/> — a warning from a trusted friend lands, the same
    /// story from a distrusted stranger dies. Pairs are processed in deterministic ID
    /// order on the world's seeded RNG; everyone meets at most once per evening.
    /// </summary>
    public sealed class MeetingSystem : IWorldSystem
    {
        private static readonly LocationId Tavern = new LocationId("loc_tavern");
        private const int MeetingHour = 20;

        /// <summary>Not everything is equally plausible second-hand.</summary>
        private const int PlausibilityPercent = 80;

        /// <summary>Each retelling costs a little confidence: the telephone game.</summary>
        private const int ConfidenceLoss = 5;

        /// <summary>One retelling in ten mutates the claim.</summary>
        private const int MutationChancePercent = 10;

        public string Id => "knowledge.meetings";
        public SimulationPhase Phase => SimulationPhase.Social;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Clock.Hour != MeetingHour || state.Clock.Minute != 0) return;
            List<NpcId> present = PresentAtTavern(state);
            var met = new HashSet<NpcId>();
            foreach (NpcId npc in present)
            {
                if (met.Contains(npc)) continue;
                List<NpcId> candidates = new List<NpcId>();
                foreach (NpcId other in present)
                    if (other != npc && !met.Contains(other)) candidates.Add(other);
                if (candidates.Count == 0) continue;
                NpcId partner = ConversationChoice.ChoosePartner(state, npc, candidates);
                ConversationContext context = RumorTrustFilter.ForConversation(state,
                    npc, partner, Tavern, PlausibilityPercent, ConfidenceLoss, MutationChancePercent);
                ConversationResult result = RumorExchange.Share(state, context,
                    new VillageRumorPolicy(), new NoDistortionPolicy());
                if (result.Conversation == null)
                {
                    // They met and talked, even if neither had news worth repeating.
                    // The meeting itself is the social event: the dynamics system
                    // shifts affection for tavern talk regardless of rumors shared.
                    state.Events.Append(state.Clock, Tavern, WorldEventType.Conversation,
                        ActorId.ForNpc(npc), new[] { ActorId.ForNpc(partner) },
                        EventVisibility.Quiet);
                }
                met.Add(npc);
                met.Add(partner);
            }
        }

        private static List<NpcId> PresentAtTavern(WorldState state)
        {
            var present = new List<NpcId>();
            foreach (NpcState npc in state.Npcs.Npcs)
            {
                ScheduleEntry entry = npc.Definition.Schedule.At(state.Clock);
                if (entry != null && entry.Kind == ActivityKind.Socialize && entry.Destination == Tavern)
                    present.Add(npc.Definition.Id);
            }
            present.Sort((left, right) => string.Compare(left.Value, right.Value, StringComparison.Ordinal));
            return present;
        }

        /// <summary>
        /// Villagers share what matters: wrongdoing first, then shortages, then good
        /// news. Routine sightings ("X was at the tavern") and everyday fair trades
        /// are not worth repeating.
        /// </summary>
        private sealed class VillageRumorPolicy : IRumorSelectionPolicy
        {
            public bool IsEligible(Belief belief, ConversationContext context)
            {
                if (belief == null || context == null) return false;
                switch (belief.Claim.Kind)
                {
                    case BeliefClaimKind.Presence:
                    case BeliefClaimKind.FairTradeWith:
                        return false;
                    default:
                        return true;
                }
            }

            public int Salience(Belief belief, ConversationContext context)
            {
                int kindWeight;
                switch (belief.Claim.Kind)
                {
                    case BeliefClaimKind.TheftObserved: kindWeight = 1000; break;
                    case BeliefClaimKind.WrongedBy: kindWeight = 800; break;
                    case BeliefClaimKind.StockMissing: kindWeight = 500; break;
                    case BeliefClaimKind.GiftFrom: kindWeight = 100; break;
                    default: kindWeight = 0; break;
                }
                return kindWeight + belief.Confidence;
            }
        }

        private sealed class NoDistortionPolicy : IRumorDistortionPolicy
        {
            public BeliefClaim Distort(BeliefClaim claim, ConversationContext context) => claim;
        }
    }
}
