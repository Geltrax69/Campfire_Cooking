using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>Immutable recollection of a claim without asserting that the claim is true.</summary>
    public sealed class Memory
    {
        internal Memory(BeliefClaim claim, int importance, GameTime formedAt, GameTime lastReinforcedAt,
            int strength, WorldEventId? originEventId)
        {
            Claim = claim ?? throw new ArgumentNullException(nameof(claim));
            if (importance < 0 || importance > 100) throw new ArgumentOutOfRangeException(nameof(importance));
            if (lastReinforcedAt < formedAt) throw new ArgumentException("Reinforcement cannot predate formation.");
            if (strength < 0 || strength > importance) throw new ArgumentOutOfRangeException(nameof(strength));
            if (originEventId.HasValue && !originEventId.Value.IsValid)
                throw new ArgumentException("Invalid origin event.", nameof(originEventId));
            Importance = importance;
            FormedAt = formedAt;
            LastReinforcedAt = lastReinforcedAt;
            Strength = strength;
            OriginEventId = originEventId;
        }

        public BeliefClaim Claim { get; }
        public int Importance { get; }
        public GameTime FormedAt { get; }
        public GameTime LastReinforcedAt { get; }
        public int Strength { get; }
        public WorldEventId? OriginEventId { get; }
    }

    /// <summary>Owns one NPC's memories in deterministic claim order.</summary>
    public sealed class MemoryStore
    {
        private const long MinutesPerDay = 1440;
        private readonly SortedList<BeliefClaim, Memory> _memories = new SortedList<BeliefClaim, Memory>();

        public MemoryStore(NpcId owner)
        {
            if (!owner.IsValid) throw new ArgumentException("A memory store needs a valid owner.", nameof(owner));
            Owner = owner;
        }

        public NpcId Owner { get; }
        public int Count => _memories.Count;

        public void Remember(BeliefClaim claim, int importance, GameTime reinforcedAt,
            WorldEventId? originEventId = null)
        {
            if (claim == null) throw new ArgumentNullException(nameof(claim));
            if (importance < 0 || importance > 100) throw new ArgumentOutOfRangeException(nameof(importance));
            if (originEventId.HasValue && !originEventId.Value.IsValid)
                throw new ArgumentException("Invalid origin event.", nameof(originEventId));
            _memories.TryGetValue(claim, out var existing);
            if (existing != null && reinforcedAt < existing.LastReinforcedAt)
                throw new InvalidOperationException("A memory cannot be reinforced in the past.");
            if (importance == 0)
            {
                _memories.Remove(claim);
                return;
            }
            GameTime formedAt = existing == null ? reinforcedAt : existing.FormedAt;
            WorldEventId? origin = originEventId ?? existing?.OriginEventId;
            _memories[claim] = new Memory(claim, importance, formedAt, reinforcedAt, importance, origin);
        }

        public IReadOnlyList<Memory> Query() => new List<Memory>(_memories.Values).AsReadOnly();

        public void Decay(GameTime now, MemoryRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            for (int i = 0; i < _memories.Count; i++)
                if (now < _memories.Values[i].LastReinforcedAt)
                    throw new InvalidOperationException("Memory decay cannot run before reinforcement.");
            for (int i = _memories.Count - 1; i >= 0; i--)
            {
                Memory memory = _memories.Values[i];
                long elapsedDays = (now.TotalMinutes - memory.LastReinforcedAt.TotalMinutes) / MinutesPerDay;
                MemoryLevel level = rules.LevelFor(memory.Importance);
                int strength = Math.Max(0, memory.Importance - rules.StrengthLoss(memory.Importance, elapsedDays));
                if (strength == 0 || elapsedDays >= level.RetentionDays)
                    _memories.RemoveAt(i);
                else if (strength != memory.Strength)
                    _memories[memory.Claim] = new Memory(memory.Claim, memory.Importance, memory.FormedAt,
                        memory.LastReinforcedAt, strength, memory.OriginEventId);
            }
        }
    }
}
