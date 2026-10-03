using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Verifies the farm household's home-grown lunch: at noon every NPC whose home
    /// is the farm recovers hunger directly, without spending a copper. Town NPCs
    /// get no farm meal.
    /// </summary>
    public sealed class FarmMealSystemTests
    {
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly LocationId TownHome = new LocationId("loc_home_mira");
        private const int MealHour = 12;

        [Test]
        public void FarmNpcEatsHomeGrownMealAtNoon()
        {
            WorldState state = TestWorld(out World world);
            NpcId farmer = RegisterNpc(state, "npc_test_corvin", Farm);

            world.RegisterSystem(new FarmMealSystem());
            world.Tick();

            Assert.That(state.Npcs[farmer].Needs.Hunger, Is.EqualTo(50),
                "Farm meal restores 50 hunger points.");
        }

        [Test]
        public void TownNpcGetsNoFarmMeal()
        {
            WorldState state = TestWorld(out World world);
            NpcId townie = RegisterNpc(state, "npc_test_mira", TownHome);

            world.RegisterSystem(new FarmMealSystem());
            world.Tick();

            Assert.That(state.Npcs[townie].Needs.Hunger, Is.EqualTo(100),
                "The farm kitchen feeds only the farm household.");
        }

        [Test]
        public void MealOnlyHappensAtNoon()
        {
            var state = new WorldState(42, new GameTime(MealHour * 60 + 60));
            var world = new World(state);
            NpcId farmer = RegisterNpc(state, "npc_test_corvin", Farm);

            world.RegisterSystem(new FarmMealSystem());
            world.Tick();

            Assert.That(state.Npcs[farmer].Needs.Hunger, Is.EqualTo(100),
                "The farm meal is served at noon sharp.");
        }

        [Test]
        public void FarmMealIsFree()
        {
            WorldState state = TestWorld(out World world);
            NpcId farmer = RegisterNpc(state, "npc_test_corvin", Farm);
            int before = Wallet(state, farmer).Balance;

            world.RegisterSystem(new FarmMealSystem());
            world.Tick();

            Assert.That(Wallet(state, farmer).Balance, Is.EqualTo(before),
                "Home-grown food costs the household no copper.");
        }

        private static WorldState TestWorld(out World world)
        {
            var state = new WorldState(42, new GameTime(MealHour * 60 - 1));
            world = new World(state);
            return state;
        }

        private static NpcId RegisterNpc(WorldState state, string id, LocationId home)
        {
            var npcId = new NpcId(id);
            var definition = new NpcDefinition(npcId, id, 30, "test", "tester", home, home, 0,
                new Dictionary<string, int> { ["honest"] = 50 },
                new NeedRates(0, 0, 0),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            var npc = new NpcState(definition, 100, 80, 50);
            state.Npcs.Register(npc);
            state.Knowledge.Register(npcId);
            var owner = ActorId.ForNpc(npcId);
            state.Belongings.Register(owner, new Inventory(new ItemCatalog(new ItemDefinition[0])),
                new Wallet(100));
            return npcId;
        }

        private static Wallet Wallet(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Wallet;
    }
}
