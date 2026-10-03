using System;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Defines whole need points changed during one awake game hour.</summary>
    public sealed class NeedRates
    {
        public NeedRates(int hungerPerHour, int energyPerHour, int socialPerHour)
        {
            if (hungerPerHour < 0) throw new ArgumentOutOfRangeException(nameof(hungerPerHour));
            if (energyPerHour < 0) throw new ArgumentOutOfRangeException(nameof(energyPerHour));
            if (socialPerHour < 0) throw new ArgumentOutOfRangeException(nameof(socialPerHour));
            HungerPerHour = hungerPerHour;
            EnergyPerHour = energyPerHour;
            SocialPerHour = socialPerHour;
        }

        public int HungerPerHour { get; }
        public int EnergyPerHour { get; }
        public int SocialPerHour { get; }
    }
}
