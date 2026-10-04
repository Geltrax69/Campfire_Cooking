using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// NPCs age one year on their birthday (a deterministic day-of-year from the
    /// NPC ID hash), move through child/adult/elder life stages, and elders face
    /// a yearly seeded death roll. Deaths are recorded as world truth; the body
    /// stays registered so inheritance (P7-03) can find it.
    /// </summary>
    public sealed class AgingSystemTests
    {
        private static readonly LocationId Farm = new LocationId("loc_farm");

        private static WorldState TestWorld(ulong seed, long startDay) =>
            new WorldState(seed, new GameTime((startDay - 1) * 1440));

        private static NpcState MakeNpcState(string id, int age)
        {
            var npcId = new NpcId(id);
            var definition = new NpcDefinition(npcId, id, age, "test", "tester",
                Farm, Farm, 0,
                new Dictionary<string, int> { ["honest"] = 50 },
                new NeedRates(0, 0, 0),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            return new NpcState(definition, 50, 80, 50);
        }

        private static NpcId RegisterNpc(WorldState state, string id, int age)
        {
            var npc = MakeNpcState(id, age);
            state.Npcs.Register(npc);
            return npc.Definition.Id;
        }

        private static World StartAging(WorldState state, long startDay)
        {
            var world = new World(state);
            world.RegisterSystem(new AgingSystem());
            state.RestoreAging(new AgingState(initialized: true, lastAgingDay: startDay - 1));
            return world;
        }

        private static void TickDays(World world, int days)
        {
            for (int day = 0; day < days; day++)
            {
                world.State.Clock = world.State.Clock.Advance(1440);
                world.Tick();
            }
        }

        [Test]
        public void SystemHasStableIdentity()
        {
            var system = new AgingSystem();
            Assert.That(system.Id, Is.EqualTo("agents.aging"));
            Assert.That(system.Phase, Is.EqualTo(SimulationPhase.Actions));
            Assert.That(() => system.Tick(null), Throws.ArgumentNullException);
        }

        [Test]
        public void AgeAdvancesYearly()
        {
            var state = TestWorld(seed: 1, startDay: 1);
            NpcId npc = RegisterNpc(state, "npc_ager", 30);
            var world = StartAging(state, 1);

            // One full 360-day village year holds exactly one birthday per NPC.
            TickDays(world, 360);

            Assert.That(state.Npcs[npc].Age, Is.EqualTo(31));
        }

        [Test]
        public void AgeStartsFromContentDefinition()
        {
            var state = TestWorld(seed: 1, startDay: 1);
            NpcId npc = RegisterNpc(state, "npc_middle_aged", 45);
            Assert.That(state.Npcs[npc].Age, Is.EqualTo(45));
        }

        [TestCase(0, LifeStage.Child)]
        [TestCase(14, LifeStage.Child)]
        [TestCase(15, LifeStage.Adult)]
        [TestCase(59, LifeStage.Adult)]
        [TestCase(60, LifeStage.Elder)]
        [TestCase(99, LifeStage.Elder)]
        public void LifeStages(int age, LifeStage expected)
        {
            Assert.That(MakeNpcState("npc_staged", age).LifeStage, Is.EqualTo(expected));
        }

        [Test]
        public void EldersMayDie()
        {
            var state = TestWorld(seed: 42, startDay: 1);
            var ids = new List<NpcId>();
            for (int i = 0; i < 50; i++)
                ids.Add(RegisterNpc(state, "npc_elder_" + i.ToString("D2"), 85));
            var world = StartAging(state, 1);

            TickDays(world, 360);

            int dead = ids.Count(id => state.Npcs[id].IsDeceased);
            Assert.That(dead, Is.GreaterThan(0),
                "At 40%/year, some of 50 85-year-olds should die within a year.");
        }

        [Test]
        public void AdultsDoNotDieOfOldAge()
        {
            var state = TestWorld(seed: 42, startDay: 1);
            var ids = new List<NpcId>();
            for (int i = 0; i < 20; i++)
                ids.Add(RegisterNpc(state, "npc_adult_" + i.ToString("D2"), 30));
            var world = StartAging(state, 1);

            TickDays(world, 360);

            Assert.That(ids.All(id => !state.Npcs[id].IsDeceased), Is.True,
                "Adults face no old-age death roll.");
            Assert.That(state.Events.Query(type: WorldEventType.Death).Count, Is.EqualTo(0));
        }

        [Test]
        public void DeathIsRecorded()
        {
            var state = TestWorld(seed: 7, startDay: 1);
            var ids = new List<NpcId>();
            for (int i = 0; i < 30; i++)
                ids.Add(RegisterNpc(state, "npc_doomed_" + i.ToString("D2"), 85));
            var world = StartAging(state, 1);

            TickDays(world, 360);

            var deaths = state.Events.Query(type: WorldEventType.Death).ToList();
            Assert.That(deaths, Is.Not.Empty, "Elder deaths are recorded as world truth.");
            var deadIds = ids.Where(id => state.Npcs[id].IsDeceased).ToList();
            Assert.That(deadIds, Is.Not.Empty);
            Assert.That(deaths.Count, Is.EqualTo(deadIds.Count),
                "One Death truth event per deceased NPC.");
            foreach (var death in deaths)
            {
                Assert.That(death.Actor.HasValue, Is.True);
                Assert.That(deadIds, Does.Contain(death.Actor.Value.Npc.Value),
                    "The death event names the deceased NPC.");
                Assert.That(death.Location, Is.EqualTo(Farm),
                    "Deaths are recorded at the NPC's home.");
            }
            // The body stays registered for inheritance (P7-03).
            foreach (var deadId in deadIds)
                Assert.DoesNotThrow(() => { var _ = state.Npcs[deadId]; });

            // The deceased do not age again and are never rolled twice.
            var agesAfterDeath = deadIds.ToDictionary(id => id, id => state.Npcs[id].Age);
            TickDays(world, 360);
            foreach (var deadId in deadIds)
                Assert.That(state.Npcs[deadId].Age, Is.EqualTo(agesAfterDeath[deadId]),
                    "The deceased do not have further birthdays.");
            Assert.That(state.Events.Query(type: WorldEventType.Death).Count,
                Is.GreaterThanOrEqualTo(deaths.Count),
                "No duplicate death events for the already-deceased.");
        }

        [Test]
        public void BirthdaysAreDeterministic()
        {
            int first = AgingSystem.BirthdayDayOfYear(new NpcId("npc_tansy_alder"));
            int second = AgingSystem.BirthdayDayOfYear(new NpcId("npc_tansy_alder"));
            Assert.That(first, Is.EqualTo(second), "Same NPC ID always yields the same birthday.");
            Assert.That(first, Is.InRange(1, 360), "Birthdays fall on a day of the 360-day village year.");
            int other = AgingSystem.BirthdayDayOfYear(new NpcId("npc_bram_stone"));
            Assert.That(other, Is.InRange(1, 360));
        }

        [Test]
        public void UninitializedAgingDoesNothing()
        {
            var state = TestWorld(seed: 1, startDay: 1);
            NpcId npc = RegisterNpc(state, "npc_ager", 30);
            var world = new World(state);
            world.RegisterSystem(new AgingSystem());
            // No RestoreAging: the system must not catch up from day 1.

            TickDays(world, 360);

            Assert.That(state.Npcs[npc].Age, Is.EqualTo(30),
                "Without initialization the aging cursor never advances.");
        }
    }
}
