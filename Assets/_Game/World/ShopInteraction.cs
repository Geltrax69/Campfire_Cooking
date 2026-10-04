using LivingWorld.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
            if (ViewCamera == null || Hud == null || Hud.IsModalOpen) return;
            if (WorldPointer.IsBlockedByUi(pointer.position.ReadValue())) return;
            if (Physics.Raycast(ViewCamera.ScreenPointToRay(pointer.position.ReadValue()), out var hit, 100f)
                && hit.collider.GetComponentInParent<ShopInteraction>() == this)
                Hud.ShowShop();
        }
    }
}
