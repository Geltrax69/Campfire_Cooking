using System;
namespace LivingWorld.Game.Bridge
{
    /// <summary>Retains fractional time and bounded catch-up debt at one minute per second.</summary>
    public sealed class BridgeClock
    {
        // A nanosecond tolerance absorbs accumulated binary rounding at exact tick boundaries.
        private const double BoundaryTolerance = 1e-9;
        private double _seconds;
        public bool Paused { get; set; }
        public int Accumulate(double seconds, int budget = 8)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (budget <= 0) throw new ArgumentOutOfRangeException(nameof(budget));
            if (Paused) return 0;
            _seconds += seconds;
            int ticks = (int)Math.Min(budget, Math.Floor(_seconds + BoundaryTolerance));
            _seconds = Math.Max(0, _seconds - ticks);
            return ticks;
        }
    }
}
