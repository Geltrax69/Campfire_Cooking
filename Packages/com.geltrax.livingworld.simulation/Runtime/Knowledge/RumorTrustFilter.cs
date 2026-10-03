using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Wires rumor exchange to the relationship registry: a listener believes a speaker's
    /// rumor in proportion to their directed trust in the speaker. Adopted confidence is
    /// speakerConfidence × trust/100 × plausibility/100 − loss (see RumorExchange); below
    /// the trust floor the listener dismisses the story outright, so the adopted
    /// confidence is zero and nothing is learned. A warning from a trusted friend lands;
    /// the same story from a distrusted stranger dies.
    /// </summary>
    public static class RumorTrustFilter
    {
        /// <summary>
        /// Below this trust, a listener dismisses the speaker's stories outright instead of
        /// half-believing them: distrust is active rejection, not just weak trust.
        /// </summary>
        public const int TrustFloor = 25;

        /// <summary>
        /// The trust percent a rumor exchange should use: the listener's directed trust in
        /// the speaker (strangers start neutral at 50), or zero below the trust floor.
        /// </summary>
        public static int TrustPercentFor(WorldState state, NpcId listener, NpcId speaker)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int trust = state.Knowledge.Relationships.Trust(listener, speaker);
            return trust < TrustFloor ? 0 : trust;
        }

        /// <summary>
        /// Builds a conversation context whose trust comes from the listener's actual
        /// directed trust in the speaker instead of a caller-supplied number.
        /// </summary>
        public static ConversationContext ForConversation(WorldState state, NpcId speaker, NpcId listener,
            LocationId location, int plausibilityPercent, int confidenceLoss, int mutationChancePercent)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new ConversationContext(speaker, listener, location,
                TrustPercentFor(state, listener, speaker),
                plausibilityPercent, confidenceLoss, mutationChancePercent);
        }
    }
}
