using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Immutable participants, location and caller-owned tuning for one conversation attempt.</summary>
    public sealed class ConversationContext
    {
        public ConversationContext(NpcId speaker, NpcId listener, LocationId location, int trustPercent,
            int plausibilityPercent, int confidenceLoss, int mutationChancePercent)
        {
            if (!speaker.IsValid) throw new ArgumentException("A conversation needs a valid speaker.", nameof(speaker));
            if (!listener.IsValid) throw new ArgumentException("A conversation needs a valid listener.", nameof(listener));
            if (speaker == listener) throw new ArgumentException("An NPC cannot converse with itself.", nameof(listener));
            if (!location.IsValid) throw new ArgumentException("A conversation needs a valid location.", nameof(location));
            ValidatePercent(trustPercent, nameof(trustPercent));
            ValidatePercent(plausibilityPercent, nameof(plausibilityPercent));
            ValidatePercent(confidenceLoss, nameof(confidenceLoss));
            ValidatePercent(mutationChancePercent, nameof(mutationChancePercent));

            Speaker = speaker;
            Listener = listener;
            Location = location;
            TrustPercent = trustPercent;
            PlausibilityPercent = plausibilityPercent;
            ConfidenceLoss = confidenceLoss;
            MutationChancePercent = mutationChancePercent;
        }

        public NpcId Speaker { get; }
        public NpcId Listener { get; }
        public LocationId Location { get; }
        public int TrustPercent { get; }
        public int PlausibilityPercent { get; }
        public int ConfidenceLoss { get; }
        public int MutationChancePercent { get; }

        private static void ValidatePercent(int value, string parameterName)
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>Chooses eligible speaker beliefs by caller-defined salience.</summary>
    public interface IRumorSelectionPolicy
    {
        bool IsEligible(Belief belief, ConversationContext context);
        int Salience(Belief belief, ConversationContext context);
    }

    /// <summary>Supplies a structured transformation when the seeded mutation roll succeeds.</summary>
    public interface IRumorDistortionPolicy
    {
        BeliefClaim Distort(BeliefClaim claim, ConversationContext context);
    }

    /// <summary>Reports the selected belief, conversation truth and any newly adopted rumor.</summary>
    public sealed class ConversationResult
    {
        internal ConversationResult(WorldEvent conversation, Belief selectedBelief, Belief adoptedBelief,
            bool wasMutated)
        {
            Conversation = conversation;
            SelectedBelief = selectedBelief;
            AdoptedBelief = adoptedBelief;
            WasMutated = wasMutated;
        }

        public WorldEvent Conversation { get; }
        public Belief SelectedBelief { get; }
        public Belief AdoptedBelief { get; }
        public bool WasMutated { get; }
    }

    /// <summary>Passes one eligible speaker belief through a traceable, seeded conversation.</summary>
    public static class RumorExchange
    {
        public static ConversationResult Share(WorldState state, ConversationContext context,
            IRumorSelectionPolicy selectionPolicy, IRumorDistortionPolicy distortionPolicy)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (selectionPolicy == null) throw new ArgumentNullException(nameof(selectionPolicy));
            if (distortionPolicy == null) throw new ArgumentNullException(nameof(distortionPolicy));
            if (!state.Knowledge.TryGet(context.Speaker, out var speakerStore))
                throw new InvalidOperationException("The conversation speaker needs a knowledge store.");
            if (!state.Knowledge.TryGet(context.Listener, out var listenerStore))
                throw new InvalidOperationException("The conversation listener needs a knowledge store.");

            Belief selected = Select(speakerStore, context, selectionPolicy);
            if (selected == null) return new ConversationResult(null, null, null, false);

            bool wasMutated = state.Rng.NextInt(100) < context.MutationChancePercent;
            BeliefClaim sharedClaim = selected.Claim;
            if (wasMutated)
            {
                sharedClaim = distortionPolicy.Distort(selected.Claim, context);
                if (!IsSafeDistortion(selected.Claim, sharedClaim))
                    throw new InvalidOperationException("A distortion may remove detail but cannot invent or replace facts.");
            }

            int confidence = CalculateConfidence(selected.Confidence, context);
            var sourceChain = new List<NpcId>(selected.Source.SourceChain) { context.Speaker };
            var source = new BeliefSource(BeliefSourceKind.ToldBy, context.Speaker,
                selected.Source.OriginEventId, sourceChain);
            Belief candidate = confidence == 0 ? null : new Belief(sharedClaim, source, confidence, state.Clock);
            Belief existing = Find(listenerStore, sharedClaim);
            bool shouldAdopt = candidate != null && (existing == null || candidate.Confidence > existing.Confidence);
            if (shouldAdopt && candidate.LearnedAt < existing?.LearnedAt)
                throw new InvalidOperationException("A rumor cannot replace a newer belief.");

            WorldEvent conversation = state.Events.Append(state.Clock, context.Location,
                WorldEventType.Conversation, ActorId.ForNpc(context.Speaker),
                new[] { ActorId.ForNpc(context.Listener) }, EventVisibility.Quiet);
            if (shouldAdopt)
            {
                listenerStore.Set(candidate);
                RecordHeardWrongdoing(state, context.Listener, candidate);
            }
            return new ConversationResult(conversation, selected, shouldAdopt ? candidate : null, wasMutated);
        }

        private static Belief Select(BeliefStore speakerStore, ConversationContext context,
            IRumorSelectionPolicy policy)
        {
            Belief selected = null;
            int bestSalience = 0;
            foreach (Belief belief in speakerStore.Query())
            {
                if (CreatesLoop(belief.Source, context) || !policy.IsEligible(belief, context)) continue;
                int salience = policy.Salience(belief, context);
                if (selected == null || salience > bestSalience)
                {
                    selected = belief;
                    bestSalience = salience;
                }
            }
            return selected;
        }

        private static bool CreatesLoop(BeliefSource source, ConversationContext context)
        {
            if (source.Speaker == context.Listener || source.Speaker == context.Speaker) return true;
            foreach (NpcId npc in source.SourceChain)
                if (npc == context.Listener || npc == context.Speaker) return true;
            return false;
        }

        private static int CalculateConfidence(int speakerConfidence, ConversationContext context)
        {
            long weighted = (long)speakerConfidence * context.TrustPercent * context.PlausibilityPercent / 10000;
            return Math.Max(0, (int)weighted - context.ConfidenceLoss);
        }

        /// <summary>
        /// A rumor about someone's wrongdoing that is believed with high confidence is
        /// remembered second-hand: half the importance of witnessing it (the divisor is 2),
        /// and no first-hand shift, so hearsay is remembered but never stings on sight —
        /// the recall bound stays zero because there was no direct experience to relive.
        /// </summary>
        private const int HeardWrongdoingMinConfidence = 60;
        private const int HeardWrongdoingImportanceDivisor = 2;

        private static void RecordHeardWrongdoing(WorldState state, NpcId listener, Belief adopted)
        {
            if (adopted.Claim.Kind != BeliefClaimKind.WrongedBy) return;
            if (adopted.Confidence < HeardWrongdoingMinConfidence) return;
            if (!adopted.Claim.Subject.HasValue) return;
            ActorId subject = adopted.Claim.Subject.Value;
            if (subject.IsPlayer || !subject.Npc.HasValue) return;
            if (!state.Knowledge.TryGet(listener, out _)) return; // Nowhere to remember.
            int importance = Math.Max(1, adopted.Confidence / HeardWrongdoingImportanceDivisor);
            state.Knowledge.GetMemories(listener).Remember(adopted.Claim, importance, state.Clock,
                adopted.Source.OriginEventId);
            state.Knowledge.RecordAttributedMemory(listener, adopted.Claim, 0, 0);
        }

        private static bool IsSafeDistortion(BeliefClaim original, BeliefClaim distorted)
        {
            return distorted != null && original.Kind == distorted.Kind && original.Location == distorted.Location &&
                IsRetainedOrRemoved(original.ItemType, distorted.ItemType) &&
                IsRetainedOrRemoved(original.Subject, distorted.Subject) &&
                IsRetainedOrRemoved(original.Quantity, distorted.Quantity);
        }

        private static bool IsRetainedOrRemoved<T>(T? original, T? distorted) where T : struct, IEquatable<T>
        {
            return !distorted.HasValue || original.HasValue && distorted.Value.Equals(original.Value);
        }

        private static Belief Find(BeliefStore store, BeliefClaim claim)
        {
            foreach (Belief belief in store.Query())
                if (belief.Claim.Equals(claim)) return belief;
            return null;
        }
    }
}
