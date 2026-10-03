using System;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Immutable location metadata; a null owner denotes communal ownership.</summary>
    public sealed class LocationDefinition
    {
        public LocationId Id { get; }
        public string Name { get; }
        public string Type { get; }
        public int X { get; }
        public int Y { get; }
        public NpcId? Owner { get; }

        public LocationDefinition(LocationId id, string name, string type, int x, int y, NpcId? owner = null)
        {
            if (!id.IsValid) throw new ArgumentException("A location ID must be valid.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("A type is required.", nameof(type));
            if (owner.HasValue && !owner.Value.IsValid) throw new ArgumentException("An owner ID must be valid.", nameof(owner));
            Id = id;
            Name = name;
            Type = type;
            X = x;
            Y = y;
            Owner = owner;
        }
    }

    /// <summary>Immutable, undirected walking link with an explicit positive integer duration.</summary>
    public sealed class TravelLink
    {
        public LocationId From { get; }
        public LocationId To { get; }
        public int Minutes { get; }

        public TravelLink(LocationId from, LocationId to, int minutes)
        {
            if (!from.IsValid) throw new ArgumentException("An origin ID must be valid.", nameof(from));
            if (!to.IsValid || from == to) throw new ArgumentException("A distinct destination ID is required.", nameof(to));
            if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes));
            From = from;
            To = to;
            Minutes = minutes;
        }
    }
}
