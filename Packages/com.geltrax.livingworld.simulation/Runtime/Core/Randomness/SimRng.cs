using System;

namespace LivingWorld.Simulation.Core
{
    /// <summary>A deterministic SplitMix64 generator with a single saveable 64-bit state.</summary>
    public sealed class SimRng
    {
        public ulong State { get; private set; }

        public SimRng(ulong seed) { State = seed; }

        public static SimRng FromState(ulong state) => new SimRng(state);

        public ulong NextUInt64()
        {
            // SplitMix64's fixed increment and mixing constants define the saved sequence.
            // Modulo-2^64 overflow is intentional, independent of compiler overflow settings.
            unchecked
            {
                State += 0x9E3779B97F4A7C15UL;
                ulong value = State;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        /// <summary>Returns an unbiased value in [0, exclusiveMax), consuming one or more draws.</summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax), "Bound must be positive.");

            ulong bound = (ulong)exclusiveMax;
            // Reject the surplus prefix so the accepted range has equal counts per remainder.
            ulong threshold = unchecked(0UL - bound) % bound;
            ulong value;
            do { value = NextUInt64(); } while (value < threshold);
            return (int)(value % bound);
        }
    }
}
