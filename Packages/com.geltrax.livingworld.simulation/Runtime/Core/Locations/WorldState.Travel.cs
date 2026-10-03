using System;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Integrates optional travel state and clock-authoritative departures into world state.</summary>
    public sealed partial class WorldState
    {
        public TravelState Travel { get; private set; }

        public void InitializeTravel(LocationMap map)
        {
            if (Travel != null) throw new InvalidOperationException("Travel is already initialized.");
            Travel = new TravelState(map ?? throw new ArgumentNullException(nameof(map)));
        }

        public void StartTravel(NpcId npc, LocationId destination)
        {
            if (Travel == null) throw new InvalidOperationException("Travel must be initialized first.");
            Travel.StartTravel(npc, destination, Clock, Events);
        }
    }

    /// <summary>Completes due journeys during Actions; register explicitly with the world runner.</summary>
    public sealed class TravelSystem : IWorldSystem
    {
        public string Id => "core.travel";
        public SimulationPhase Phase => SimulationPhase.Actions;
        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.Travel?.CompleteArrivals(state.Clock, state.Events);
        }
    }
}
