using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Inheritance (P7-03): when an NPC dies, their money and items pass to
    /// heirs (designated heir → spouse → children → parents → village fund).
    /// Money is conserved; every transfer is recorded as truth.
    /// </summary>
    public sealed class InheritanceTests
    {
        private static readonly LocationId Home = new LocationId("loc_home_test");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");

        private static WorldState TestWorld(ulong seed = 42)
        {
            var state = new WorldState(seed, new GameTime(0));
            state.RestoreVillageFund(new VillageFundState(initialized: true, startingCopper: 1000));
            return state;
        }

        private static NpcId RegisterNpc(WorldState state, string id, int age,
            int copper = 0, int apples = 0)
        {
            var npcId = new NpcId(id);
            var definition = new NpcDefinition(npcId, id, age, "other", "tester",
                Home, Home, 0,
                new Dictionary<string, int>(),
                new NeedRates(6, 4, 4),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            state.Npcs.Register(new NpcState(definition, 30, 80, 50));
            var owner = ActorId.ForNpc(npcId);
            var inventory = new Inventory(TestCatalog());
            if (apples > 0) inventory.Add(Apple, apples);
            state.Belongings.Register(owner, inventory, new Wallet(copper));
            return npcId;
        }

        private static ItemCatalog TestCatalog()
        {
            return new ItemCatalog(new[]
            {
                new ItemDefinition(Apple, "Apple", "food", 3, 1)
            });
        }

        private static int WalletOf(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Wallet.Balance;

        private static int ApplesOf(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Inventory.Count(Apple);

        private static int TotalMoney(WorldState state)
        {
            int total = state.VillageFund.Funds.Balance;
            foreach (var entry in state.Belongings.Entries)
                total += entry.Wallet.Balance;
            return total;
        }

        [Test]
        public void SpouseInheritsFirst()
        {
            var state = TestWorld();
            var husband = RegisterNpc(state, "npc_husband", 40, copper: 100, apples: 5);
            var wife = RegisterNpc(state, "npc_wife", 38, copper: 20);
            state.Npcs[husband].SetPartner(wife);
            state.Npcs[wife].SetPartner(husband);

            state.Npcs[husband].MarkDeceased();
            Inheritance.Distribute(state, husband);

            Assert.That(WalletOf(state, husband), Is.EqualTo(0), "Deceased wallet is emptied.");
            Assert.That(WalletOf(state, wife), Is.EqualTo(120), "Spouse receives all money.");
            Assert.That(ApplesOf(state, wife), Is.EqualTo(5), "Spouse receives all items.");
            Assert.That(ApplesOf(state, husband), Is.EqualTo(0));
        }

        [Test]
        public void ChildrenSplitEqually()
        {
            var state = TestWorld();
            var parent = RegisterNpc(state, "npc_parent", 50, copper: 100);
            var child1 = RegisterNpc(state, "npc_child1", 30);
            var child2 = RegisterNpc(state, "npc_child2", 28);
            var child3 = RegisterNpc(state, "npc_child3", 25);
            state.Npcs[parent].AddChild(child1);
            state.Npcs[parent].AddChild(child2);
            state.Npcs[parent].AddChild(child3);

            state.Npcs[parent].MarkDeceased();
            Inheritance.Distribute(state, parent);

            // 100 / 3 = 33 each, remainder 1 to the eldest (child1, age 30).
            Assert.That(WalletOf(state, child1), Is.EqualTo(34));
            Assert.That(WalletOf(state, child2), Is.EqualTo(33));
            Assert.That(WalletOf(state, child3), Is.EqualTo(33));
            Assert.That(WalletOf(state, parent), Is.EqualTo(0));
        }

        [Test]
        public void DeadSpouseFallsThroughToChildren()
        {
            var state = TestWorld();
            var husband = RegisterNpc(state, "npc_husband", 40, copper: 90);
            var wife = RegisterNpc(state, "npc_wife", 38);
            var child = RegisterNpc(state, "npc_child", 20);
            state.Npcs[husband].SetPartner(wife);
            state.Npcs[wife].SetPartner(husband);
            state.Npcs[husband].AddChild(child);
            state.Npcs[wife].MarkDeceased();

            state.Npcs[husband].MarkDeceased();
            Inheritance.Distribute(state, husband);

            Assert.That(WalletOf(state, child), Is.EqualTo(90), "Dead spouse is skipped; child inherits.");
            Assert.That(WalletOf(state, wife), Is.EqualTo(0));
        }

        [Test]
        public void ParentsInheritWhenNoSpouseOrChildren()
        {
            var state = TestWorld();
            var mother = RegisterNpc(state, "npc_mother", 65, copper: 10);
            var father = RegisterNpc(state, "npc_father", 67, copper: 10);
            var adult = RegisterNpc(state, "npc_adult", 40, copper: 100);
            state.Npcs[adult].SetParents(mother, father);

            state.Npcs[adult].MarkDeceased();
            Inheritance.Distribute(state, adult);

            // 100 / 2 = 50 each, no remainder.
            Assert.That(WalletOf(state, mother), Is.EqualTo(60));
            Assert.That(WalletOf(state, father), Is.EqualTo(60));
        }

        [Test]
        public void NoHeirsGoesToFund()
        {
            var state = TestWorld();
            var loner = RegisterNpc(state, "npc_loner", 45, copper: 250);

            state.Npcs[loner].MarkDeceased();
            Inheritance.Distribute(state, loner);

            Assert.That(WalletOf(state, loner), Is.EqualTo(0));
            Assert.That(state.VillageFund.Funds.Balance, Is.EqualTo(1250),
                "Money with no heirs goes to the village fund.");
        }

        [Test]
        public void MoneyConserved()
        {
            var state = TestWorld();
            var parent = RegisterNpc(state, "npc_parent", 55, copper: 500, apples: 10);
            var child1 = RegisterNpc(state, "npc_child1", 30, copper: 50);
            var child2 = RegisterNpc(state, "npc_child2", 28, copper: 70);
            state.Npcs[parent].AddChild(child1);
            state.Npcs[parent].AddChild(child2);

            int before = TotalMoney(state);
            state.Npcs[parent].MarkDeceased();
            Inheritance.Distribute(state, parent);
            int after = TotalMoney(state);

            Assert.That(after, Is.EqualTo(before), "Inheritance conserves every copper.");
        }

        [Test]
        public void MoneyConservedWhenFundInherits()
        {
            var state = TestWorld();
            var loner = RegisterNpc(state, "npc_loner", 45, copper: 333);

            int before = TotalMoney(state);
            state.Npcs[loner].MarkDeceased();
            Inheritance.Distribute(state, loner);

            Assert.That(TotalMoney(state), Is.EqualTo(before));
        }

        [Test]
        public void DesignatedHeirOverrides()
        {
            var state = TestWorld();
            var testator = RegisterNpc(state, "npc_testator", 60, copper: 200, apples: 4);
            var spouse = RegisterNpc(state, "npc_spouse", 58, copper: 10);
            var friend = RegisterNpc(state, "npc_friend", 55, copper: 5);
            state.Npcs[testator].SetPartner(spouse);
            state.Npcs[spouse].SetPartner(testator);
            state.Npcs[testator].SetDesignatedHeir(friend);

            state.Npcs[testator].MarkDeceased();
            Inheritance.Distribute(state, testator);

            Assert.That(WalletOf(state, friend), Is.EqualTo(205), "Designated heir gets everything.");
            Assert.That(ApplesOf(state, friend), Is.EqualTo(4));
            Assert.That(WalletOf(state, spouse), Is.EqualTo(10), "Spouse gets nothing when a will names another.");
        }

        [Test]
        public void DeadDesignatedHeirFallsThroughToNormalPriority()
        {
            var state = TestWorld();
            var testator = RegisterNpc(state, "npc_testator", 60, copper: 200);
            var spouse = RegisterNpc(state, "npc_spouse", 58);
            var friend = RegisterNpc(state, "npc_friend", 55);
            state.Npcs[testator].SetPartner(spouse);
            state.Npcs[spouse].SetPartner(testator);
            state.Npcs[testator].SetDesignatedHeir(friend);
            state.Npcs[friend].MarkDeceased();

            state.Npcs[testator].MarkDeceased();
            Inheritance.Distribute(state, testator);

            Assert.That(WalletOf(state, spouse), Is.EqualTo(200),
                "A dead designated heir is skipped; the spouse inherits.");
        }

        [Test]
        public void OrphanChildShareHeldByGuardian()
        {
            var state = TestWorld();
            var parent = RegisterNpc(state, "npc_parent", 40, copper: 120);
            var orphan = RegisterNpc(state, "npc_orphan", 10);
            var aunt = RegisterNpc(state, "npc_aunt", 35, copper: 40);
            var householdId = new HouseholdId("household_test");
            var household = new Household(householdId, Home);
            state.Households.Register(household);
            foreach (var member in new[] { parent, orphan, aunt })
            {
                household.AddMember(member);
                state.Npcs[member].SetHousehold(householdId);
            }
            state.Npcs[parent].AddChild(orphan);

            state.Npcs[parent].MarkDeceased();
            Inheritance.Distribute(state, parent);

            Assert.That(WalletOf(state, orphan), Is.EqualTo(0),
                "A child under 15 does not hold money directly.");
            Assert.That(WalletOf(state, aunt), Is.EqualTo(160),
                "The eldest adult in the household holds the orphan's share.");
        }

        [Test]
        public void ItemsGoToPrimaryHeir()
        {
            var state = TestWorld();
            var parent = RegisterNpc(state, "npc_parent", 50, copper: 99, apples: 7);
            var elder = RegisterNpc(state, "npc_elder", 30);
            var younger = RegisterNpc(state, "npc_younger", 25);
            state.Npcs[parent].AddChild(elder);
            state.Npcs[parent].AddChild(younger);

            state.Npcs[parent].MarkDeceased();
            Inheritance.Distribute(state, parent);

            Assert.That(ApplesOf(state, elder), Is.EqualTo(7), "All items go to the eldest child.");
            Assert.That(ApplesOf(state, younger), Is.EqualTo(0));
            // 99 / 2 = 49 each, remainder 1 to the eldest.
            Assert.That(WalletOf(state, elder), Is.EqualTo(50));
            Assert.That(WalletOf(state, younger), Is.EqualTo(49));
        }

        [Test]
        public void InheritanceRecordsTruthEvents()
        {
            var state = TestWorld();
            var husband = RegisterNpc(state, "npc_husband", 40, copper: 100);
            var wife = RegisterNpc(state, "npc_wife", 38);
            state.Npcs[husband].SetPartner(wife);
            state.Npcs[wife].SetPartner(husband);

            state.Npcs[husband].MarkDeceased();
            Inheritance.Distribute(state, husband);

            var events = state.Events.Query(type: WorldEventType.Inheritance).ToList();
            Assert.That(events, Is.Not.Empty, "Each transfer is recorded as truth.");
            Assert.That(events.All(e => e.Actor.HasValue &&
                e.Actor.Value == ActorId.ForNpc(husband)), Is.True,
                "The deceased is the actor on inheritance events.");
            Assert.That(events.Sum(e => e.Copper ?? 0), Is.EqualTo(100),
                "Recorded copper matches the estate.");
        }

        [Test]
        public void DistributeRequiresDeceasedNpc()
        {
            var state = TestWorld();
            var living = RegisterNpc(state, "npc_living", 40, copper: 100);

            Assert.That(() => Inheritance.Distribute(state, living),
                Throws.InvalidOperationException, "The living have no estate to distribute.");
            Assert.That(() => Inheritance.Distribute(state, new NpcId("npc_unknown")),
                Throws.ArgumentException, "Unknown NPC IDs are rejected.");
        }

        [Test]
        public void DesignatedHeirRejectsBadInput()
        {
            var state = TestWorld();
            var npc = RegisterNpc(state, "npc_self", 40);
            var other = RegisterNpc(state, "npc_other", 30);

            Assert.That(() => state.Npcs[npc].SetDesignatedHeir(npc),
                Throws.ArgumentException, "An NPC cannot designate itself.");
            Assert.That(() => state.Npcs[npc].SetDesignatedHeir(new NpcId()),
                Throws.ArgumentException, "Invalid IDs are rejected.");
            state.Npcs[npc].SetDesignatedHeir(other);
            Assert.That(state.Npcs[npc].DesignatedHeirId, Is.EqualTo(other));
            state.Npcs[npc].SetDesignatedHeir(null);
            Assert.That(state.Npcs[npc].DesignatedHeirId.HasValue, Is.False);
        }

        [Test]
        public void InheritanceSystemHasStableIdentity()
        {
            var system = new InheritanceSystem();
            Assert.That(system.Id, Is.EqualTo("agents.inheritance"));
            Assert.That(system.Phase, Is.EqualTo(SimulationPhase.Actions));
            Assert.That(() => system.Tick(null), Throws.ArgumentNullException);
        }

        [Test]
        public void InheritanceSystemDistributesOncePerDeath()
        {
            var state = TestWorld();
            var husband = RegisterNpc(state, "npc_husband", 40, copper: 100);
            var wife = RegisterNpc(state, "npc_wife", 38);
            state.Npcs[husband].SetPartner(wife);
            state.Npcs[wife].SetPartner(husband);

            var world = new World(state);
            world.RegisterSystem(new InheritanceSystem());
            state.RestoreInheritance(new InheritanceState(initialized: true));
            state.Npcs[husband].MarkDeceased();

            world.Tick();
            world.Tick();

            Assert.That(WalletOf(state, wife), Is.EqualTo(100),
                "The estate is distributed exactly once, not once per tick.");
            var events = state.Events.Query(type: WorldEventType.Inheritance).ToList();
            Assert.That(events.Count, Is.EqualTo(1));
        }
    }
}
