using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>Proves save/load can rebuild exact mutable NPC state from approved definitions.</summary>
    public sealed class NpcRestoreTests
    {
        private static readonly LocationId Home = new LocationId("loc_home");

        [Test]
        public void RestoreKeepsFractionalNeedsSleepingFlagAndIntentionExactly()
        {
            var definition = Definition(new NeedRates(6, 6, 6));
            var source = NeedState.FromSixtieths(2500, 4799, 60);
            var intention = new NpcIntention(ActivityKind.Sleep, Home, new GameTime(900));

            var restored = NpcState.Restore(definition, source, true, intention);

            Assert.That(restored.Definition, Is.SameAs(definition));
            Assert.That((restored.Needs.HungerSixtieths, restored.Needs.EnergySixtieths, restored.Needs.SocialSixtieths),
                Is.EqualTo((2500, 4799, 60)));
            Assert.That((restored.Needs.Hunger, restored.Needs.Energy, restored.Needs.Social),
                Is.EqualTo((41, 79, 1)));
            Assert.That(restored.IsSleeping, Is.True);
            Assert.That(restored.CurrentIntention.Kind, Is.EqualTo(ActivityKind.Sleep));
            Assert.That(restored.CurrentIntention.Destination, Is.EqualTo(Home));
            Assert.That(restored.CurrentIntention.ChosenAt, Is.EqualTo(new GameTime(900)));
            // The restored NPC owns its needs; later caller changes to the source must not leak in.
            Assert.That(restored.Needs, Is.Not.SameAs(source));
        }

        [Test]
        public void RestoredAwakeNpcAdvancesExactlyLikeLiveNeedsAndSleeperPauses()
        {
            var definition = Definition(new NeedRates(60, 60, 60));
            var awake = NpcState.Restore(definition, NeedState.FromSixtieths(600, 4800, 3000), false, null);
            var sleeper = NpcState.Restore(definition, NeedState.FromSixtieths(600, 4800, 3000), true,
                new NpcIntention(ActivityKind.Sleep, Home, new GameTime(0)));

            awake.AdvanceNeedsOneMinute();
            sleeper.AdvanceNeedsOneMinute();

            Assert.That((awake.Needs.HungerSixtieths, awake.Needs.EnergySixtieths, awake.Needs.SocialSixtieths),
                Is.EqualTo((660, 4740, 2940)));
            Assert.That((sleeper.Needs.HungerSixtieths, sleeper.Needs.EnergySixtieths, sleeper.Needs.SocialSixtieths),
                Is.EqualTo((600, 4800, 3000)));
        }

        [Test]
        public void RestoreRejectsNullsButAcceptsDisagreeingSleepAndIntention()
        {
            var definition = Definition(new NeedRates(1, 1, 1));
            var source = NeedState.FromSixtieths(2500, 4799, 60);
            var sleep = new NpcIntention(ActivityKind.Sleep, Home, new GameTime(0));
            var eat = new NpcIntention(ActivityKind.Eat, Home, new GameTime(0));

            Assert.Throws<ArgumentNullException>(() => NpcState.Restore(null, source, false, null));
            Assert.Throws<ArgumentNullException>(() => NpcState.Restore(definition, null, false, null));

            // The running world drives IsSleeping from the schedule while the
            // intention driver is unwired, so a night-time save honestly carries
            // IsSleeping=true with no Sleep intention (see NpcState.Restore's
            // remarks): the flag and the intention restore independently.
            NpcState nightSave = NpcState.Restore(definition, source, true, null);
            Assert.That(nightSave.IsSleeping, Is.True);
            Assert.That(nightSave.CurrentIntention, Is.Null);
            NpcState staleIntention = NpcState.Restore(definition, source, true, eat);
            Assert.That(staleIntention.CurrentIntention.Kind, Is.EqualTo(ActivityKind.Eat));
            NpcState leftover = NpcState.Restore(definition, source, false, sleep);
            Assert.That(leftover.IsSleeping, Is.False);

            Assert.That((source.HungerSixtieths, source.EnergySixtieths, source.SocialSixtieths),
                Is.EqualTo((2500, 4799, 60)));
        }

        private static NpcDefinition Definition(NeedRates rates)
        {
            return new NpcDefinition(new NpcId("npc_test"), "Test", 30, "test", "tester",
                Home, Home, 0, new Dictionary<string, int> { ["honest"] = 50 }, rates,
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
        }
    }
}
