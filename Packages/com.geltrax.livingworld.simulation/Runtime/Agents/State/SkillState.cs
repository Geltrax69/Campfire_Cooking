using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// One actor's mutable state for a single skill (docs/design/SKILLS.md): level 0
    /// (unlearned) through 5 (master), earned only through practice and teaching —
    /// never XP-from-killing. Practice grants points toward cumulative thresholds;
    /// at most <see cref="DailyCap"/> points accrue per game day (excess is lost);
    /// being taught by someone better doubles the gain. Levels never decrease:
    /// "a skill learned is kept."
    /// </summary>
    public sealed class SkillState
    {
        /// <summary>Highest level; 1500 cumulative points makes a master.</summary>
        public const int MaxLevel = 5;
        /// <summary>Points that can accrue in one game day; excess practice is lost.</summary>
        public const int DailyCap = 20;
        /// <summary>Cumulative points that unlock level 1 from unlearned (dabbling).</summary>
        public const int DabbleThreshold = 20;
        /// <summary>Cumulative points for level 2.</summary>
        public const int Level2Threshold = 100;
        /// <summary>Cumulative points for level 3.</summary>
        public const int Level3Threshold = 300;
        /// <summary>Cumulative points for level 4.</summary>
        public const int Level4Threshold = 700;
        /// <summary>Cumulative points for level 5.</summary>
        public const int Level5Threshold = 1500;

        /// <summary>Starts an unlearned skill (level 0).</summary>
        public SkillState(SkillId skill) : this(skill, 0, 0, 0, -1)
        {
        }

        /// <summary>
        /// Starts a skill at a known level (e.g. the player's chosen starting skill
        /// at level 1, or an NPC teacher's established skill). Points start at 0;
        /// the level is kept until practice crosses the next threshold above it.
        /// </summary>
        public SkillState(SkillId skill, int startingLevel)
            : this(skill, ValidateLevel(startingLevel), 0, 0, -1)
        {
        }

        private SkillState(SkillId skill, int level, int practicePoints, int dailyPoints, long lastPracticeDay)
        {
            if (!skill.IsValid) throw new ArgumentException("A skill needs a valid ID.", nameof(skill));
            Skill = skill;
            Level = level;
            PracticePoints = practicePoints;
            DailyPoints = dailyPoints;
            LastPracticeDay = lastPracticeDay;
        }

        private static int ValidateLevel(int level)
        {
            if (level < 0 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level),
                    "Skill level must be 0 (unlearned) through " + MaxLevel + ".");
            return level;
        }

        /// <summary>
        /// Rebuilds exact skill state for save/load. Everything is validated before
        /// anything is built; a failed restore throws without side effects.
        /// </summary>
        public static SkillState Restore(SkillId skill, int level, int practicePoints,
            int dailyPoints, long lastPracticeDay)
        {
            if (!skill.IsValid) throw new ArgumentException("A skill needs a valid ID.", nameof(skill));
            ValidateLevel(level);
            if (practicePoints < 0) throw new ArgumentOutOfRangeException(nameof(practicePoints));
            if (dailyPoints < 0 || dailyPoints > DailyCap)
                throw new ArgumentOutOfRangeException(nameof(dailyPoints),
                    "Daily points must be 0 through " + DailyCap + ".");
            if (lastPracticeDay < -1) throw new ArgumentOutOfRangeException(nameof(lastPracticeDay));
            // Points force a minimum level (practice levels up when thresholds cross);
            // a higher level is fine — it was granted as a starting level and kept.
            if (level < LevelForPoints(practicePoints))
                throw new ArgumentException(
                    "Level " + level + " is below what " + practicePoints + " points grant.",
                    nameof(level));
            return new SkillState(skill, level, practicePoints, dailyPoints, lastPracticeDay);
        }

        public SkillId Skill { get; }
        /// <summary>0 = unlearned, 1-5. Only ever moves up, never down.</summary>
        public int Level { get; private set; }
        /// <summary>Cumulative practice points; never decreases.</summary>
        public int PracticePoints { get; private set; }
        /// <summary>Points accrued on <see cref="LastPracticeDay"/>; resets each new day.</summary>
        public int DailyPoints { get; private set; }
        /// <summary>Game day of the last practice; -1 before any practice.</summary>
        public long LastPracticeDay { get; private set; }

        /// <summary>
        /// Quality bonus added to the 0-100 item quality scale: +0 at levels 0-1,
        /// then +1 per level (+1 at 2, up to +4 at 5).
        /// </summary>
        public int QualityBonus => Level <= 1 ? 0 : Level - 1;

        /// <summary>
        /// Practices the skill: gains are doubled when taught by someone better,
        /// then capped at <see cref="DailyCap"/> per game day (excess is lost).
        /// Returns the actual points gained. Level rises when cumulative thresholds
        /// are crossed; it never falls. The RNG is accepted for future use —
        /// practice itself is deterministic, so no draws are consumed.
        /// </summary>
        public int Practice(int points, bool isTaught, long day, SimRng rng)
        {
            if (points < 0) throw new ArgumentOutOfRangeException(nameof(points),
                "Practice points must not be negative.");
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day),
                "Game day must not be negative.");
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            if (day != LastPracticeDay)
            {
                DailyPoints = 0;
                LastPracticeDay = day;
            }

            int gain = isTaught ? points * 2 : points;
            int actual = Math.Min(gain, DailyCap - DailyPoints);
            if (actual <= 0) return 0;

            DailyPoints += actual;
            PracticePoints += actual;
            int earned = LevelForPoints(PracticePoints);
            if (earned > Level) Level = earned;
            return actual;
        }

        private static int LevelForPoints(int points)
        {
            if (points >= Level5Threshold) return 5;
            if (points >= Level4Threshold) return 4;
            if (points >= Level3Threshold) return 3;
            if (points >= Level2Threshold) return 2;
            if (points >= DabbleThreshold) return 1;
            return 0;
        }
    }
}
