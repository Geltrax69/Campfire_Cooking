namespace LivingWorld.Simulation.Core
{
    /// <summary>Owns the simulation clock, shared RNG and state added by later Core tasks.</summary>
    public sealed partial class WorldState
    {
        public GameTime Clock { get; internal set; }
        public SimRng Rng { get; }

        public WorldState(ulong seed, GameTime startTime = default)
        {
            Clock = startTime;
            Rng = new SimRng(seed);
        }
    }
}
