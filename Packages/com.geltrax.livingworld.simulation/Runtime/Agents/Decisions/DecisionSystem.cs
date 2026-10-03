using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Controls deterministic resolution when activities have equal utility.</summary>
    public enum TieBreakBehavior { SeededRandom }

    /// <summary>Immutable utility coefficients supplied by approved scenario tuning.</summary>
    public sealed class ActivityUtilityWeights
    {
        public ActivityUtilityWeights(int baseScore, int scheduleFit, int hungerUrgency,
            int energyUrgency, int socialUrgency)
        {
            if (baseScore < 0) throw new ArgumentOutOfRangeException(nameof(baseScore));
            if (scheduleFit < 0) throw new ArgumentOutOfRangeException(nameof(scheduleFit));
            if (hungerUrgency < 0) throw new ArgumentOutOfRangeException(nameof(hungerUrgency));
            if (energyUrgency < 0) throw new ArgumentOutOfRangeException(nameof(energyUrgency));
            if (socialUrgency < 0) throw new ArgumentOutOfRangeException(nameof(socialUrgency));
            BaseScore = baseScore;
            ScheduleFit = scheduleFit;
            HungerUrgency = hungerUrgency;
            EnergyUrgency = energyUrgency;
            SocialUrgency = socialUrgency;
        }

        public int BaseScore { get; }
        public int ScheduleFit { get; }
        public int HungerUrgency { get; }
        public int EnergyUrgency { get; }
        public int SocialUrgency { get; }
    }

    /// <summary>Caller-supplied penalty for an activity when an owned belief meets a confidence threshold.</summary>
    public sealed class BeliefUtilityAdjustment
    {
        public BeliefUtilityAdjustment(ActivityKind activity, BeliefClaim claim,
            int minimumConfidence, int penalty)
        {
            ScheduleEntry.ValidateKind(activity);
            if (claim == null) throw new ArgumentNullException(nameof(claim));
            if (minimumConfidence < 0 || minimumConfidence > 100)
                throw new ArgumentOutOfRangeException(nameof(minimumConfidence));
            if (penalty <= 0) throw new ArgumentOutOfRangeException(nameof(penalty));
            Activity = activity;
            Claim = claim;
            MinimumConfidence = minimumConfidence;
            Penalty = penalty;
        }

        public ActivityKind Activity { get; }
        public BeliefClaim Claim { get; }
        public int MinimumConfidence { get; }
        public int Penalty { get; }
    }

    /// <summary>Immutable, owner-labelled belief view supplied for one NPC decision.</summary>
    public sealed class NpcBeliefSnapshot
    {
        public NpcBeliefSnapshot(NpcId owner, IEnumerable<Belief> beliefs)
        {
            if (!owner.IsValid) throw new ArgumentException("A belief snapshot needs a valid owner.", nameof(owner));
            if (beliefs == null) throw new ArgumentNullException(nameof(beliefs));
            var copied = new List<Belief>();
            foreach (Belief belief in beliefs)
            {
                if (belief == null) throw new ArgumentException("Beliefs cannot contain null.", nameof(beliefs));
                copied.Add(belief);
            }
            copied.Sort((left, right) => left.Claim.CompareTo(right.Claim));
            for (int index = 1; index < copied.Count; index++)
                if (copied[index - 1].Claim.Equals(copied[index].Claim))
                    throw new ArgumentException("A belief snapshot cannot repeat a claim.", nameof(beliefs));
            Owner = owner;
            Beliefs = copied.AsReadOnly();
        }

        public NpcId Owner { get; }
        public IReadOnlyList<Belief> Beliefs { get; }
    }

    /// <summary>Validated, caller-supplied utility values with no production balancing defaults.</summary>
    public sealed class DecisionTuning
    {
        private readonly IReadOnlyDictionary<ActivityKind, ActivityUtilityWeights> _weights;

        public DecisionTuning(IEnumerable<KeyValuePair<ActivityKind, ActivityUtilityWeights>> weights,
            int shopHungerThreshold, int shopMinimumCopper, TieBreakBehavior tieBreak)
            : this(weights, shopHungerThreshold, shopMinimumCopper, tieBreak, null)
        {
        }

        public DecisionTuning(IEnumerable<KeyValuePair<ActivityKind, ActivityUtilityWeights>> weights,
            int shopHungerThreshold, int shopMinimumCopper, TieBreakBehavior tieBreak,
            IEnumerable<BeliefUtilityAdjustment> beliefAdjustments)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (shopHungerThreshold < 0 || shopHungerThreshold > 100)
                throw new ArgumentOutOfRangeException(nameof(shopHungerThreshold));
            if (shopMinimumCopper <= 0) throw new ArgumentOutOfRangeException(nameof(shopMinimumCopper));
            if (tieBreak != TieBreakBehavior.SeededRandom) throw new ArgumentOutOfRangeException(nameof(tieBreak));
            var copied = new SortedDictionary<ActivityKind, ActivityUtilityWeights>();
            foreach (var pair in weights)
            {
                ScheduleEntry.ValidateKind(pair.Key);
                if (pair.Value == null) throw new ArgumentException("Activity weights cannot be null.", nameof(weights));
                if (copied.ContainsKey(pair.Key)) throw new ArgumentException("Activity weights must be unique.", nameof(weights));
                copied.Add(pair.Key, pair.Value);
            }
            int expected = Enum.GetValues(typeof(ActivityKind)).Length;
            if (copied.Count != expected) throw new ArgumentException("Every activity requires explicit weights.", nameof(weights));
            _weights = new ReadOnlyDictionary<ActivityKind, ActivityUtilityWeights>(copied);
            var copiedAdjustments = new List<BeliefUtilityAdjustment>();
            if (beliefAdjustments != null)
                foreach (BeliefUtilityAdjustment adjustment in beliefAdjustments)
                {
                    if (adjustment == null)
                        throw new ArgumentException("Belief adjustments cannot contain null.", nameof(beliefAdjustments));
                    copiedAdjustments.Add(adjustment);
                }
            copiedAdjustments.Sort(CompareAdjustments);
            for (int index = 1; index < copiedAdjustments.Count; index++)
                if (CompareAdjustments(copiedAdjustments[index - 1], copiedAdjustments[index]) == 0)
                    throw new ArgumentException("Belief adjustments must be unique by activity and claim.",
                        nameof(beliefAdjustments));
            BeliefAdjustments = copiedAdjustments.AsReadOnly();
            ShopHungerThreshold = shopHungerThreshold;
            ShopMinimumCopper = shopMinimumCopper;
            TieBreak = tieBreak;
        }

        public int ShopHungerThreshold { get; }
        public int ShopMinimumCopper { get; }
        public TieBreakBehavior TieBreak { get; }
        public IReadOnlyList<BeliefUtilityAdjustment> BeliefAdjustments { get; }
        internal ActivityUtilityWeights For(ActivityKind kind) => _weights[kind];

        private static int CompareAdjustments(BeliefUtilityAdjustment left, BeliefUtilityAdjustment right)
        {
            int comparison = left.Activity.CompareTo(right.Activity);
            return comparison != 0 ? comparison : left.Claim.CompareTo(right.Claim);
        }
    }

    /// <summary>Read-only external facts needed for one NPC decision.</summary>
    public sealed class DecisionContext
    {
        public DecisionContext(int availableCopper, bool shopAvailable, LocationId foodLocation,
            LocationId shopLocation, LocationId socialLocation)
            : this(availableCopper, shopAvailable, foodLocation, shopLocation, socialLocation, null)
        {
        }

        public DecisionContext(int availableCopper, bool shopAvailable, LocationId foodLocation,
            LocationId shopLocation, LocationId socialLocation, NpcBeliefSnapshot beliefs)
        {
            if (availableCopper < 0) throw new ArgumentOutOfRangeException(nameof(availableCopper));
            if (!foodLocation.IsValid) throw new ArgumentException("Food location must be valid.", nameof(foodLocation));
            if (!shopLocation.IsValid) throw new ArgumentException("Shop location must be valid.", nameof(shopLocation));
            if (!socialLocation.IsValid) throw new ArgumentException("Social location must be valid.", nameof(socialLocation));
            AvailableCopper = availableCopper;
            ShopAvailable = shopAvailable;
            FoodLocation = foodLocation;
            ShopLocation = shopLocation;
            SocialLocation = socialLocation;
            Beliefs = beliefs;
        }

        public int AvailableCopper { get; }
        public bool ShopAvailable { get; }
        public LocationId FoodLocation { get; }
        public LocationId ShopLocation { get; }
        public LocationId SocialLocation { get; }
        public NpcBeliefSnapshot Beliefs { get; }
    }

    /// <summary>Supplies an immutable context snapshot without exposing mutable external state.</summary>
    public interface IDecisionContextProvider
    {
        DecisionContext GetSnapshot(NpcState npc);
    }

    /// <summary>Immutable activity intention selected at a game minute.</summary>
    public sealed class NpcIntention
    {
        public NpcIntention(ActivityKind kind, LocationId destination, GameTime chosenAt)
        {
            ScheduleEntry.ValidateKind(kind);
            if (!destination.IsValid) throw new ArgumentException("An intention destination must be valid.", nameof(destination));
            Kind = kind;
            Destination = destination;
            ChosenAt = chosenAt;
        }

        public ActivityKind Kind { get; }
        public LocationId Destination { get; }
        public GameTime ChosenAt { get; }
    }

    /// <summary>Selects intentions from schedules, needs and caller-supplied context during Decisions.</summary>
    public sealed class DecisionSystem : IWorldSystem
    {
        private static readonly ActivityKind[] CoreChoices = { ActivityKind.Eat, ActivityKind.Sleep,
            ActivityKind.Work, ActivityKind.Shop, ActivityKind.Socialize };
        private readonly DecisionTuning _tuning;
        private readonly IDecisionContextProvider _contexts;

        public DecisionSystem(DecisionTuning tuning, IDecisionContextProvider contexts)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        }

        public string Id => "agents.decisions";
        public SimulationPhase Phase => SimulationPhase.Decisions;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (NpcState npc in state.Npcs.Npcs)
            {
                DecisionContext context = _contexts.GetSnapshot(npc)
                    ?? throw new ArgumentNullException(nameof(context), "Decision context cannot be null.");
                if (context.Beliefs != null && context.Beliefs.Owner != npc.Definition.Id)
                    throw new InvalidOperationException("A decision context may contain only the deciding NPC's beliefs.");
                ScheduleEntry scheduled = npc.Definition.Schedule.At(state.Clock);
                NpcIntention intention = Choose(npc, context, scheduled, state);
                npc.SetIntention(intention);
            }
        }

        private NpcIntention Choose(NpcState npc, DecisionContext context, ScheduleEntry scheduled, WorldState state)
        {
            var candidates = new List<ActivityKind>(CoreChoices);
            if (scheduled != null && !candidates.Contains(scheduled.Kind)) candidates.Add(scheduled.Kind);
            candidates.Sort();
            long best = long.MinValue;
            var tied = new List<ActivityKind>();
            foreach (ActivityKind kind in candidates)
            {
                if (kind == ActivityKind.Shop && (!context.ShopAvailable
                    || context.AvailableCopper < _tuning.ShopMinimumCopper
                    || (scheduled?.Kind != ActivityKind.Shop
                        && npc.Needs.Hunger < _tuning.ShopHungerThreshold))) continue;
                long score = Score(kind, npc, scheduled, context);
                if (score > best)
                {
                    best = score;
                    tied.Clear();
                    tied.Add(kind);
                }
                else if (score == best) tied.Add(kind);
            }
            ActivityKind chosen = tied.Count == 1 ? tied[0] : tied[state.Rng.NextInt(tied.Count)];
            return new NpcIntention(chosen, Destination(chosen, npc, context, scheduled), state.Clock);
        }

        private long Score(ActivityKind kind, NpcState npc, ScheduleEntry scheduled, DecisionContext context)
        {
            ActivityUtilityWeights weights = _tuning.For(kind);
            long score = weights.BaseScore
                + (scheduled != null && scheduled.Kind == kind ? weights.ScheduleFit : 0L)
                + (long)npc.Needs.Hunger * weights.HungerUrgency
                + (long)(100 - npc.Needs.Energy) * weights.EnergyUrgency
                + (long)(100 - npc.Needs.Social) * weights.SocialUrgency;
            if (context.Beliefs == null) return score;
            foreach (BeliefUtilityAdjustment adjustment in _tuning.BeliefAdjustments)
            {
                if (adjustment.Activity != kind) continue;
                foreach (Belief belief in context.Beliefs.Beliefs)
                    if (belief.Claim.Equals(adjustment.Claim)
                        && belief.Confidence >= adjustment.MinimumConfidence)
                    {
                        score -= adjustment.Penalty;
                        break;
                    }
            }
            return score;
        }

        private static LocationId Destination(ActivityKind kind, NpcState npc,
            DecisionContext context, ScheduleEntry scheduled)
        {
            if (scheduled != null && scheduled.Kind == kind) return scheduled.Destination;
            switch (kind)
            {
                case ActivityKind.Eat: return context.FoodLocation;
                case ActivityKind.Shop: return context.ShopLocation;
                case ActivityKind.Socialize: return context.SocialLocation;
                case ActivityKind.Work: return npc.Definition.Workplace;
                default: return npc.Definition.Home;
            }
        }
    }
}
