using LivingWorld.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LivingWorld.Game.World
{
    /// <summary>Opens the shop presentation when a pointer taps its visible collider.</summary>
    public sealed class ShopInteraction : MonoBehaviour
    {
        public ShopHud Hud;
        public Camera ViewCamera;

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (ViewCamera == null || Hud == null) return;
            var document = Hud.GetComponent<UIDocument>();
            var root = document == null ? null : document.rootVisualElement;
            if (root != null && root.panel != null)
            {
                var screen = pointer.position.ReadValue();
                screen.y = Screen.height - screen.y;
                var picked = root.panel.Pick(RuntimePanelUtils.ScreenToPanel(root.panel, screen));
                if (picked != null && picked != root) return;
            }
            if (Physics.Raycast(ViewCamera.ScreenPointToRay(pointer.position.ReadValue()), out var hit, 100f)
                && hit.collider.GetComponentInParent<ShopInteraction>() == this)
                Hud.ShowShop();
        }
    }
}
