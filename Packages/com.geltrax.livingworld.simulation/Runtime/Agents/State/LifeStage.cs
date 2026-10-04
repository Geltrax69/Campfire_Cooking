namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// An NPC's stage of life, computed from age (P7-01): children are 0-14,
    /// adults 15-59, elders 60 and up. Elders face the yearly old-age death roll.
    /// </summary>
    public enum LifeStage
    {
        Child,
        Adult,
        Elder
    }
}
