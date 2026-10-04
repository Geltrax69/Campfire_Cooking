using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents.State
{
    /// <summary>
    /// Parent/child/partner links on NpcState, household membership, the
    /// household registry, and the initial family setup from approved Content.
    /// </summary>
    public sealed class FamilyTests
    {
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly LocationId MillerHome = new LocationId("loc_home_miller");

        private static NpcState MakeNpc(string id, int age, string gender, LocationId home)
        {
            var definition = new NpcDefinition(new NpcId(id), id, age, gender, "tester",
                home, home, 0,
                new Dictionary<string, int>(),
                new NeedRates(6, 4, 4),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            return new NpcState(definition, 30, 80, 50);
        }

        private static WorldState WorldWith(params NpcState[] npcs)
        {
            var state = new WorldState(1, new GameTime(0));
            foreach (var npc in npcs)
                state.Npcs.Register(npc);
            return state;
        }

        [Test]
        public void ParentChildLinksAreBidirectional()
        {
            var mother = MakeNpc("npc_mother", 40, "female", Farm);
            var father = MakeNpc("npc_father", 42, "male", Farm);
            var child = MakeNpc("npc_child", 8, "female", Farm);

            child.SetParents(mother.Definition.Id, father.Definition.Id);
            mother.AddChild(child.Definition.Id);
            father.AddChild(child.Definition.Id);

            Assert.That(child.MotherId, Is.EqualTo(mother.Definition.Id));
            Assert.That(child.FatherId, Is.EqualTo(father.Definition.Id));
            Assert.That(mother.ChildrenIds, Does.Contain(child.Definition.Id));
            Assert.That(father.ChildrenIds, Does.Contain(child.Definition.Id));
        }

        [Test]
        public void NewNpcsStartWithoutFamily()
        {
            var npc = MakeNpc("npc_lonely", 30, "male", Farm);
            Assert.That(npc.MotherId.HasValue, Is.False);
            Assert.That(npc.FatherId.HasValue, Is.False);
            Assert.That(npc.PartnerId.HasValue, Is.False);
            Assert.That(npc.ChildrenIds, Is.Empty);
            Assert.That(npc.HouseholdId.HasValue, Is.False);
        }

        [Test]
        public void SetParentsRejectsChangingToADifferentParent()
        {
            var child = MakeNpc("npc_child", 8, "female", Farm);
            var father = new NpcId("npc_father");
            var other = new NpcId("npc_other");

            child.SetParents(null, father);
            Assert.That(child.FatherId, Is.EqualTo(father));

            Assert.That(() => child.SetParents(null, other), Throws.InvalidOperationException,
                "Parents are set at birth and never change.");
            // Setting the same parent again, or filling in a missing one, is fine.
            Assert.DoesNotThrow(() => child.SetParents(new NpcId("npc_mother"), father));
            Assert.That(child.MotherId.Value.Value, Is.EqualTo("npc_mother"));
        }

        [Test]
        public void SetParentsRejectsSelfAndSameParents()
        {
            var child = MakeNpc("npc_child", 8, "female", Farm);
            Assert.That(() => child.SetParents(child.Definition.Id, null), Throws.ArgumentException,
                "An NPC cannot be its own mother.");
            Assert.That(() => child.SetParents(null, child.Definition.Id), Throws.ArgumentException,
                "An NPC cannot be its own father.");
            var parent = new NpcId("npc_parent");
            Assert.That(() => child.SetParents(parent, parent), Throws.ArgumentException,
                "Mother and father must be different NPCs.");
        }

        [Test]
        public void AddChildIgnoresDuplicates()
        {
            var parent = MakeNpc("npc_parent", 40, "female", Farm);
            var childId = new NpcId("npc_child");
            parent.AddChild(childId);
            parent.AddChild(childId);
            Assert.That(parent.ChildrenIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void PartnerLinkCanBeSetAndCleared()
        {
            var npc = MakeNpc("npc_one", 30, "male", Farm);
            var partner = new NpcId("npc_two");
            npc.SetPartner(partner);
            Assert.That(npc.PartnerId, Is.EqualTo(partner));
            npc.SetPartner(null);
            Assert.That(npc.PartnerId.HasValue, Is.False);
            Assert.That(() => npc.SetPartner(npc.Definition.Id), Throws.ArgumentException,
                "An NPC cannot partner itself.");
        }

        [Test]
        public void HouseholdMembership()
        {
            var household = new Household(new HouseholdId("household_loc_farm"), Farm);
            var member = new NpcId("npc_member");
            household.AddMember(member);
            household.AddMember(member);
            Assert.That(household.MemberCount, Is.EqualTo(1));
            Assert.That(household.HasMember(member), Is.True);
            Assert.That(household.RemoveMember(member), Is.True);
            Assert.That(household.HasMember(member), Is.False);
            Assert.That(household.RemoveMember(member), Is.False);
        }

        [Test]
        public void HouseholdRejectsBadIds()
        {
            Assert.That(() => new Household(default, Farm), Throws.ArgumentException);
            Assert.That(() => new Household(new HouseholdId("h"), default), Throws.ArgumentException);
        }

        [Test]
        public void HouseholdRegistryRoundTrip()
        {
            var registry = new HouseholdRegistry();
            var id = new HouseholdId("household_loc_farm");
            registry.Register(new Household(id, Farm));
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry[id].Home, Is.EqualTo(Farm));
            Assert.That(registry.Contains(id), Is.True);
            Assert.That(registry.Contains(new HouseholdId("household_missing")), Is.False);
            Assert.That(() => registry.Register(new Household(id, Farm)), Throws.ArgumentException,
                "Household IDs must be unique.");
        }

        [Test]
        public void FamilyContentLoaderReadsRelations()
        {
            var root = FindContentRoot();
            using var stream = File.OpenRead(Path.Combine(root, "Content/npcs/npcs.json"));
            var relations = FamilyContentLoader.Load(stream);

            var corvin = new NpcId("npc_corvin_alder");
            Assert.That(relations.ContainsKey(corvin), Is.True);
            var kinds = relations[corvin].Select(r => r.Relation).ToList();
            Assert.That(kinds, Does.Contain("wife"));
            Assert.That(kinds, Does.Contain("son"));
            Assert.That(kinds, Does.Contain("daughter"));
            var wife = relations[corvin].First(r => r.Relation == "wife");
            Assert.That(wife.Target, Is.EqualTo(new NpcId("npc_maren_alder")));
        }

        [Test]
        public void InitialFamiliesLinkContentParentsAndHouseholds()
        {
            var corvin = MakeNpc("npc_corvin_alder", 45, "male", Farm);
            var maren = MakeNpc("npc_maren_alder", 42, "female", Farm);
            var piotr = MakeNpc("npc_piotr_alder", 16, "male", Farm);
            var lida = MakeNpc("npc_lida_alder", 8, "female", Farm);
            var garrick = MakeNpc("npc_garrick_alder", 38, "male", MillerHome);
            var tansy = MakeNpc("npc_tansy_alder", 10, "female", MillerHome);
            var state = WorldWith(corvin, maren, piotr, garrick, tansy, lida);

            var root = FindContentRoot();
            using var stream = File.OpenRead(Path.Combine(root, "Content/npcs/npcs.json"));
            FamilySetup.AssignInitialFamilies(state, FamilyContentLoader.Load(stream));

            // Tansy's father is Garrick (the approved design fact).
            var tansyState = state.Npcs[new NpcId("npc_tansy_alder")];
            Assert.That(tansyState.FatherId, Is.EqualTo(new NpcId("npc_garrick_alder")));
            Assert.That(tansyState.MotherId.HasValue, Is.False, "Content names no mother for Tansy.");

            // The Alder children link both parents.
            var piotrState = state.Npcs[new NpcId("npc_piotr_alder")];
            Assert.That(piotrState.FatherId, Is.EqualTo(new NpcId("npc_corvin_alder")));
            Assert.That(piotrState.MotherId, Is.EqualTo(new NpcId("npc_maren_alder")));

            // Parents list their children.
            var corvinState = state.Npcs[new NpcId("npc_corvin_alder")];
            Assert.That(corvinState.ChildrenIds,
                Is.EquivalentTo(new[] { new NpcId("npc_piotr_alder"), new NpcId("npc_lida_alder") }));

            // Marriage is a two-way partner link.
            Assert.That(corvinState.PartnerId, Is.EqualTo(new NpcId("npc_maren_alder")));
            Assert.That(state.Npcs[new NpcId("npc_maren_alder")].PartnerId,
                Is.EqualTo(new NpcId("npc_corvin_alder")));

            // One household per home location; everyone belongs to theirs.
            Assert.That(state.Households.Count, Is.EqualTo(2));
            var farmHousehold = state.Households[new HouseholdId("household_loc_farm")];
            Assert.That(farmHousehold.MemberCount, Is.EqualTo(4));
            Assert.That(piotrState.HouseholdId, Is.EqualTo(farmHousehold.Id));
            var millerHousehold = state.Households[new HouseholdId("household_loc_home_miller")];
            Assert.That(tansyState.HouseholdId, Is.EqualTo(millerHousehold.Id));
        }

        private static string FindContentRoot()
        {
            var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Content/npcs/npcs.json")))
                root = root.Parent;
            Assert.That(root, Is.Not.Null, "Find repository content above test output.");
            return root.FullName;
        }
    }
}
