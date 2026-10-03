using System;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Identifies the player or a particular NPC without sharing their identity domains.</summary>
    public readonly struct ActorId : IEquatable<ActorId>
    {
        private ActorId(bool isPlayer, NpcId? npc)
        {
            IsPlayer = isPlayer;
            Npc = npc;
        }

        public static ActorId Player => new ActorId(true, null);
        public static ActorId ForNpc(NpcId npc)
        {
            if (!npc.IsValid) throw new ArgumentException("An NPC actor needs a valid ID.", nameof(npc));
            return new ActorId(false, npc);
        }

        public bool IsPlayer { get; }
        public NpcId? Npc { get; }
        public bool IsValid => IsPlayer ? !Npc.HasValue : Npc.HasValue && Npc.Value.IsValid;
        public bool Equals(ActorId other) => IsPlayer == other.IsPlayer && Npc == other.Npc;
        public override bool Equals(object obj) => obj is ActorId other && Equals(other);
        public override int GetHashCode() => IsPlayer.GetHashCode() ^ Npc.GetHashCode();
        public static bool operator ==(ActorId left, ActorId right) => left.Equals(right);
        public static bool operator !=(ActorId left, ActorId right) => !left.Equals(right);
    }
}
