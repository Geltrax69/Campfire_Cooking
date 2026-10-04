using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Families (agents.family, Actions phase): once per village year, every
    /// household with an eligible couple may welcome a baby (10%/year), and every
    /// 15-year-old may leave home to start their own household (50%). Babies are
    /// born as full NPCs (age 0) with parent links both ways, the parents'
    /// household, and a Birth truth event. A couple is two adults of opposite
    /// gender in the same household; explicit Content partners are preferred,
    /// otherwise the first eligible pair in ordinal ID order (documented
    /// simplification: at most one birth roll per household per year, and the
    /// mother must be 18-45).
    /// </summary>
    public sealed class FamilySystem : IWorldSystem
    {
        /// <summary>The village calendar's year length (AgingSystem.DaysPerYear).</summary>
        public const int DaysPerYear = 360;
        /// <summary>Yearly birth chance per eligible household, in percent.</summary>
        public const int BirthChancePercent = 10;
        /// <summary>Chance a 15-year-old leaves home in their 15th year, in percent.</summary>
        public const int LeaveHomeChancePercent = 50;
        /// <summary>Age of adulthood; children may leave home at this age.</summary>
        public const int AdultAge = 15;
        /// <summary>Mothers must be within this age range to give birth.</summary>
        public const int MinChildbearingAge = 18;
        public const int MaxChildbearingAge = 45;

        public string Id => "agents.family";
        public SimulationPhase Phase => SimulationPhase.Actions;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Family.IsInitialized) return;
            FamilyState progress = state.Family;
            while (progress.LastFamilyDay < state.Clock.Day)
            {
                progress.LastFamilyDay++;
                if (progress.LastFamilyDay % DaysPerYear == 0)
                    ProcessYear(state, progress);
            }
        }

        private static void ProcessYear(WorldState state, FamilyState progress)
        {
            var time = new GameTime((progress.LastFamilyDay - 1) * 1440);
            TryBirths(state, progress, time);
            TryLeaveHome(state);
        }

        private static void TryBirths(WorldState state, FamilyState progress, GameTime time)
        {
            foreach (Household household in state.Households.Households)
            {
                var members = household.Members
                    .Where(id => state.Npcs.Contains(id))
                    .Select(id => state.Npcs[id])
                    .Where(npc => !npc.IsDeceased && npc.Age >= AdultAge)
                    .OrderBy(npc => npc.Definition.Id)
                    .ToList();
                if (!FindCouple(members, out NpcId motherId, out NpcId fatherId)) continue;
                if (state.Rng.NextInt(100) >= BirthChancePercent) continue;
                GiveBirth(state, progress, household, motherId, fatherId, time);
            }
        }

        /// <summary>
        /// Picks the household's couple: the first childbearing-age woman with an
        /// eligible partner (explicit Content partner preferred), paired with her
        /// partner or the first adult man in ordinal ID order.
        /// </summary>
        private static bool FindCouple(List<NpcState> adults, out NpcId motherId, out NpcId fatherId)
        {
            motherId = default;
            fatherId = default;
            var women = adults
                .Where(npc => IsFemale(npc) && npc.Age >= MinChildbearingAge && npc.Age <= MaxChildbearingAge)
                .ToList();
            var men = adults.Where(IsMale).ToList();
            foreach (NpcState woman in women)
            {
                NpcState man = null;
                if (woman.PartnerId.HasValue)
                    man = men.FirstOrDefault(candidate => candidate.Definition.Id == woman.PartnerId.Value);
                if (man == null) man = men.FirstOrDefault();
                if (man == null) continue;
                motherId = woman.Definition.Id;
                fatherId = man.Definition.Id;
                return true;
            }
            return false;
        }

        private static bool IsFemale(NpcState npc) =>
            string.Equals(npc.Definition.Gender, "female", StringComparison.OrdinalIgnoreCase);
        private static bool IsMale(NpcState npc) =>
            string.Equals(npc.Definition.Gender, "male", StringComparison.OrdinalIgnoreCase);

        private static void GiveBirth(WorldState state, FamilyState progress,
            Household household, NpcId motherId, NpcId fatherId, GameTime time)
        {
            NpcState mother = state.Npcs[motherId];
            NpcState father = state.Npcs[fatherId];
            long birthNumber = progress.BirthsSoFar;
            progress.BirthsSoFar++;
            var babyId = new NpcId("npc_born_" + birthNumber);
            string gender = state.Rng.NextInt(2) == 0 ? "female" : "male";
            var definition = new NpcDefinition(babyId, "Child of " + mother.Definition.Name, 0,
                gender, "child", household.Home, household.Home, 0,
                new Dictionary<string, int>(), mother.Definition.NeedRates,
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            var baby = new NpcState(definition, 30, 80, 50);
            baby.SetParents(motherId, fatherId);
            baby.SetHousehold(household.Id);
            state.Npcs.Register(baby);
            mother.AddChild(babyId);
            father.AddChild(babyId);
            household.AddMember(babyId);
            state.Events.Append(time, household.Home, WorldEventType.Birth,
                actor: ActorId.ForNpc(babyId),
                targets: new[] { ActorId.ForNpc(motherId), ActorId.ForNpc(fatherId) },
                visibility: EventVisibility.Normal);
        }

        private static void TryLeaveHome(WorldState state)
        {
            foreach (NpcState npc in state.Npcs.Npcs)
            {
                if (npc.IsDeceased || npc.Age != AdultAge || !npc.HouseholdId.HasValue) continue;
                if (state.Rng.NextInt(100) >= LeaveHomeChancePercent) continue;
                var newId = new HouseholdId("household_" + npc.Definition.Id.Value);
                if (!state.Households.Contains(newId))
                    state.Households.Register(new Household(newId, npc.Definition.Home));
                Household oldHousehold = state.Households[npc.HouseholdId.Value];
                oldHousehold.RemoveMember(npc.Definition.Id);
                state.Households[newId].AddMember(npc.Definition.Id);
                npc.SetHousehold(newId);
            }
        }
    }
}
