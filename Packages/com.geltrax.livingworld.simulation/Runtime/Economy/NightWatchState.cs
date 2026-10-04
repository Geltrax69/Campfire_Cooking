namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Whether a standing night watch patrols the village (P5-01, TOWN.md). An active
    /// watch adds to the safety stat; events and player actions toggle it (later phases).
    /// </summary>
    public sealed class NightWatchState
    {
        public NightWatchState(bool active = false)
        {
            IsActive = active;
        }

        public bool IsActive { get; private set; }

        public void SetActive(bool active) => IsActive = active;
    }
}
