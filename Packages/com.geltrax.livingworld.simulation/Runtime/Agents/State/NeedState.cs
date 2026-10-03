using System;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Stores exact need levels in sixtieths while exposing whole 0–100 values.</summary>
    public sealed class NeedState
    {
        private const int UnitsPerPoint = 60;
        private const int MaximumSixtieths = 100 * UnitsPerPoint;

        public NeedState(int hunger, int energy, int social)
            : this(ToSixtieths(hunger, nameof(hunger)), ToSixtieths(energy, nameof(energy)),
                ToSixtieths(social, nameof(social)), true)
        {
        }

        private NeedState(int hungerSixtieths, int energySixtieths, int socialSixtieths, bool validated)
        {
            HungerSixtieths = hungerSixtieths;
            EnergySixtieths = energySixtieths;
            SocialSixtieths = socialSixtieths;
        }

        public int Hunger => HungerSixtieths / UnitsPerPoint;
        public int Energy => EnergySixtieths / UnitsPerPoint;
        public int Social => SocialSixtieths / UnitsPerPoint;
        public int HungerRemainderSixtieths => HungerSixtieths % UnitsPerPoint;
        public int EnergyRemainderSixtieths => EnergySixtieths % UnitsPerPoint;
        public int SocialRemainderSixtieths => SocialSixtieths % UnitsPerPoint;
        public int HungerSixtieths { get; private set; }
        public int EnergySixtieths { get; private set; }
        public int SocialSixtieths { get; private set; }

        public static NeedState FromSixtieths(int hungerSixtieths, int energySixtieths, int socialSixtieths)
        {
            ValidateSixtieths(hungerSixtieths, nameof(hungerSixtieths));
            ValidateSixtieths(energySixtieths, nameof(energySixtieths));
            ValidateSixtieths(socialSixtieths, nameof(socialSixtieths));
            return new NeedState(hungerSixtieths, energySixtieths, socialSixtieths, true);
        }

        internal void AdvanceOneAwakeMinute(NeedRates rates)
        {
            if (rates == null) throw new ArgumentNullException(nameof(rates));
            int hunger = Clamp((long)HungerSixtieths + rates.HungerPerHour);
            int energy = Clamp((long)EnergySixtieths - rates.EnergyPerHour);
            int social = Clamp((long)SocialSixtieths - rates.SocialPerHour);
            HungerSixtieths = hunger;
            EnergySixtieths = energy;
            SocialSixtieths = social;
        }

        private static int ToSixtieths(int value, string parameterName)
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(parameterName);
            return value * UnitsPerPoint;
        }

        private static void ValidateSixtieths(int value, string parameterName)
        {
            if (value < 0 || value > MaximumSixtieths) throw new ArgumentOutOfRangeException(parameterName);
        }

        private static int Clamp(long value)
        {
            if (value <= 0) return 0;
            if (value >= MaximumSixtieths) return MaximumSixtieths;
            return (int)value;
        }
    }
}
