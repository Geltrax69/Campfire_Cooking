using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Immutable endpoints and absolute arrival time for an active journey.</summary>
    public sealed class TravelJourney
    {
        public LocationId Origin { get; }
        public LocationId Destination { get; }
        public GameTime Arrival { get; }
        internal TravelJourney(LocationId origin, LocationId destination, GameTime arrival)
        {
            Origin = origin;
            Destination = destination;
            Arrival = arrival;
        }
    }

    /// <summary>Read-only NPC travel view; a travelling NPC has no current location.</summary>
    public sealed class NpcTravelState
    {
        public NpcId Id { get; }
        public LocationId? CurrentLocation { get; internal set; }
        public TravelJourney Journey { get; internal set; }
        internal NpcTravelState(NpcId id, LocationId location) { Id = id; CurrentLocation = location; }
    }

    /// <summary>Owns registered NPC travel state, kept in ordinal ID order at registration.</summary>
    public sealed class TravelState
    {
        private readonly SortedList<NpcId, NpcTravelState> _npcs = new SortedList<NpcId, NpcTravelState>();
        public LocationMap Map { get; }
        public IReadOnlyList<NpcTravelState> Npcs { get; }
        public NpcTravelState this[NpcId id]
        {
            get
            {
                if (!id.IsValid || !_npcs.TryGetValue(id, out var npc))
                    throw new ArgumentException("An NPC must be registered before travel.", nameof(id));
                return npc;
            }
        }

        internal TravelState(LocationMap map)
        {
            Map = map;
            Npcs = new ReadOnlyCollection<NpcTravelState>(_npcs.Values);
        }

        public void RegisterNpc(NpcId id, LocationId location)
        {
            if (!id.IsValid) throw new ArgumentException("An NPC ID must be valid.", nameof(id));
            var place = Map[location];
            _npcs.Add(id, new NpcTravelState(id, place.Id));
        }

        internal void StartTravel(NpcId id, LocationId destination, GameTime now, EventLog events)
        {
            var npc = this[id];
            if (npc.Journey != null) throw new InvalidOperationException("NPC is already travelling.");
            LocationId origin = npc.CurrentLocation.Value;
            long? duration = Map.GetTravelMinutes(origin, destination);
            if (!duration.HasValue) throw new InvalidOperationException("Destination is unreachable.");
            if (duration.Value == 0) return;
            // Validate time and append truth before removing presence; a failed log must leave the NPC put.
            var journey = new TravelJourney(origin, destination, now.Advance(duration.Value));
            events.Append(now, origin, WorldEventType.Departure, ActorId.ForNpc(id));
            npc.Journey = journey;
            npc.CurrentLocation = null;
        }

        internal void CompleteArrivals(GameTime now, EventLog events)
        {
            for (int i = 0; i < Npcs.Count; i++)
            {
                var npc = Npcs[i];
                if (npc.Journey == null || npc.Journey.Arrival > now) continue;
                // An overdue move happens now, not retroactively at its scheduled arrival minute.
                events.Append(now, npc.Journey.Destination, WorldEventType.Arrival, ActorId.ForNpc(npc.Id));
                npc.CurrentLocation = npc.Journey.Destination;
                npc.Journey = null;
            }
        }
    }
}
