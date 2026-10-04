using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// The NPCs living under one roof (P7-02). Membership is world truth owned by
    /// the household; each member's NpcState also carries the household ID so both
    /// directions stay in sync through FamilySetup and FamilySystem.
    /// </summary>
    public sealed class Household
    {
        private readonly List<NpcId> _members = new List<NpcId>();

        public Household(HouseholdId id, LocationId home)
        {
            if (!id.IsValid) throw new ArgumentException("A household ID is required.", nameof(id));
            if (!home.IsValid) throw new ArgumentException("A home location is required.", nameof(home));
            Id = id;
            Home = home;
        }

        public HouseholdId Id { get; }
        public LocationId Home { get; }
        /// <summary>Members in insertion order; callers needing ordinal order must sort.</summary>
        public IReadOnlyList<NpcId> Members => _members.AsReadOnly();
        public int MemberCount => _members.Count;

        public bool HasMember(NpcId member) => member.IsValid && _members.Contains(member);

        /// <summary>Adds a member; adding the same NPC twice is a no-op.</summary>
        public void AddMember(NpcId member)
        {
            if (!member.IsValid) throw new ArgumentException("A member ID is required.", nameof(member));
            if (!_members.Contains(member)) _members.Add(member);
        }

        /// <summary>Removes a member; returns false when the NPC was not a member.</summary>
        public bool RemoveMember(NpcId member)
        {
            if (!member.IsValid) throw new ArgumentException("A member ID is required.", nameof(member));
            return _members.Remove(member);
        }
    }
}
