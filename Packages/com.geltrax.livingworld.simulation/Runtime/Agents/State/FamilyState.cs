namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Caller-owned, restorable record of family processing (P7-02): the last day
    /// processed and how many babies have been born (baby IDs are
    /// "npc_born_&lt;n&gt;", so the counter keeps them unique across save/load).
    /// </summary>
    public sealed class FamilyState
    {
        public FamilyState(bool initialized = false, long lastFamilyDay = 0, long birthsSoFar = 0)
        {
            IsInitialized = initialized;
            LastFamilyDay = lastFamilyDay;
            BirthsSoFar = birthsSoFar;
        }

        public bool IsInitialized { get; }
        public long LastFamilyDay { get; internal set; }
        public long BirthsSoFar { get; internal set; }
    }
}
