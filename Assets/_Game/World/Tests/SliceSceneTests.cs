using System.Collections;
using System.IO;
using LivingWorld.Game.Bridge;
using LivingWorld.Game.UI;
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
