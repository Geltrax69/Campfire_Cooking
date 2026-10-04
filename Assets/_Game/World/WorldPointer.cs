using UnityEngine;
using UnityEngine.UIElements;

namespace LivingWorld.Game.World
{
    /// <summary>Keeps taps on every interface panel from reaching world colliders.</summary>
    public static class WorldPointer
    {
        public static bool IsBlockedByUi(Vector2 screenPointer)
        {
            screenPointer.y = Screen.height - screenPointer.y;
            foreach (var document in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var root = document.rootVisualElement;
                if (root == null || root.panel == null) continue;
                var picked = root.panel.Pick(RuntimePanelUtils.ScreenToPanel(root.panel, screenPointer));
                while (picked != null && picked != root)
                {
                    if (picked is Button || picked is ScrollView || picked.name == "apple-shop" || picked.name == "npc-dialogue") return true;
                    picked = picked.parent;
                }
            }
            return false;
        }
    }
}
