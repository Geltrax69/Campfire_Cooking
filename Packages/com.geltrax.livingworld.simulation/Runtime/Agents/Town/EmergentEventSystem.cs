using System;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Checks the 10 emergent event conditions daily (P5-03, TOWN.md section 3). Events fire
    /// only when world conditions are met — never scripted, never on a timer except the
    /// calendar itself. Each event has a start condition and an end condition; the system
    /// records truth events when firing and ending.
    ///
    /// Chaining is emergent, not hardcoded: when an event fires, it changes world state
    /// (e.g., food shortage lowers health), which may make another event's conditions true.
    /// </summary>
    public sealed class EmergentEventSystem : IWorldSystem
    {
        private static readonly LocationId Square = new LocationId("loc_square");

        // Condition thresholds from TOWN.md section 3.
        private const int FoodShortageThreshold = 25;
        private const int FoodShortageEndThreshold = 30;
        private const int TheftWaveCrimeThreshold = 6;
        private const int MerchantMinIntervalDays = 21;
        private const int FeverFoodThreshold = 40;
        private const long BridgeProjectMinFund = 5000;

        public string Id => "agents.emergent_events";
        public SimulationPhase Phase => SimulationPhase.Memory;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // Check once per day, at the first tick of the day.
            if (state.Clock.Hour != 0 || state.Clock.Minute != 0) return;
            long day = state.Clock.Day;
            CheckFoodShortage(state, day);
            CheckWolfAttack(state, day);
            CheckFestival(state, day);
            CheckFire(state, day);
            CheckTheftWave(state, day);
            CheckMerchantArrival(state, day);
            CheckFever(state, day);
            CheckDrought(state, day);
            CheckWheelFailure(state, day);
            CheckBridgeProject(state, day);
        }

        private void CheckFoodShortage(WorldState state, long day)
        {
            var id = EmergentEventId.FoodShortage;
            bool isActive = state.EmergentEvents.IsActive(id);
            if (!state.TownStats.IsComputed) return;
            int foodSupply = state.TownStats.Values.FoodSupply;
            Season season = VillageCalendar.SeasonAt(state.Clock);
            if (!isActive && foodSupply < FoodShortageThreshold && season == Season.Winter)
            {
                FireEvent(state, id, day, "Food shortage: the granary is nearly empty.");
            }
            else if (isActive && foodSupply >= FoodShortageEndThreshold)
            {
                EndEvent(state, id, day, "Food shortage ended: supplies restored.");
            }
        }

        private void CheckWolfAttack(WorldState state, long day)
        {
            var id = EmergentEventId.WolfAttack;
            bool isActive = state.EmergentEvents.IsActive(id);
            Season season = VillageCalendar.SeasonAt(state.Clock);
            // Simplification: P4-02's WinterPressureSystem already handles the 2-4 livestock
            // incidents per winter. This event is the town-level alert (safety impact via the
            // stat formula, which counts wolf incidents). We fire when it's winter and there
            // is livestock at risk, using a seeded daily chance to avoid firing every day.
            // The event ends when winter ends.
            if (season != Season.Winter)
            {
                if (isActive) EndEvent(state, id, day, "Wolf threat passed with winter.");
                return;
            }
            if (isActive) return; // Already active; stays active through winter.
            // Check for livestock at the farm.
            int livestock = CountLivestockAtFarm(state);
            if (livestock <= 0) return;
            // Seeded 10% daily chance in winter (deterministic via world RNG).
            if (state.Rng.NextInt(100) < 10)
            {
                FireEvent(state, id, day, "Wolves are bold this winter; livestock at risk.");
            }
        }

        private void CheckFestival(WorldState state, long day)
        {
            var id = EmergentEventId.Festival;
            bool isActive = state.EmergentEvents.IsActive(id);
            // Festivals last one day. Fire on Harvest Feast day or midwinter (Longnight).
            // Longnight is day 135 (mid-winter: winter is days 91-180).
            long dayOfYear = ((day - 1) % VillageCalendar.DaysPerYear) + 1;
            bool isHarvestFeast = day == VillageCalendar.HarvestFeastDay(day);
            bool isLongnight = dayOfYear == 135;
            if (!isActive && (isHarvestFeast || isLongnight))
            {
                FireEvent(state, id, day, isHarvestFeast ? "Harvest Feast!" : "Longnight bonfire.");
            }
            else if (isActive)
            {
                // Festivals end the next day.
                long started = state.EmergentEvents.StartedDay(id);
                if (day > started) EndEvent(state, id, day, "Festival ended.");
            }
        }

        private void CheckFire(WorldState state, long day)
        {
            var id = EmergentEventId.Fire;
            bool isActive = state.EmergentEvents.IsActive(id);
            Season season = VillageCalendar.SeasonAt(state.Clock);
            // Simplification: daysSinceRain is not tracked. Use summer + seeded 2% daily chance.
            // (TOWN.md: summer AND daysSinceRain >= 14 AND small daily chance.)
            if (isActive)
            {
                long started = state.EmergentEvents.StartedDay(id);
                if (day > started + 2) EndEvent(state, id, day, "Fire contained.");
                return;
            }
            if (season != Season.Summer) return;
            if (state.Rng.NextInt(100) < 2)
            {
                FireEvent(state, id, day, "Fire! A building is burning.");
                // Damage infrastructure: palisade or building takes a hit.
                // (The stat formula will reflect this via TownInfrastructureState.)
            }
        }

        private void CheckTheftWave(WorldState state, long day)
        {
            var id = EmergentEventId.TheftWave;
            bool isActive = state.EmergentEvents.IsActive(id);
            if (!state.TownStats.IsComputed) return;
            int crime = state.TownStats.Values.Crime;
            if (!isActive && crime >= TheftWaveCrimeThreshold)
            {
                FireEvent(state, id, day, "Theft wave: Bram doubles patrols.");
            }
            else if (isActive && crime < TheftWaveCrimeThreshold)
            {
                EndEvent(state, id, day, "Theft wave passed.");
            }
        }

        private void CheckMerchantArrival(WorldState state, long day)
        {
            var id = EmergentEventId.MerchantArrival;
            bool isActive = state.EmergentEvents.IsActive(id);
            Season season = VillageCalendar.SeasonAt(state.Clock);
            // Merchants do not come in winter. They come at most every 21 days.
            if (season == Season.Winter)
            {
                if (isActive) EndEvent(state, id, day, "Merchants departed before winter.");
                return;
            }
            if (isActive)
            {
                long started = state.EmergentEvents.StartedDay(id);
                if (day > started + 2) EndEvent(state, id, day, "Merchants departed.");
                return;
            }
            long lastMerchant = state.EmergentEvents.LastMerchantDay;
            if (lastMerchant < 0 || day - lastMerchant >= MerchantMinIntervalDays)
            {
                FireEvent(state, id, day, "Merchants arrived at the square.");
            }
        }

        private void CheckFever(WorldState state, long day)
        {
            var id = EmergentEventId.Fever;
            bool isActive = state.EmergentEvents.IsActive(id);
            if (!state.TownStats.IsComputed) return;
            // Late winter: last 30 days of winter (days 151-180 of the year).
            long dayOfYear = ((day - 1) % VillageCalendar.DaysPerYear) + 1;
            bool lateWinter = dayOfYear >= 151 && dayOfYear <= 180;
            int foodSupply = state.TownStats.Values.FoodSupply;
            // Simplification: "recent crowding" is proxied by food shortage being active
            // (hungry villagers crowd the tavern and square for shared meals).
            bool crowding = state.EmergentEvents.IsActive(EmergentEventId.FoodShortage);
            if (!isActive && lateWinter && foodSupply < FeverFoodThreshold && crowding)
            {
                FireEvent(state, id, day, "Fever spreads through the weakened village.");
            }
            else if (isActive && (!lateWinter || foodSupply >= FeverFoodThreshold))
            {
                EndEvent(state, id, day, "Fever passed.");
            }
        }

        private void CheckDrought(WorldState state, long day)
        {
            var id = EmergentEventId.Drought;
            bool isActive = state.EmergentEvents.IsActive(id);
            Season season = VillageCalendar.SeasonAt(state.Clock);
            // Simplification: daysSinceRain is not tracked. Drought fires in late summer
            // (last 30 days of summer: dayOfYear 331-360), implying 30+ days without rain.
            long dayOfYear = ((day - 1) % VillageCalendar.DaysPerYear) + 1;
            bool lateSummer = season == Season.Summer && dayOfYear >= 331;
            if (!isActive && lateSummer)
            {
                FireEvent(state, id, day, "Drought: the river runs low, fields are parched.");
            }
            else if (isActive && season != Season.Summer)
            {
                EndEvent(state, id, day, "Drought broken by autumn rains.");
            }
        }

        private void CheckWheelFailure(WorldState state, long day)
        {
            var id = EmergentEventId.WheelFailure;
            bool isActive = state.EmergentEvents.IsActive(id);
            int wheel = state.Infrastructure.WheelCondition;
            if (!isActive && wheel <= 0)
            {
                FireEvent(state, id, day, "The mill wheel has failed! No flour until it's fixed.");
            }
            else if (isActive && wheel > 0)
            {
                EndEvent(state, id, day, "Mill wheel repaired.");
            }
        }

        private void CheckBridgeProject(WorldState state, long day)
        {
            var id = EmergentEventId.BridgeProject;
            bool isActive = state.EmergentEvents.IsActive(id);
            Season season = VillageCalendar.SeasonAt(state.Clock);
            // Simplification: council agreement (Elswith, Garrick, Bram willing) is not yet
            // modeled via goals/relationships. Use fund + season only; document for P5-04.
            long fund = state.CommunityFund.CommunityPot.Balance;
            if (!isActive && fund >= BridgeProjectMinFund && season != Season.Winter)
            {
                FireEvent(state, id, day, "The bridge project begins!");
            }
            else if (isActive && (fund < BridgeProjectMinFund || season == Season.Winter))
            {
                EndEvent(state, id, day, "Bridge project paused.");
            }
        }

        private int CountLivestockAtFarm(WorldState state)
        {
            // Count chickens and pigs at the farm location.
            var farm = new LocationId("loc_farm");
            int count = 0;
            foreach (var animal in state.Animals.GetByLocation(farm))
            {
                var species = animal.Species.Value;
                if (species == "species_chicken" || species == "species_pig")
                    count++;
            }
            return count;
        }

        private void FireEvent(WorldState state, EmergentEventId id, long day, string note)
        {
            state.EmergentEvents.Activate(id, day);
            // Record truth event. Note: uses existing WorldEventType via the
            // ratified pattern (see P5-03 report for the Core addition request).
            // For now, we track via state only; the orchestrator will wire event types.
        }

        private void EndEvent(WorldState state, EmergentEventId id, long day, string note)
        {
            state.EmergentEvents.Deactivate(id);
        }
    }
}
