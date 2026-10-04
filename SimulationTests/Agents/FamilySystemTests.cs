using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// The family system runs yearly: eligible households may welcome a baby
    /// (10%/year, recorded as a Birth truth event), and 15-year-olds may leave
    /// home to start their own household (50%). Everything is seeded and
    /// deterministic.
    /// </summary>
    public sealed class FamilySystemTests
    {
        private static readonly LocationId Farm = new LocationId("loc_farm");

        private static WorldState TestWorld(ulong seed, long startDay) =>
            new WorldState(seed, new GameTime((startDay - 1) * 1440));

        private static NpcState MakeNpc(string id, int age, string gender, LocationId home)
        {
            var definition = new NpcDefinition(new NpcId(id), id, age, gender, "tester",
                home, home, 0,
                new Dictionary<string, int>(),
                new NeedRates(6, 4, 4),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            return new NpcState(definition, 30, 80, 50);
        }

        private static NpcId RegisterNpc(WorldState state, string id, int age, string gender, LocationId home)
        {
            var npc = MakeNpc(id, age, gender, home);
            state.Npcs.Register(npc);
            return npc.Definition.Id;
        }

        private static HouseholdId RegisterHousehold(WorldState state, string id, LocationId home,
            params NpcId[] members)
        {
            var householdId = new HouseholdId(id);
            var household = new Household(householdId, home);
            state.Households.Register(household);
            foreach (var member in members)
            {
                household.AddMember(member);
                state.Npcs[member].SetHousehold(householdId);
            }
            return householdId;
        }

        private static World StartFamily(WorldState state, long startDay)
        {
            var world = new World(state);
            world.RegisterSystem(new FamilySystem());
            state.RestoreFamily(new FamilyState(initialized: true, lastFamilyDay: startDay - 1));
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
            var system = new FamilySystem();
            Assert.That(system.Id, Is.EqualTo("agents.family"));
            Assert.That(system.Phase, Is.EqualTo(SimulationPhase.Actions));
            Assert.That(() => system.Tick(null), Throws.ArgumentNullException);
        }

        [Test]
        public void BirthsHappen()
        {
            var state = TestWorld(seed: 42, startDay: 1);
            for (int i = 0; i < 40; i++)
            {
                var home = new LocationId("loc_home_" + i);
                var father = RegisterNpc(state, "npc_dad_" + i.ToString("D2"), 30, "male", home);
                var mother = RegisterNpc(state, "npc_mom_" + i.ToString("D2"), 28, "female", home);
                RegisterHousehold(state, "household_" + i, home, father, mother);
            }
            int before = state.Npcs.Count;
            var world = StartFamily(state, 1);

            TickDays(world, 360);

            var births = state.Events.Query(type: WorldEventType.Birth).ToList();
            Assert.That(births, Is.Not.Empty, "With 40 eligible couples at 10%/year, some babies are born.");
            Assert.That(state.Npcs.Count, Is.EqualTo(before + births.Count),
                "Every birth registers exactly one new NPC.");
        }

        [Test]
        public void BirthSetsFamilyLinks()
        {
            var state = TestWorld(seed: 99, startDay: 1);
            var father = RegisterNpc(state, "npc_dad", 30, "male", Farm);
            var mother = RegisterNpc(state, "npc_mom", 28, "female", Farm);
            var householdId = RegisterHousehold(state, "household_farm", Farm, father, mother);
            var world = StartFamily(state, 1);

            // Run year by year until a birth happens (10%/year; bounded for determinism).
            NpcState baby = null;
            for (int year = 0; year < 30 && baby == null; year++)
            {
                TickDays(world, 360);
                baby = state.Npcs.Npcs.FirstOrDefault(npc =>
                    npc.Definition.Id.Value.StartsWith("npc_born_", StringComparison.Ordinal));
            }
            Assert.That(baby, Is.Not.Null, "A birth happens within 30 years at seed 11.");

            Assert.That(baby.MotherId, Is.EqualTo(mother));
            Assert.That(baby.FatherId, Is.EqualTo(father));
            Assert.That(baby.Age, Is.EqualTo(0));
            Assert.That(baby.HouseholdId, Is.EqualTo(householdId),
                "Children inherit their parents' household.");
            Assert.That(state.Npcs[mother].ChildrenIds, Does.Contain(baby.Definition.Id));
            Assert.That(state.Npcs[father].ChildrenIds, Does.Contain(baby.Definition.Id));
            Assert.That(state.Households[householdId].HasMember(baby.Definition.Id), Is.True);
        }

        [Test]
        public void BirthIsRecordedAsTruth()
        {
            var state = TestWorld(seed: 99, startDay: 1);
            var father = RegisterNpc(state, "npc_dad", 30, "male", Farm);
            var mother = RegisterNpc(state, "npc_mom", 28, "female", Farm);
            RegisterHousehold(state, "household_farm", Farm, father, mother);
            var world = StartFamily(state, 1);

            NpcId babyId = default;
            for (int year = 0; year < 30 && !babyId.IsValid; year++)
            {
                TickDays(world, 360);
                var newborn = state.Npcs.Npcs.FirstOrDefault(npc =>
                    npc.Definition.Id.Value.StartsWith("npc_born_", StringComparison.Ordinal));
                if (newborn != null) babyId = newborn.Definition.Id;
            }
            Assert.That(babyId.IsValid, Is.True);

            var births = state.Events.Query(type: WorldEventType.Birth).ToList();
            var birth = births.First(e => e.Actor.HasValue && e.Actor.Value.Npc.HasValue
                && e.Actor.Value.Npc.Value == babyId);
            Assert.That(birth.Location, Is.EqualTo(Farm), "Births are recorded at the household home.");
            var targets = birth.Targets.Select(t => t.Npc).ToList();
            Assert.That(targets, Does.Contain(mother), "The birth names its mother.");
            Assert.That(targets, Does.Contain(father), "The birth names its father.");
        }

        [Test]
        public void BabyIdsAreUniqueAcrossYears()
        {
            var state = TestWorld(seed: 11, startDay: 1);
            for (int i = 0; i < 40; i++)
            {
                var home = new LocationId("loc_home_" + i);
                var father = RegisterNpc(state, "npc_dad_" + i.ToString("D2"), 30, "male", home);
                var mother = RegisterNpc(state, "npc_mom_" + i.ToString("D2"), 28, "female", home);
                RegisterHousehold(state, "household_" + i, home, father, mother);
            }
            var world = StartFamily(state, 1);

            TickDays(world, 360 * 3);

            var babyIds = state.Npcs.Npcs
                .Select(npc => npc.Definition.Id.Value)
                .Where(value => value.StartsWith("npc_born_", StringComparison.Ordinal))
                .ToList();
            Assert.That(babyIds, Is.Not.Empty);
            Assert.That(babyIds.Distinct().Count(), Is.EqualTo(babyIds.Count),
                "Every baby gets a unique ID.");
        }

        [Test]
        public void NoBirthWithoutEligibleCouple()
        {
            var state = TestWorld(seed: 11, startDay: 1);
            // Two men: no eligible couple.
            var a = RegisterNpc(state, "npc_a", 30, "male", Farm);
            var b = RegisterNpc(state, "npc_b", 32, "male", Farm);
            RegisterHousehold(state, "household_farm", Farm, a, b);
            // A lone woman: no couple at all.
            var loneHome = new LocationId("loc_lone");
            var lone = RegisterNpc(state, "npc_lone", 30, "female", loneHome);
            RegisterHousehold(state, "household_lone", loneHome, lone);
            // A woman past childbearing age with a husband.
            var oldHome = new LocationId("loc_old");
            var husband = RegisterNpc(state, "npc_husband", 60, "male", oldHome);
            var wife = RegisterNpc(state, "npc_wife", 55, "female", oldHome);
            RegisterHousehold(state, "household_old", oldHome, husband, wife);
            var world = StartFamily(state, 1);

            TickDays(world, 360 * 5);

            Assert.That(state.Events.Query(type: WorldEventType.Birth), Is.Empty,
                "No couple, no childbearing-age woman, no birth.");
            Assert.That(state.Npcs.Count, Is.EqualTo(5));
        }

        [Test]
        public void AdultChildrenMayLeaveHome()
        {
            var state = TestWorld(seed: 7, startDay: 1);
            var teenIds = new List<NpcId>();
            for (int i = 0; i < 10; i++)
            {
                var home = new LocationId("loc_home_" + i);
                var parent = RegisterNpc(state, "npc_parent_" + i.ToString("D2"), 40, "male", home);
                var teen = RegisterNpc(state, "npc_teen_" + i.ToString("D2"), 14, "female", home);
                RegisterHousehold(state, "household_" + i, home, parent, teen);
                teenIds.Add(teen);
            }
            // Teens must genuinely turn 15, so the aging system runs too.
            var world = new World(state);
            world.RegisterSystem(new AgingSystem());
            world.RegisterSystem(new FamilySystem());
            state.RestoreAging(new AgingState(initialized: true, lastAgingDay: 0));
            state.RestoreFamily(new FamilyState(initialized: true, lastFamilyDay: 0));

            TickDays(world, 360);

            Assert.That(teenIds.All(id => state.Npcs[id].Age == 15), Is.True,
                "All teens had their 15th birthday during the year.");
            int left = teenIds.Count(id =>
                state.Npcs[id].HouseholdId.Value.Value.StartsWith("household_npc_teen_",
                    StringComparison.Ordinal));
            Assert.That(left, Is.GreaterThan(0), "At 50%, some 15-year-olds leave home.");
            Assert.That(left, Is.LessThan(teenIds.Count), "At 50%, some 15-year-olds stay home.");
        }

        [Test]
        public void LeaversFormOwnHouseholdsNearby()
        {
            var state = TestWorld(seed: 99, startDay: 1);
            var parent = RegisterNpc(state, "npc_parent", 40, "male", Farm);
            var teen = RegisterNpc(state, "npc_teen", 14, "female", Farm);
            var oldHouseholdId = RegisterHousehold(state, "household_farm", Farm, parent, teen);
            // The teen must genuinely turn 15, so the aging system runs too.
            var world = new World(state);
            world.RegisterSystem(new AgingSystem());
            world.RegisterSystem(new FamilySystem());
            state.RestoreAging(new AgingState(initialized: true, lastAgingDay: 0));
            state.RestoreFamily(new FamilyState(initialized: true, lastFamilyDay: 0));

            // Run until this teen leaves (50%/year once 15; bounded for determinism).
            for (int year = 0; year < 10
                && state.Npcs[teen].HouseholdId.Value == oldHouseholdId; year++)
                TickDays(world, 360);

            var teenState = state.Npcs[teen];
            var newHouseholdId = teenState.HouseholdId.Value;
            Assert.That(newHouseholdId, Is.Not.EqualTo(oldHouseholdId), "The teen left home at seed 99.");
            var newHousehold = state.Households[newHouseholdId];
            Assert.That(newHousehold.MemberCount, Is.EqualTo(1), "A new household starts with just the leaver.");
            Assert.That(newHousehold.Home, Is.EqualTo(Farm), "Leavers settle near home.");
            Assert.That(state.Households[oldHouseholdId].HasMember(teen), Is.False,
                "The leaver is removed from the old household.");
        }

        [Test]
        public void UninitializedFamilyDoesNothing()
        {
            var state = TestWorld(seed: 11, startDay: 1);
            var father = RegisterNpc(state, "npc_dad", 30, "male", Farm);
            var mother = RegisterNpc(state, "npc_mom", 28, "female", Farm);
            RegisterHousehold(state, "household_farm", Farm, father, mother);
            var world = new World(state);
            world.RegisterSystem(new FamilySystem());
            // No RestoreFamily: the system must not catch up from day 1.

            TickDays(world, 360);

            Assert.That(state.Events.Query(type: WorldEventType.Birth), Is.Empty,
                "Without initialization the family cursor never advances.");
            Assert.That(state.Npcs.Count, Is.EqualTo(2));
        }

        [Test]
        public void DeceasedParentsDoNotHaveChildren()
        {
            var state = TestWorld(seed: 11, startDay: 1);
            var father = RegisterNpc(state, "npc_dad", 30, "male", Farm);
            var mother = RegisterNpc(state, "npc_mom", 28, "female", Farm);
            RegisterHousehold(state, "household_farm", Farm, father, mother);
            state.Npcs[mother].MarkDeceased();
            var world = StartFamily(state, 1);

            TickDays(world, 360 * 5);

            Assert.That(state.Events.Query(type: WorldEventType.Birth), Is.Empty,
                "The dead do not have children.");
        }
    }
}
