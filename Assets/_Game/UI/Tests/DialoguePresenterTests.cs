using LivingWorld.Game.Bridge;
using NUnit.Framework;
namespace LivingWorld.Game.UI.Tests
{
    public sealed class DialoguePresenterTests
    {
        [Test] public void OpeningAndChangingTopicUsesSelectedNpcAndExactBridgeLine()
        {
            string id = null; ConversationTopic topic = ConversationTopic.Farewell;
            var presenter = new DialoguePresenter((selected, requested) => { id = selected; topic = requested; return "Known fact only."; });
            presenter.Open("npc_anna");
            Assert.That(id, Is.EqualTo("npc_anna")); Assert.That(topic, Is.EqualTo(ConversationTopic.Greeting));
            presenter.Say(ConversationTopic.News);
            Assert.That(topic, Is.EqualTo(ConversationTopic.News)); Assert.That(presenter.Line, Is.EqualTo("Known fact only."));
        }
        [Test] public void ConversationFailureIsRecoverable()
        {
            bool fail = true;
            var presenter = new DialoguePresenter((id, topic) => { if (fail) throw new System.InvalidOperationException("Not ready"); return "Welcome."; });
            presenter.Open("unknown"); Assert.That(presenter.Line, Does.Contain("Not ready"));
            fail = false; presenter.Say(ConversationTopic.Greeting); Assert.That(presenter.Line, Is.EqualTo("Welcome."));
        }
    }
}
