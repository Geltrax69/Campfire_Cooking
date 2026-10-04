using System.Collections;
using System.IO;
using LivingWorld.Game.Bridge;
using LivingWorld.Game.UI;
using LivingWorld.Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using System.Linq;

namespace LivingWorld.Game.World.Tests
{
    /// <summary>Validates generated scene wiring and captures actual runtime presentation.</summary>
    public sealed class SliceSceneTests
    {
        [UnityTest]
        public IEnumerator SceneWiresShopColliderAndCapturesLandscapeUi()
        {
#if !ENABLE_INPUT_SYSTEM
            Assert.Fail("Input System must be active; restart Unity after changing Active Input Handling.");
#endif
            yield return SceneManager.LoadSceneAsync("AppleShopSlice");
            yield return null;
            var runner = Object.FindFirstObjectByType<WorldRunner>();
            var hud = Object.FindFirstObjectByType<ShopHud>();
            var shop = Object.FindFirstObjectByType<ShopInteraction>();
            Assert.That(runner, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(shop.Hud, Is.SameAs(hud));
            Assert.That(shop.ViewCamera, Is.SameAs(Camera.main));
            Assert.That(shop.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(Camera.main.GetComponent<AudioListener>(), Is.Not.Null);
            hud.ShowShop();
            runner.SetPaused(true);
            yield return null; // Allow the opened panel to complete layout before dispatch.
            Assert.That(runner.Failure, Is.Null);
            var button = hud.GetComponent<UIDocument>().rootVisualElement.Query<Button>().ToList()
                .Single(item => item.text == "Buy one apple");
            Assert.That(button.enabledInHierarchy, Is.True, "Bound HUD must allow buying from initial stock.");
            Assert.That(button.panel, Is.Not.Null);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            yield return null;
            Assert.That(button.enabledSelf, Is.False, "A dispatched buy must disable the button while the command is pending.");
            Assert.That(runner.Snapshot.ShopApples, Is.EqualTo(20), "UI must queue the purchase until a tick.");
            runner.SetPaused(false);
            runner.AdvanceMinutes(1);
            runner.SetPaused(true);
            Assert.That(runner.Snapshot.ShopApples, Is.EqualTo(19));
            Assert.That(runner.Snapshot.PlayerApples, Is.EqualTo(1));
            Assert.That(runner.Snapshot.PlayerCopper, Is.EqualTo(12));
            Directory.CreateDirectory("TestResults");
            SetCaptureSize(1366, 1024);
            yield return null;
            yield return null; // WaitForEndOfFrame does not resume in editor batchmode.
            Assert.That(Screen.width, Is.EqualTo(1366));
            Assert.That(Screen.height, Is.EqualTo(1024));
            ScreenCapture.CaptureScreenshot("TestResults/apple-shop-ipad.png");
            yield return null;
            SetCaptureSize(844, 390);
            yield return null;
            yield return null; // WaitForEndOfFrame does not resume in editor batchmode.
            Assert.That(Screen.width, Is.EqualTo(844));
            Assert.That(Screen.height, Is.EqualTo(390));
            ScreenCapture.CaptureScreenshot("TestResults/apple-shop-small-landscape.png");
            yield return null;
        }
        [UnityTest]
        public IEnumerator WalkthroughMovesAndStopsAtBoundsAndTalksToNamedNpc()
        {
            yield return SceneManager.LoadSceneAsync("AppleShopSlice");
            yield return null;
            var walker = Object.FindFirstObjectByType<TownWalker>();
            var hud = Object.FindFirstObjectByType<ShopHud>();
            Assert.That(walker, Is.Not.Null);
            Assert.That(walker.IdleClip, Is.Not.Null);
            Assert.That(walker.WalkClip, Is.Not.Null);
            Assert.That(walker.GetComponentInChildren<SkinnedMeshRenderer>(), Is.Not.Null, "The walker needs a visible character.");
            var start = walker.transform.position;
            for (var i = 0; i < 10; i++) walker.MoveInput(Vector2.right, 0.02f);
            Assert.That(Vector3.Distance(start, walker.transform.position), Is.GreaterThan(0.3f));
            var controller = walker.GetComponent<CharacterController>();
            controller.enabled = false;
            walker.transform.position = new Vector3(29.8f, 0.2f, 20);
            controller.enabled = true;
            Physics.SyncTransforms();
            for (var i = 0; i < 50; i++) controller.Move(Vector3.right * 0.1f);
            Assert.That(walker.transform.position.x, Is.LessThan(30.3f), "Bounds must stop escape from the town ground.");
            var tavern = GameObject.Find("The Hearthside").GetComponent<BoxCollider>();
            controller.enabled = false;
            walker.transform.position = new Vector3(tavern.bounds.center.x, 0.2f, tavern.bounds.min.z - 1);
            controller.enabled = true;
            Physics.SyncTransforms();
            for (var i = 0; i < 40; i++) controller.Move(Vector3.forward * 0.1f);
            Assert.That(walker.transform.position.z, Is.LessThan(tavern.bounds.min.z), "The tavern wall must block walking through the building.");
            var runner = Object.FindFirstObjectByType<WorldRunner>();
            var lighting = Object.FindFirstObjectByType<VillageLighting>();
            runner.SetPaused(false);
            runner.AdvanceMinutes((int)(720 - runner.Snapshot.Minute % 1440));
            runner.SetPaused(true);
            yield return null;
            var noonIntensity = lighting.Sun.intensity;
            runner.SetPaused(false);
            runner.AdvanceMinutes(720);
            runner.SetPaused(true);
            yield return null;
            Assert.That(lighting.Sun.intensity, Is.LessThan(noonIntensity), "Light must follow simulation time at midnight.");
            runner.SetPaused(false);
            runner.AdvanceMinutes(720); // Return to noon for readable dialogue captures.
            runner.SetPaused(true);
            controller.enabled = false;
            walker.transform.position = new Vector3(4, 0.15f, -7);
            controller.enabled = true;
            yield return null;
            var npc = Object.FindObjectsByType<NpcInteraction>(FindObjectsSortMode.None).Single(item => item.NpcId == "npc_mira_holt");
            npc.OpenConversation();
            yield return null;
            Assert.That(hud.IsModalOpen, Is.True);
            var dialogue = hud.GetComponent<UIDocument>().rootVisualElement.Q("npc-dialogue");
            Assert.That(dialogue.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(dialogue.Query<Label>().ToList().Any(label => label.text.Contains("Mira Holt")), Is.True);
            var talkingPosition = walker.transform.position;
            walker.MoveInput(Vector2.up, 0.1f);
            Assert.That(Vector2.Distance(new Vector2(talkingPosition.x, talkingPosition.z),
                new Vector2(walker.transform.position.x, walker.transform.position.z)), Is.LessThan(0.001f));
            var smalltalk = dialogue.Query<Button>().ToList().Single(button => button.text == "Smalltalk");
            Assert.That(smalltalk.enabledInHierarchy, Is.True);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = smalltalk;
                smalltalk.SendEvent(submit);
            }
            yield return null;
            Assert.That(dialogue.Query<Label>().ToList().Any(label => label.text.Contains("apple-stall owner")), Is.True,
                "The selected topic must return Mira's approved occupation through the bridge.");
            Directory.CreateDirectory("TestResults");
            SetCaptureSize(1366, 1024);
            yield return null;
            yield return null;
            Assert.That(Screen.width, Is.EqualTo(1366));
            Assert.That(Screen.height, Is.EqualTo(1024));
            var close = dialogue.Query<Button>().ToList().Single(button => button.text == "Close");
            Assert.That(close.worldBound.Overlaps(dialogue.worldBound), Is.True, "Close must remain inside the visible dialogue panel.");
            ScreenCapture.CaptureScreenshot("TestResults/npc-dialogue-ipad.png");
            yield return null;
            SetCaptureSize(844, 390);
            yield return null;
            yield return null;
            Assert.That(Screen.width, Is.EqualTo(844));
            Assert.That(Screen.height, Is.EqualTo(390));
            Assert.That(close.worldBound.Overlaps(dialogue.worldBound), Is.True);
            Assert.That(dialogue.Q<ScrollView>(), Is.Not.Null, "Dialogue choices must scroll on short screens.");
            ScreenCapture.CaptureScreenshot("TestResults/npc-dialogue-small-landscape.png");
            yield return null;
            hud.HideNpc();
            Assert.That(hud.IsModalOpen, Is.False);
        }

#if UNITY_EDITOR
        [Test]
        public void ProjectSettingsEnableInputSystem()
        {
            var settings = new UnityEditor.SerializedObject(
                UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var inputHandler = settings.FindProperty("activeInputHandler");
            Assert.That(inputHandler, Is.Not.Null);
            Assert.That(inputHandler.intValue, Is.Not.EqualTo(0), "Legacy-only input disables shop pointer handling.");
        }
#endif
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
                new[] { System.Enum.Parse(kindType, "FixedResolution"), (object)width, height, "Living World capture" });
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
