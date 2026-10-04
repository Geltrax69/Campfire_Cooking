namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Simulation fidelity for a village (P6-01). Millbrook is Full: every NPC is
    /// simulated. Neighbors are Abstract: population, wealth, food and mood tick
    /// at low fidelity through VillageDriftSystem instead.
    /// </summary>
    public enum VillageLod
    {
        Full = 0,
        Abstract = 1,
    }
}
