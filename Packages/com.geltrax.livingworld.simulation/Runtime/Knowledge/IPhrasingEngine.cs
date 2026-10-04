namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Turns an NPC fact sheet into spoken dialogue (P8-02).
    ///
    /// Contract every implementation must obey:
    /// <list type="bullet">
    /// <item>The output may phrase only facts present in the sheet. It must not
    /// add facts, reveal anything the NPC does not know, or invent proper nouns
    /// and numbers.</item>
    /// <item>Phrasing is pure: it never changes world state.</item>
    /// <item>Phrasing is deterministic for the same (sheet, intent) given the
    /// engine's configuration. An engine that varies wording takes its seed as
    /// a constructor input, so the seed is part of the input, not hidden state.</item>
    /// </list>
    ///
    /// TemplatePhrasingEngine is the deterministic simulation-side
    /// implementation. A future AI model adapter would implement this same
    /// interface and obey the same contract.
    /// </summary>
    public interface IPhrasingEngine
    {
        /// <summary>Phrases the sheet for the given intent. Pure and deterministic.</summary>
        string Phrase(FactSheet sheet, DialogueIntent intent);
    }
}
