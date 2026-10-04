namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Shared town-stat constants (P5-01, TOWN.md). Lives in Economy so both the
    /// Economy calculators and the Agents town-stats calculator use one definition.
    /// </summary>
    public static class TownStatNumbers
    {
        /// <summary>
        /// Abstracted background villagers added to the simulated NPC count for the
        /// population stat (WORLD.md: ~120 people, 20 simulated in full).
        /// </summary>
        public const int BackgroundVillagers = 100;
    }
}
