using System;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// The single read-only entry point for talking to an NPC (P8-03).
    ///
    /// Contract, enforced by construction:
    /// <list type="bullet">
    /// <item>Dialogue may phrase facts from the sheet, and only those facts.
    /// It cannot add facts, reveal anything the NPC does not know, or change
    /// game state. The phrasing engine's contract (see IPhrasingEngine) binds
    /// every implementation, including a future AI model adapter.</item>
    /// <item>The session holds only the fact sheet and the phrasing engine. It
    /// has no reference to WorldState or any mutable store, so there is no
    /// path by which dialogue could reach, read beyond, or alter the world.
    /// DialogueSessionTests.DialogueSessionHasNoWorldStateReference locks this
    /// in with a reflection check over the session's type graph.</item>
    /// <item>Building the sheet (FactSheetBuilder, which reads the world) and
    /// talking through it (this session, which cannot) are separate steps, so
    /// the world is never in scope while dialogue runs.</item>
    /// </list>
    ///
    /// Who may be talked to — for example, whether the deceased can be
    /// addressed — is the caller's decision, not the session's: the session
    /// phrases whatever sheet it receives and never throws on placeholder or
    /// deceased sheets.
    /// </summary>
    public sealed class DialogueSession
    {
        private readonly FactSheet _sheet;
        private readonly IPhrasingEngine _engine;

        /// <summary>
        /// Starts a dialogue session for one NPC's fact sheet.
        /// </summary>
        public DialogueSession(FactSheet sheet, IPhrasingEngine engine)
        {
            _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        /// <summary>
        /// Says one line for the given intent. Pure: never touches world state.
        /// </summary>
        public string Say(DialogueIntent intent) => _engine.Phrase(_sheet, intent);
    }
}
