using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>Proves the per-actor SkillStore: lookup, practice routing, and save/load capture.</summary>
    public sealed class SkillStoreTests
    {
        private static readonly SkillId Cooking = new SkillId("skill_cooking");
        private static readonly SkillId Taming = new SkillId("skill_taming");

        private static SimRng Rng() => new SimRng(999);

        [Test]
        public void GetCreatesLevelZeroOnFirstAccess()
        {
            var store = new SkillStore();
            SkillState skill = store.Get(Cooking);
            Assert.That(skill, Is.Not.Null);
            Assert.That(skill.Skill, Is.EqualTo(Cooking));
            Assert.That(skill.Level, Is.EqualTo(0));
            // Second Get returns the same object.
            Assert.That(store.Get(Cooking), Is.SameAs(skill));
        }

        [Test]
        public void PracticeThroughStoreAccruesPoints()
        {
            var store = new SkillStore();
            Assert.That(store.Practice(Cooking, 10, false, 1, Rng()), Is.EqualTo(10));
            Assert.That(store.GetLevel(Cooking), Is.EqualTo(0));
            Assert.That(store.Practice(Cooking, 10, false, 2, Rng()), Is.EqualTo(10));
            Assert.That(store.GetLevel(Cooking), Is.EqualTo(1), "20 points unlocks level 1.");
        }

        [Test]
        public void GetLevelAndQualityBonusDefaultToZeroForUnknownSkill()
        {
            var store = new SkillStore();
            Assert.That(store.GetLevel(Taming), Is.EqualTo(0));
            Assert.That(store.GetQualityBonus(Taming), Is.EqualTo(0));
        }

        [Test]
        public void GetQualityBonusFollowsLevel()
        {
            var store = new SkillStore();
            store.Get(Cooking).Practice(100, false, 1, Rng());
            // 20 points on day 1 (capped) → level 1 → bonus +0.
            Assert.That(store.GetQualityBonus(Cooking), Is.EqualTo(0));
            for (long day = 2; day <= 5; day++)
                store.Practice(Cooking, 20, false, day, Rng());
            // 100 total → level 2 → bonus +1.
            Assert.That(store.GetLevel(Cooking), Is.EqualTo(2));
            Assert.That(store.GetQualityBonus(Cooking), Is.EqualTo(1));
        }

        [Test]
        public void CaptureRestoreRoundTrip()
        {
            var store = new SkillStore();
            store.Practice(Cooking, 20, false, 1, Rng());
            store.Practice(Taming, 10, true, 1, Rng());

            IReadOnlyList<SkillState> snapshot = store.Capture();
            Assert.That(snapshot.Count, Is.EqualTo(2));

            var restored = new SkillStore();
            restored.Restore(snapshot);
            Assert.That(restored.GetLevel(Cooking), Is.EqualTo(1));
            Assert.That(restored.GetLevel(Taming), Is.EqualTo(1));
            Assert.That(restored.Get(Cooking).PracticePoints, Is.EqualTo(20));
            Assert.That(restored.Get(Taming).PracticePoints, Is.EqualTo(20));
            Assert.That(restored.Get(Cooking).DailyPoints, Is.EqualTo(20));
        }

        [Test]
        public void RestoreRejectsBadSnapshotsWithoutMutating()
        {
            var store = new SkillStore();
            store.Practice(Cooking, 20, false, 1, Rng());

            Assert.Throws<ArgumentNullException>(() => store.Restore(null));
            Assert.Throws<ArgumentException>(() =>
                store.Restore(new[] { store.Get(Cooking), store.Get(Cooking) }),
                "Duplicate skill in snapshot.");
            Assert.Throws<ArgumentNullException>(() =>
                store.Restore(new SkillState[] { null }));

            // Rejected restores leave the store unchanged.
            Assert.That(store.GetLevel(Cooking), Is.EqualTo(1));
            Assert.That(store.Get(Cooking).PracticePoints, Is.EqualTo(20));
        }

        [Test]
        public void NpcStateHasSkillStore()
        {
            var definition = new NpcDefinition(new NpcId("npc_test"), "Test", 30, "test", "tester",
                new LocationId("loc_home"), new LocationId("loc_home"), 0,
                new Dictionary<string, int> { ["honest"] = 50 }, new NeedRates(1, 1, 1),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            var npc = new NpcState(definition, 30, 80, 50);
            Assert.That(npc.Skills, Is.Not.Null);
            Assert.That(npc.Skills.GetLevel(Cooking), Is.EqualTo(0));
        }

        [Test]
        public void WorldStateHasPlayerSkills()
        {
            var state = new WorldState(42, new GameTime(0));
            Assert.That(state.PlayerSkills, Is.Not.Null);
            state.PlayerSkills.Practice(Cooking, 20, false, 1, Rng());
            Assert.That(state.PlayerSkills.GetLevel(Cooking), Is.EqualTo(1));
        }
    }
}
