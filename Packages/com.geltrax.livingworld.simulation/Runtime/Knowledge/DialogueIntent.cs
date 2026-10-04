namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Why the player is talking to the NPC (P8-02): what kind of line the
    /// phrasing engine should produce. The engine phrases only facts already
    /// present in the fact sheet; the intent selects which ones.
    /// </summary>
    public enum DialogueIntent
    {
        Greeting = 0,
        Farewell = 1,
        Smalltalk = 2,
        AskAboutPlayer = 3,
        ShareNews = 4,
        AskAboutFamily = 5,
    }
}
