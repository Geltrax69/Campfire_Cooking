using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// The tavern's renown as a place to eat, 0-100, starting at 50 (P3-03, SKILLS.md
    /// section 2). A cook working the tavern kitchen shifts it by (skill level - 2):
    /// level 3+ raises it, level 1 lowers it a little. People forget: without skilled
    /// cooking it drifts back toward 50. High popularity keeps travelers longer —
    /// see <see cref="TavernPopularitySystem.ExtraNights"/>.
    /// </summary>
    public sealed class TavernPopularityState
    {
        /// <summary>Where popularity starts and where forgetfulness returns it.</summary>
        public const int Baseline = 50;

        public TavernPopularityState(int popularity = Baseline)
        {
            if (popularity < 0 || popularity > 100)
                throw new ArgumentOutOfRangeException(nameof(popularity), "Popularity is 0-100.");
            Popularity = popularity;
        }

        public int Popularity { get; private set; }

        /// <summary>
        /// The last game day a cook of skill level 2+ worked the tavern kitchen
        /// (-1 when none ever has); the decay system reads this to know whether the
        /// village still remembers good cooking.
        /// </summary>
        public long LastSkilledCookDay { get; internal set; } = -1;

        /// <summary>The last game day the decay system ran; it decays at most once a day.</summary>
        public long LastDecayDay { get; internal set; } = -1;

        /// <summary>Shifts popularity by a signed amount, clamped to 0-100.</summary>
        public void Shift(int delta)
        {
            int shifted = Popularity + delta;
            if (shifted < 0) shifted = 0;
            if (shifted > 100) shifted = 100;
            Popularity = shifted;
        }

        internal void DecayOnePointTowardBaseline()
        {
            if (Popularity < Baseline) Popularity++;
            else if (Popularity > Baseline) Popularity--;
        }
    }

    /// <summary>
    /// Lets tavern popularity fade: once per game day, when no skilled cook worked
    /// the tavern kitchen yesterday, popularity moves one point back toward 50.
    /// A single skilled cook's work is remembered for a day; daily cooking sustains
    /// it. Runs in the Economy phase.
    /// </summary>
    public sealed class TavernPopularitySystem : IWorldSystem
    {
        /// <summary>
        /// Extra nights travelers stay per 20 popularity above 50: 70 -> 1 night,
        /// 90 -> 2 nights. Below 50 the tavern is unremarkable, not repellent, so
        /// there is no penalty — only the bonus side exists.
        /// </summary>
        public static int ExtraNights(int popularity)
        {
            if (popularity < 0 || popularity > 100)
                throw new ArgumentOutOfRangeException(nameof(popularity), "Popularity is 0-100.");
            return popularity <= TavernPopularityState.Baseline
                ? 0
                : (popularity - TavernPopularityState.Baseline) / 20;
        }

        public string Id => "economy.tavern-popularity";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            TavernPopularityState popularity = state.TavernPopularity;
            long today = state.Clock.Day;
            if (popularity.LastDecayDay >= today) return;
            popularity.LastDecayDay = today;
            // A skilled cook yesterday keeps the memory alive; otherwise the village
            // starts forgetting, one point a day.
            if (popularity.LastSkilledCookDay >= today - 1) return;
            popularity.DecayOnePointTowardBaseline();
        }
    }
}
