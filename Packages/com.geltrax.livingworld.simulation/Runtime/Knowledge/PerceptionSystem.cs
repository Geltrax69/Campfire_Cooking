using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Supplies event-specific NPC eligibility without exposing hidden world truth to perception.</summary>
    public interface IPerceptionContext
    {
        IEnumerable<NpcId> Candidates(WorldEvent worldEvent);
        bool IsPresentAt(NpcId npc, LocationId location);
        bool IsAwake(NpcId npc);
    }

    /// <summary>Supplies explicit notice probabilities and observation confidence.</summary>
    public interface IPerceptionTuning
    {
        int NoticeChancePercent(NpcId npc, WorldEvent worldEvent);
        int Confidence(NpcId npc, WorldEvent worldEvent);
    }

    /// <summary>Turns eligible truth events into seeded, per-NPC observations during the Perception phase.</summary>
    public sealed class PerceptionSystem : IWorldSystem
    {
        private readonly IPerceptionContext _context;
        private readonly IPerceptionTuning _tuning;

        public PerceptionSystem(IPerceptionContext context, IPerceptionTuning tuning)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        }

        public string Id => "knowledge.perception";
        public SimulationPhase Phase => SimulationPhase.Perception;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (WorldEvent worldEvent in state.Events.Query())
            {
                if (worldEvent.Id.Value <= state.Knowledge.PerceptionCursor) continue;
                if (TryCreateClaim(worldEvent, out var claim)) Observe(state, worldEvent, claim);
                state.Knowledge.PerceptionCursor = worldEvent.Id.Value;
            }
        }

        private void Observe(WorldState state, WorldEvent worldEvent, BeliefClaim claim)
        {
            IEnumerable<NpcId> candidates = _context.Candidates(worldEvent);
            if (candidates == null) throw new InvalidOperationException("Perception candidates must not be null.");
            var ordered = new SortedSet<NpcId>();
            foreach (NpcId npc in candidates)
            {
                if (!npc.IsValid) throw new ArgumentException("Perception candidates need valid NPC IDs.");
                ordered.Add(npc);
            }

            foreach (NpcId npc in ordered)
            {
                if (!_context.IsPresentAt(npc, worldEvent.Location) || !_context.IsAwake(npc)) continue;
                if (!state.Knowledge.TryGet(npc, out var beliefs))
                    throw new InvalidOperationException("Every perception candidate must have a knowledge store.");
                int chance = _tuning.NoticeChancePercent(npc, worldEvent);
                if (chance < 0 || chance > 100) throw new ArgumentOutOfRangeException(nameof(chance));
                bool noticed = state.Rng.NextInt(100) < chance;
                if (!noticed) continue;
                int confidence = _tuning.Confidence(npc, worldEvent);
                if (confidence < 0 || confidence > 100) throw new ArgumentOutOfRangeException(nameof(confidence));
                beliefs.Set(new Belief(claim,
                    new BeliefSource(BeliefSourceKind.Seen, originEventId: worldEvent.Id),
                    confidence, state.Clock));
            }
        }

        private static bool TryCreateClaim(WorldEvent worldEvent, out BeliefClaim claim)
        {
            if (worldEvent.Type == WorldEventType.Theft)
            {
                claim = new BeliefClaim(BeliefClaimKind.TheftObserved, worldEvent.Location,
                    worldEvent.ItemType, worldEvent.Actor, worldEvent.Quantity);
                return true;
            }
            if (worldEvent.Type == WorldEventType.Arrival && worldEvent.Actor.HasValue)
            {
                claim = new BeliefClaim(BeliefClaimKind.Presence, worldEvent.Location,
                    subject: worldEvent.Actor.Value);
                return true;
            }
            claim = null;
            return false;
        }
    }
}
