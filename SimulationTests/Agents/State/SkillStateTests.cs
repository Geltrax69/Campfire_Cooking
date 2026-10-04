using System;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Proves the P3-01 skill progression rules: thresholds, daily cap, teaching
    /// bonus, quality bonus, dabbling, and no decay. See docs/design/SKILLS.md.
    /// </summary>
    public sealed class SkillStateTests
    {
        private static readonly SkillId Cooking = new SkillId("skill_cooking");

        private static SimRng Rng() => new SimRng(12345);

        [Test]
        public void PracticeGrantsPointsAndLevelsUp()
        {
            var skill = new SkillState(Cooking, startingLevel: 1);

            // 80 points over 4 days (20/day cap) → 80 total, still level 1.
            for (long day = 1; day <= 4; day++)
                Assert.That(skill.Practice(20, false, day, Rng()), Is.EqualTo(20));
            Assert.That(skill.PracticePoints, Is.EqualTo(80));
            Assert.That(skill.Level, Is.EqualTo(1));

            // 20 more → 100 total → level 2.
            Assert.That(skill.Practice(20, false, 5, Rng()), Is.EqualTo(20));
            Assert.That(skill.PracticePoints, Is.EqualTo(100));
            Assert.That(skill.Level, Is.EqualTo(2));

            // 200 more (10 days) → 300 total → level 3.
            for (long day = 6; day <= 15; day++)
                skill.Practice(20, false, day, Rng());
            Assert.That(skill.PracticePoints, Is.EqualTo(300));
            Assert.That(skill.Level, Is.EqualTo(3));

            // 400 more (20 days) → 700 total → level 4.
            for (long day = 16; day <= 35; day++)
                skill.Practice(20, false, day, Rng());
            Assert.That(skill.PracticePoints, Is.EqualTo(700));
            Assert.That(skill.Level, Is.EqualTo(4));

            // 800 more (40 days) → 1500 total → level 5 (master).
            for (long day = 36; day <= 75; day++)
                skill.Practice(20, false, day, Rng());
            Assert.That(skill.PracticePoints, Is.EqualTo(1500));
            Assert.That(skill.Level, Is.EqualTo(5));

            // Beyond level 5: points still accrue, level stays 5.
            skill.Practice(20, false, 76, Rng());
            Assert.That(skill.PracticePoints, Is.EqualTo(1520));
            Assert.That(skill.Level, Is.EqualTo(5));
        }

        [Test]
        public void DailyCapEnforced()
        {
            var skill = new SkillState(Cooking, startingLevel: 1);

            // 30 points in one day → only 20 accrue, excess is lost.
            Assert.That(skill.Practice(30, false, 1, Rng()), Is.EqualTo(20));
            Assert.That(skill.PracticePoints, Is.EqualTo(20));
            Assert.That(skill.DailyPoints, Is.EqualTo(20));

            // Same day again → nothing more (cap already hit).
            Assert.That(skill.Practice(10, false, 1, Rng()), Is.EqualTo(0));
            Assert.That(skill.PracticePoints, Is.EqualTo(20));

            // Next day → the cap resets, can gain again.
            Assert.That(skill.Practice(10, false, 2, Rng()), Is.EqualTo(10));
            Assert.That(skill.PracticePoints, Is.EqualTo(30));
            Assert.That(skill.DailyPoints, Is.EqualTo(10));
        }

        [Test]
        public void TeachingDoublesGains()
        {
            var skill = new SkillState(Cooking, startingLevel: 1);

            // 10 points taught → 20 gained (doubled, then capped).
            Assert.That(skill.Practice(10, true, 1, Rng()), Is.EqualTo(20));
            Assert.That(skill.PracticePoints, Is.EqualTo(20));

            // Teaching 15 on a fresh day → 30 doubled, but cap holds at 20.
            Assert.That(skill.Practice(15, true, 2, Rng()), Is.EqualTo(20));
            Assert.That(skill.PracticePoints, Is.EqualTo(40));
        }

        [Test]
        public void QualityBonusByLevel()
        {
            Assert.That(new SkillState(Cooking).QualityBonus, Is.EqualTo(0),
                "Level 0 (unlearned): +0");
            Assert.That(new SkillState(Cooking, 1).QualityBonus, Is.EqualTo(0),
                "Level 1: +0");
            Assert.That(new SkillState(Cooking, 2).QualityBonus, Is.EqualTo(1),
                "Level 2: +1");
            Assert.That(new SkillState(Cooking, 3).QualityBonus, Is.EqualTo(2),
                "Level 3: +2");
            Assert.That(new SkillState(Cooking, 4).QualityBonus, Is.EqualTo(3),
                "Level 4: +3");
            Assert.That(new SkillState(Cooking, 5).QualityBonus, Is.EqualTo(4),
                "Level 5: +4");
        }

        [Test]
        public void DabblingUnlocksLevel1()
        {
            var skill = new SkillState(Cooking);
            Assert.That(skill.Level, Is.EqualTo(0), "Unpracticed skill starts at level 0.");

            // 19 points → still level 0 (dabbling, not yet unlocked).
            skill.Practice(19, false, 1, Rng());
            Assert.That(skill.Level, Is.EqualTo(0));
            Assert.That(skill.PracticePoints, Is.EqualTo(19));

            // 20th point → level 1 unlocked.
            skill.Practice(1, false, 2, Rng());
            Assert.That(skill.Level, Is.EqualTo(1));
            Assert.That(skill.PracticePoints, Is.EqualTo(20));
        }

        [Test]
        public void NoDecay()
        {
            // The design says "a skill learned is kept": SkillState exposes no API
            // to decrease points or level. This test documents that monotonicity:
            // practice only ever moves points and level upward.
            var skill = new SkillState(Cooking, startingLevel: 2);
            int pointsBefore = skill.PracticePoints;
            int levelBefore = skill.Level;

            // Even zero-point practice cannot reduce anything.
            Assert.That(skill.Practice(0, false, 1, Rng()), Is.EqualTo(0));
            Assert.That(skill.PracticePoints, Is.EqualTo(pointsBefore));
            Assert.That(skill.Level, Is.EqualTo(levelBefore));

            // And a long gap with no practice changes nothing.
            var idle = new SkillState(Cooking, startingLevel: 3);
            Assert.That(idle.Level, Is.EqualTo(3));
            Assert.That(idle.PracticePoints, Is.EqualTo(0));
        }

        [Test]
        public void PracticeRejectsBadInput()
        {
            var skill = new SkillState(Cooking, startingLevel: 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => skill.Practice(-1, false, 1, Rng()));
            Assert.Throws<ArgumentOutOfRangeException>(() => skill.Practice(10, false, -1, Rng()));
            Assert.Throws<ArgumentNullException>(() => skill.Practice(10, false, 1, null));
        }

        [Test]
        public void StartingLevelMustBeValid()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillState(Cooking, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillState(Cooking, 6));
        }
    }
}
