using System.Collections;
using System.IO;
using System.Linq;
using LivingWorld.Game.Bridge;
using LivingWorld.Game.Player;
using LivingWorld.Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LivingWorld.Game.World.Tests
{
    /// <summary>Walks the whole village, talks to every placed NPC through every topic, and captures the tour.</summary>
    public sealed class VillageTourTests
    {
        private const int CaptureWidth = 1366;
        private const int CaptureHeight = 1024;

        [UnityTest]
        public IEnumerator TourVisitsEveryNpcShopsAndShowsTheDayCycle()
        {
#if !ENABLE_INPUT_SYSTEM
            Assert.Fail("Input System must be active; restart Unity after changing Active Input Handling.");
#endif
            yield return SceneManager.LoadSceneAsync("AppleShopSlice");
            yield return null;
            var walker = Object.FindFirstObjectByType<TownWalker>();
            var runner = Object.FindFirstObjectByType<WorldRunner>();
            var hud = Object.FindFirstObjectByType<ShopHud>();
            var controller = walker.GetComponent<CharacterController>();
            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.Failure, Is.Null, "The tour needs a running village.");

            SetCaptureSize(CaptureWidth, CaptureHeight);
            Directory.CreateDirectory("TestResults");

            // The tour needs a frozen world for stable captures and exact counters; the
            // village otherwise keeps living at one minute per second while we talk.
            runner.SetPaused(true);
            AdvanceToClock(runner, 12 * 60);
            yield return null;
            ScreenCapture.CaptureScreenshot("TestResults/tour-00-village-start.png");
            yield return null;
            yield return null;

            var npcs = Object.FindObjectsByType<NpcInteraction>(FindObjectsSortMode.None)
                .OrderBy(item => item.NpcId, System.StringComparer.Ordinal).ToArray();
            Assert.That(npcs.Length, Is.EqualTo(10), "The central slice places ten approved NPCs; the rest live outside it.");

            var topics = (ConversationTopic[])System.Enum.GetValues(typeof(ConversationTopic));
            var snapshotNames = runner.Snapshot.Npcs.ToDictionary(item => item.Id, item => item.Name);
            var visited = 0;
            foreach (var npc in npcs)
            {
                Assert.That(snapshotNames.ContainsKey(npc.NpcId), Is.True, npc.NpcId + " must exist in the simulation snapshot.");
                // Stand the walker two metres south of the NPC, the way a player walking up would arrive.
                Teleport(controller, new Vector3(npc.transform.position.x, 0.15f, npc.transform.position.z - 2.2f));
                yield return null;
                Assert.That(npc.NameLabel, Is.Not.Null);
                Assert.That(npc.NameLabel.text, Is.EqualTo(snapshotNames[npc.NpcId]),
                    "The floating label must show the approved identity.");

                npc.OpenConversation();
                yield return null;
                Assert.That(hud.IsModalOpen, Is.True, snapshotNames[npc.NpcId] + " must open a conversation.");
                var dialogue = hud.GetComponent<UIDocument>().rootVisualElement.Q("npc-dialogue");
                Assert.That(dialogue.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(dialogue.Query<Label>().ToList().Any(label => label.text == snapshotNames[npc.NpcId]), Is.True,
                    "The panel must carry the NPC's approved name.");

                foreach (var topic in topics)
                {
                    PressTopic(dialogue, TopicButton(topic));
                    yield return null;
                    Assert.That(LongestLine(dialogue).Length, Is.GreaterThan(3),
                        TopicButton(topic) + " for " + npc.NpcId + " must produce a spoken reply.");
                }

                ScreenCapture.CaptureScreenshot("TestResults/tour-" + (visited + 1).ToString("00") + "-" + npc.NpcId + ".png");
                yield return null;
                yield return null;
                hud.HideNpc();
                Assert.That(hud.IsModalOpen, Is.False);
                visited++;
            }

            // Shop: buy, then steal, then let the shopkeeper count her stock overnight.
            // Scenario buyers empty the stall during the morning, so assert deltas, not absolute stock.
            var mira = npcs.Single(item => item.NpcId == "npc_mira_holt");
            Teleport(controller, new Vector3(mira.transform.position.x, 0.15f, mira.transform.position.z - 2.2f));
            yield return null;
            hud.ShowShop();
            yield return null;
            Assert.That(hud.IsModalOpen, Is.True);
            var shopPanel = hud.GetComponent<UIDocument>().rootVisualElement.Q("apple-shop");
            Assert.That(shopPanel.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            var stockBefore = runner.Snapshot.ShopApples;
            var copperBefore = runner.Snapshot.PlayerCopper;
            PressHudButton(shopPanel, "Buy one apple");
            AdvanceToClock(runner, 13 * 60);
            Assert.That(runner.Snapshot.ShopApples, Is.EqualTo(stockBefore - 1), "Buying must move one apple at the next tick.");
            Assert.That(runner.Snapshot.PlayerCopper, Is.EqualTo(copperBefore - 3), "An apple costs three copper.");
            Assert.That(runner.Snapshot.PlayerApples, Is.EqualTo(1), "The player starts with no apples.");

            PressHudButton(shopPanel, "Take six · steal");
            AdvanceToClock(runner, 14 * 60);
            Assert.That(runner.Snapshot.ShopApples, Is.EqualTo(stockBefore - 7), "Stealing must remove six apples without payment.");
            Assert.That(runner.Snapshot.PlayerApples, Is.EqualTo(7), "Stolen apples join the player's stock.");
            Assert.That(runner.Snapshot.PlayerCopper, Is.EqualTo(copperBefore - 3), "Stealing must not move money.");
            ScreenCapture.CaptureScreenshot("TestResults/tour-20-shop-after-theft.png");
            yield return null;
            yield return null;

            // The living world answers the next morning: Mira counts the missing apples.
            hud.HideShop();
            AdvanceToClock(runner, 10 * 60);
            Assert.That(runner.Failure, Is.Null, "The village must survive a full day of ticks.");
            yield return null;
            mira.OpenConversation();
            yield return null;
            var miraDialogue = hud.GetComponent<UIDocument>().rootVisualElement.Q("npc-dialogue");
            PressTopic(miraDialogue, "Apple stall");
            StringAssert.Contains("6 apples are missing", LongestLine(miraDialogue),
                "The shopkeeper must count the stolen apples.");
            ScreenCapture.CaptureScreenshot("TestResults/tour-21-mira-notices-theft.png");
            yield return null;
            yield return null;
            hud.HideNpc();
            Assert.That(hud.IsModalOpen, Is.False);

            // One day as the light moves: dusk, night and dawn over the square.
            Teleport(controller, new Vector3(3f, 0.15f, -8f));
            yield return null;
            AdvanceToClock(runner, 19 * 60 + 30);
            ScreenCapture.CaptureScreenshot("TestResults/tour-30-dusk.png");
            yield return null;
            yield return null;
            AdvanceToClock(runner, 23 * 60 + 30);
            ScreenCapture.CaptureScreenshot("TestResults/tour-31-night.png");
            yield return null;
            yield return null;
            AdvanceToClock(runner, 6 * 60 + 30);
            Assert.That(runner.Failure, Is.Null, "The village must survive the whole day cycle.");
            ScreenCapture.CaptureScreenshot("TestResults/tour-32-dawn.png");
            yield return null;
            yield return null;

            // A short real walk across the square with the walk animation playing.
            var before = walker.transform.position;
            for (var i = 0; i < 20; i++) walker.MoveInput(Vector2.right, 0.02f);
            ScreenCapture.CaptureScreenshot("TestResults/tour-33-walking.png");
            Assert.That(Vector3.Distance(before, walker.transform.position), Is.GreaterThan(0.4f),
                "The tour must end with the player able to walk away.");
            yield return null;
        }

        private static string TopicButton(ConversationTopic topic)
        {
            return topic == ConversationTopic.AboutPlayer ? "About me"
                : topic == ConversationTopic.ShopStock ? "Apple stall" : topic.ToString();
        }

        private static void PressTopic(VisualElement dialogue, string buttonText)
        {
            var button = dialogue.Query<Button>().ToList().Single(item => item.text == buttonText);
            Assert.That(button.enabledInHierarchy, Is.True, buttonText + " must be clickable.");
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private static void PressHudButton(VisualElement panel, string buttonText)
        {
            var button = panel.Query<Button>().ToList().Single(item => item.text == buttonText);
            Assert.That(button.enabledInHierarchy, Is.True, buttonText + " must be enabled.");
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private static string LongestLine(VisualElement dialogue)
        {
            return dialogue.Query<Label>().ToList()
                .Where(label => label.text != "Close")
                .OrderByDescending(label => label.text.Length)
                .First().text;
        }

        private static void Teleport(CharacterController controller, Vector3 position)
        {
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static void AdvanceToClock(WorldRunner runner, int minuteOfDay)
        {
            var current = (int)(runner.Snapshot.Minute % 1440);
            var target = minuteOfDay > current ? minuteOfDay - current : 1440 - current + minuteOfDay;
            runner.SetPaused(false);
            runner.AdvanceMinutes(target);
            runner.SetPaused(true);
        }

        private static void SetCaptureSize(int width, int height)
        {
#if UNITY_EDITOR
            // Screen.SetResolution controls a player window, not the editor Game View.
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes,
                new[] { System.Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = System.Activator.CreateInstance(sizeType,
                new[] { System.Enum.Parse(kindType, "FixedResolution"), (object)width, height, "Living World tour" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            var count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = UnityEditor.EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .SetValue(view, count - 1);
            view.Repaint();
#else
            Screen.SetResolution(width, height, false);
#endif
        }
    }
}
