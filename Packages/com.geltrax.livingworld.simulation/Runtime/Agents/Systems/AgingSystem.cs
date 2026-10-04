using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Caller-owned, restorable record of aging: the last day processed.</summary>
    public sealed class AgingState
    {
        public AgingState(bool initialized = false, long lastAgingDay = 0)
        {
            IsInitialized = initialized;
            LastAgingDay = lastAgingDay;
        }

        public bool IsInitialized { get; }
        public long LastAgingDay { get; internal set; }
    }

    /// <summary>
    /// NPC aging (agents.aging, Actions phase): once per day, every living NPC
    /// whose deterministic birthday (a day-of-year from the NPC ID hash) matches
    /// the day turns one year older. Elders face a yearly old-age death roll on
    /// their birthday — 5% at 60-69, 15% at 70-79, 40% at 80+ — using the world's
    /// seeded RNG. Death is recorded as world truth at the NPC's home; the body
    /// stays registered so inheritance (P7-03) can find it.
    /// </summary>
    public sealed class AgingSystem : IWorldSystem
    {
        /// <summary>The village calendar's year length (VillageCalendar.DaysPerYear).</summary>
        public const int DaysPerYear = 360;
        /// <summary>Yearly old-age death chance for ages 60-69, in percent.</summary>
        public const int DeathChanceSixties = 5;
        /// <summary>Yearly old-age death chance for ages 70-79, in percent.</summary>
        public const int DeathChanceSeventies = 15;
        /// <summary>Yearly old-age death chance for ages 80 and up, in percent.</summary>
        public const int DeathChanceEightyPlus = 40;

        public string Id => "agents.aging";
        public SimulationPhase Phase => SimulationPhase.Actions;

        /// <summary>
        /// Deterministic birthday: day-of-year 1-360 from the NPC ID's FNV-1a hash
        /// (NpcId.GetHashCode is defined over it, unlike string.GetHashCode).
        /// </summary>
        public static int BirthdayDayOfYear(NpcId npcId)
        {
            if (!npcId.IsValid) throw new ArgumentException("An NPC ID is required.", nameof(npcId));
            return (int)((uint)npcId.GetHashCode() % DaysPerYear) + 1;
        }

        /// <summary>Yearly old-age death chance in percent for the given age; 0 below 60.</summary>
        public static int DeathChancePerYear(int age)
        {
            if (age < 60) return 0;
            if (age < 70) return DeathChanceSixties;
            if (age < 80) return DeathChanceSeventies;
            return DeathChanceEightyPlus;
        }

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Aging.IsInitialized) return;
            AgingState progress = state.Aging;
            while (progress.LastAgingDay < state.Clock.Day)
            {
                progress.LastAgingDay++;
                ProcessDay(state, progress.LastAgingDay);
            }
        }

        private static void ProcessDay(WorldState state, long day)
        {
            int dayOfYear = (int)((day - 1) % DaysPerYear) + 1;
            var time = new GameTime((day - 1) * 1440);
            foreach (NpcState npc in state.Npcs.Npcs)
            {
                if (npc.IsDeceased) continue;
                if (BirthdayDayOfYear(npc.Definition.Id) != dayOfYear) continue;
                npc.AdvanceAge();
                int chance = DeathChancePerYear(npc.Age);
                if (chance > 0 && state.Rng.NextInt(100) < chance)
                {
                    npc.MarkDeceased();
                    state.Events.Append(time, npc.Definition.Home, WorldEventType.Death,
                        actor: ActorId.ForNpc(npc.Definition.Id),
                        visibility: EventVisibility.Normal);
                }
            }
        }
    }
}
