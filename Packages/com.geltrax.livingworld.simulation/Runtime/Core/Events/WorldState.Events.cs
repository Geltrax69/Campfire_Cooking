namespace LivingWorld.Simulation.Core
{
    public sealed partial class WorldState
    {
        public EventLog Events { get; } = new EventLog();
    }
}
