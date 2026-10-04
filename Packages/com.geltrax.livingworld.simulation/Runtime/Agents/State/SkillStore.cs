using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// One actor's (NPC or player) collection of skill states. Skills are looked up
    /// by <see cref="SkillId"/>; unknown skills read as level 0 / +0 bonus and are
    /// created at level 0 on first practice (dabbling). Deterministic: skills are
    /// stored in ordinal SkillId order.
    /// </summary>
    public sealed class SkillStore
    {
        private readonly SortedDictionary<SkillId, SkillState> _skills =
            new SortedDictionary<SkillId, SkillState>();

        /// <summary>
        /// Returns the skill state, creating a level-0 (unlearned) one on first access.
        /// </summary>
        public SkillState Get(SkillId skill)
        {
            if (!skill.IsValid) throw new ArgumentException("A skill lookup needs a valid ID.", nameof(skill));
            if (!_skills.TryGetValue(skill, out SkillState state))
            {
                state = new SkillState(skill);
                _skills.Add(skill, state);
            }
            return state;
        }

        /// <summary>Non-creating lookup; false when the actor has never touched the skill.</summary>
        public bool TryGet(SkillId skill, out SkillState state)
        {
            if (!skill.IsValid) throw new ArgumentException("A skill lookup needs a valid ID.", nameof(skill));
            return _skills.TryGetValue(skill, out state);
        }

        /// <summary>
        /// Practices a skill: creates it at level 0 if new, then applies the
        /// <see cref="SkillState.Practice"/> rules. Returns actual points gained.
        /// </summary>
        public int Practice(SkillId skill, int points, bool isTaught, long day, SimRng rng) =>
            Get(skill).Practice(points, isTaught, day, rng);

        /// <summary>Current level, or 0 for a skill the actor has never touched.</summary>
        public int GetLevel(SkillId skill)
        {
            if (!skill.IsValid) throw new ArgumentException("A skill lookup needs a valid ID.", nameof(skill));
            return _skills.TryGetValue(skill, out SkillState state) ? state.Level : 0;
        }

        /// <summary>Quality bonus for the skill's level, or +0 when unknown.</summary>
        public int GetQualityBonus(SkillId skill)
        {
            if (!skill.IsValid) throw new ArgumentException("A skill lookup needs a valid ID.", nameof(skill));
            return _skills.TryGetValue(skill, out SkillState state) ? state.QualityBonus : 0;
        }

        /// <summary>
        /// Captures every skill state for Persistence, in deterministic SkillId order.
        /// </summary>
        public IReadOnlyList<SkillState> Capture() =>
            new List<SkillState>(_skills.Values).AsReadOnly();

        /// <summary>
        /// Atomically replaces all skill states with a validated snapshot for
        /// Persistence. A rejected snapshot (null, null entry, duplicate skill)
        /// leaves the store unchanged.
        /// </summary>
        public void Restore(IEnumerable<SkillState> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var staged = new SortedDictionary<SkillId, SkillState>();
            foreach (SkillState state in snapshot)
            {
                if (state == null) throw new ArgumentNullException(nameof(snapshot),
                    "Restored skill states cannot contain null.");
                if (staged.ContainsKey(state.Skill))
                    throw new ArgumentException(
                        "Restored skill '" + state.Skill + "' appears twice.", nameof(snapshot));
                staged.Add(state.Skill, state);
            }
            _skills.Clear();
            foreach (KeyValuePair<SkillId, SkillState> pair in staged)
                _skills.Add(pair.Key, pair.Value);
        }
    }
}
