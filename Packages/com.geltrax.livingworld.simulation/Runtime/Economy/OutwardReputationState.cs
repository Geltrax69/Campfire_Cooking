using System;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// How outsiders see Millbrook, 0-100 per component (P5-01, TOWN.md stat_reputation).
    /// Distinct from D-09's player reputation by group: this is the village's name on
    /// the Alder Road. Defaults are the design's starting values; traveler experiences
    /// move the components (P5-03) through Shift.
    /// </summary>
    public sealed class OutwardReputationState
    {
        public OutwardReputationState(int appleFame = 70, int roadSafety = 60, int hospitality = 65)
        {
            AppleFame = Validate(appleFame, nameof(appleFame));
            RoadSafety = Validate(roadSafety, nameof(roadSafety));
            Hospitality = Validate(hospitality, nameof(hospitality));
        }

        public int AppleFame { get; private set; }
        public int RoadSafety { get; private set; }
        public int Hospitality { get; private set; }

        /// <summary>Moves each component by a signed delta, clamped to 0-100.</summary>
        public void Shift(int appleFameDelta, int roadSafetyDelta, int hospitalityDelta)
        {
            AppleFame = Clamp(AppleFame + appleFameDelta);
            RoadSafety = Clamp(RoadSafety + roadSafetyDelta);
            Hospitality = Clamp(Hospitality + hospitalityDelta);
        }

        private static int Validate(int value, string name)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(name, "Reputation components are 0-100.");
            return value;
        }

        private static int Clamp(int value) => value < 0 ? 0 : value > 100 ? 100 : value;
    }
}
