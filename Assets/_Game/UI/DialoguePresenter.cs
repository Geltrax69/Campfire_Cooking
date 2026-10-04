using System;
using LivingWorld.Game.Bridge;
namespace LivingWorld.Game.UI
{
    /// <summary>Conversation selection and recoverable feedback with bridge-owned phrasing.</summary>
    public sealed class DialoguePresenter
    {
        private readonly Func<string, ConversationTopic, string> _talk;
        public string NpcId { get; private set; }
        public string Line { get; private set; } = "";
        public DialoguePresenter(Func<string, ConversationTopic, string> talk) { _talk = talk; }
        public void Open(string id) { NpcId = id; Say(ConversationTopic.Greeting); }
        public void Say(ConversationTopic topic)
        {
            try { Line = _talk(NpcId, topic); }
            catch (Exception error) { Line = "Conversation unavailable: " + error.Message; }
        }
    }
}
