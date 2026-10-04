using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Meal quality effects: eating a cooked meal shifts happiness by
    /// round((quality - 50) / 25) (SKILLS.md section 2 — a small daily happiness that
    /// compounds), records a MealEaten truth event carrying the quality, and giving a
    /// good meal to someone is a social act: trust rises via the P2-03 gift rule plus
    /// a direct quality bonus for great meals. Health effects are future work: no
    /// health system exists yet (recorded in OPEN_QUESTIONS).
    /// </summary>
    [TestFixture]
    public sealed class MealEffectsTests
    {
        private static readonly ItemTypeId Stew = new ItemTypeId("item_stew");
        private static readonly LocationId Home = new LocationId("loc_test_home");

        [Test]
        public void HighQualityMealRaisesHappinessMore()
        {
            WorldState state = TestWorld();
            NpcId great = RegisterNpc(state, "npc_test_alda");
            NpcId plain = RegisterNpc(state, "npc_test_bram");

            MealEffects.EatCookedMeal(state.Npcs[great], Stew, 80, state, Home);
            MealEffects.EatCookedMeal(state.Npcs[plain], Stew, 50, state, Home);

            Assert.That(state.Npcs[great].Happiness, Is.EqualTo(51),
                "Quality 80: round((80 - 50) / 25) = +1.");
            Assert.That(state.Npcs[plain].Happiness, Is.EqualTo(50),
                "Quality 50 is average: happiness does not move.");
        }

        [Test]
        public void LowQualityMealLowersHappiness()
        {
            WorldState state = TestWorld();
            NpcId npc = RegisterNpc(state, "npc_test_alda");

            MealEffects.EatCookedMeal(state.Npcs[npc], Stew, 25, state, Home);

            Assert.That(state.Npcs[npc].Happiness, Is.EqualTo(49),
                "Quality 25: round((25 - 50) / 25) = -1.");
        }

        [Test]
        public void MealEatenIsRecordedAsTruthEvent()
        {
            WorldState state = TestWorld();
            NpcId npc = RegisterNpc(state, "npc_test_alda");

            MealEffects.EatCookedMeal(state.Npcs[npc], Stew, 80, state, Home);

            WorldEvent meal = AssertSingleMealEvent(state);
            Assert.That(meal.Actor, Is.EqualTo((ActorId?)ActorId.ForNpc(npc)));
            Assert.That(meal.ItemType, Is.EqualTo((ItemTypeId?)Stew));
            Assert.That(meal.Quantity, Is.EqualTo((int?)80),
                "The event's quantity carries the meal quality (0-100), documented on the event type.");
        }

        [Test]
        public void MealQualityValidationRejectsBadInput()
        {
            WorldState state = TestWorld();
            NpcId npc = RegisterNpc(state, "npc_test_alda");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MealEffects.EatCookedMeal(state.Npcs[npc], Stew, 101, state, Home));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MealEffects.EatCookedMeal(state.Npcs[npc], Stew, -1, state, Home));
            Assert.Throws<ArgumentNullException>(() =>
                MealEffects.EatCookedMeal(null, Stew, 80, state, Home));
        }

        [Test]
        public void FeedingRaisesTrust()
        {
            // A quality-80 meal: the P2-03 gift rule gives +1 trust and the great-meal
            // bonus adds +1 directly, for +2 total once the Social phase runs.
            World world = TestWorldWithDynamics(out WorldState state);
            NpcId giver = RegisterNpc(state, "npc_test_alda");
            NpcId receiver = RegisterNpc(state, "npc_test_bram", hunger: 60);
            Pantry(state, giver).Add(Stew, 1);

            MealEffects.GiveMeal(state, TestCatalog(), giver, receiver, Stew, 80, Home);

            Assert.That(Pantry(state, giver).Count(Stew), Is.EqualTo(0),
                "The meal leaves the giver's hands.");
            Assert.That(Hunger(state, receiver), Is.EqualTo(40),
                "The receiver eats the stew at once: 60 - 20 hunger.");
            Assert.That(state.Npcs[receiver].Happiness, Is.EqualTo(51),
                "Quality 80 is a +1-happiness meal for the receiver too.");
            Assert.That(Trust(state, receiver, giver), Is.EqualTo(51),
                "Great meal: +1 trust immediately, before the Social phase.");
            Assert.That(state.Events.Query(type: WorldEventType.Gift), Has.Count.EqualTo(1),
                "Feeding is recorded as a gift for the P2-03 relationship system.");

            world.Tick();

            Assert.That(Trust(state, receiver, giver), Is.EqualTo(52),
                "The gift rule adds its +1 trust in the Social phase: +2 total for quality >= 80.");
        }

        [Test]
        public void GoodMealGiftRaisesTrustByOne()
        {
            // Quality 60-79: no direct bonus; the P2-03 gift rule alone gives +1 trust.
            World world = TestWorldWithDynamics(out WorldState state);
            NpcId giver = RegisterNpc(state, "npc_test_alda");
            NpcId receiver = RegisterNpc(state, "npc_test_bram");
            Pantry(state, giver).Add(Stew, 1);

            MealEffects.GiveMeal(state, TestCatalog(), giver, receiver, Stew, 60, Home);

            Assert.That(Trust(state, receiver, giver), Is.EqualTo(50),
                "No direct bonus below quality 80.");
            Assert.That(state.Events.Query(type: WorldEventType.Gift), Has.Count.EqualTo(1));

            world.Tick();

            Assert.That(Trust(state, receiver, giver), Is.EqualTo(51),
                "The gift rule's +1 trust applies in the Social phase.");
        }

        [Test]
        public void PoorMealGiftRaisesNoTrust()
        {
            World world = TestWorldWithDynamics(out WorldState state);
            NpcId giver = RegisterNpc(state, "npc_test_alda");
            NpcId receiver = RegisterNpc(state, "npc_test_bram");
            Pantry(state, giver).Add(Stew, 1);

            MealEffects.GiveMeal(state, TestCatalog(), giver, receiver, Stew, 40, Home);

            Assert.That(Trust(state, receiver, giver), Is.EqualTo(50));
            Assert.That(state.Events.Query(type: WorldEventType.Gift), Is.Empty,
                "A poor meal is no gift: no trust, and nothing for the dynamics system.");

            world.Tick();

            Assert.That(Trust(state, receiver, giver), Is.EqualTo(50));
        }

        [Test]
        public void GivingWithoutTheMealThrows()
        {
            WorldState state = TestWorld();
            NpcId giver = RegisterNpc(state, "npc_test_alda");
            NpcId receiver = RegisterNpc(state, "npc_test_bram");

            Assert.Throws<InvalidOperationException>(() =>
                MealEffects.GiveMeal(state, TestCatalog(), giver, receiver, Stew, 80, Home));
        }

        [Test]
        public void AboveAverageMealLiftsHappiness()
        {
            // The reachable cooking band is quality 50-54 (level bonus on a 50
            // base): a level-3 cook's stew (quality 52) must be the small daily
            // happiness the design promises (WORLD.md section 8, SKILLS.md
            // section 1), not a neutral meal.
            Assert.That(MealEffects.HappinessDeltaForQuality(52), Is.EqualTo(1),
                "Bessa's level-3 campfire stew lifts mood by 1.");
            Assert.That(MealEffects.HappinessDeltaForQuality(54), Is.EqualTo(1),
                "A level-5 cook's ordinary dish still lifts mood by 1.");
            Assert.That(MealEffects.HappinessDeltaForQuality(51), Is.EqualTo(1));
        }

        [Test]
        public void BelowAverageMealLowersHappiness()
        {
            Assert.That(MealEffects.HappinessDeltaForQuality(49), Is.EqualTo(-1));
            Assert.That(MealEffects.HappinessDeltaForQuality(40), Is.EqualTo(-1),
                "A poor meal is a small daily unhappiness, symmetric with the lift.");
        }

        [Test]
        public void ExceptionalMealsMoveHappinessByTwo()
        {
            Assert.That(MealEffects.HappinessDeltaForQuality(90), Is.EqualTo(2),
                "A masterpiece (quality 85+) is remembered longer.");
            Assert.That(MealEffects.HappinessDeltaForQuality(100), Is.EqualTo(2));
            Assert.That(MealEffects.HappinessDeltaForQuality(10), Is.EqualTo(-2));
            Assert.That(MealEffects.HappinessDeltaForQuality(0), Is.EqualTo(-2));
        }

        [Test]
        public void AverageMealLeavesHappinessAlone()
        {
            Assert.That(MealEffects.HappinessDeltaForQuality(50), Is.EqualTo(0),
                "An exactly average meal is no event.");
        }

        private static WorldEvent AssertSingleMealEvent(WorldState state)
        {
            var meals = state.Events.Query(type: WorldEventType.MealEaten);
            Assert.That(meals, Has.Count.EqualTo(1), "Exactly one MealEaten truth event.");
            return meals[0];
        }

        private static ItemCatalog TestCatalog() => new ItemCatalog(new[]
        {
            new ItemDefinition(Stew, "Campfire stew", "food", 6, 1, hungerEffect: -20),
        });

        private static WorldState TestWorld() => new WorldState(42, new GameTime(0));

        private static World TestWorldWithDynamics(out WorldState state)
        {
            state = TestWorld();
            var world = new World(state);
            world.RegisterSystem(new RelationshipDynamicsSystem());
            return world;
        }

        private static NpcId RegisterNpc(WorldState state, string id, int hunger = 50)
        {
            var npcId = new NpcId(id);
            var definition = new NpcDefinition(npcId, id, 30, "test", "tester", Home, Home, 0,
                new Dictionary<string, int> { ["honest"] = 50 },
                new NeedRates(0, 0, 0),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            state.Npcs.Register(new NpcState(definition, hunger, 80, 50));
            state.Knowledge.Register(npcId);
            state.Belongings.Register(ActorId.ForNpc(npcId), new Inventory(TestCatalog()), new Wallet(0));
            return npcId;
        }

        private static Inventory Pantry(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Inventory;

        private static int Hunger(WorldState state, NpcId npc) => state.Npcs[npc].Needs.Hunger;

        private static int Trust(WorldState state, NpcId from, NpcId to) =>
            state.Knowledge.Relationships.Trust(from, to);
    }
}
