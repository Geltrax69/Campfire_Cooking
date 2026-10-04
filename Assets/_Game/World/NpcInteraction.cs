using LivingWorld.Game.Bridge;
using LivingWorld.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LivingWorld.Game.World
{
    /// <summary>Static workplace preview that opens only this NPC's bridge conversation.</summary>
    public sealed class NpcInteraction : MonoBehaviour
    {
        public string NpcId;
        public string ModelPath;
        public WorldRunner Runner;
        public ShopHud Hud;
        public Camera ViewCamera;
        public TextMesh NameLabel;
        private void Start()
        {
            if (Runner == null || Runner.Snapshot == null) return;
            foreach (var npc in Runner.Snapshot.Npcs)
                if (npc.Id == NpcId)
                {
                    if (NameLabel != null) NameLabel.text = npc.Name;
                    if (ModelPath != npc.ModelPath) Debug.LogWarning("NPC preview model differs from approved bridge model: " + NpcId, this);
                    return;
                }
        }
        public void OpenConversation() { if (Hud != null) Hud.ShowNpc(NpcId); }
        private void Update()
        {
            if (NameLabel != null && ViewCamera != null)
                NameLabel.transform.rotation = ViewCamera.transform.rotation;
            if (Hud == null || Hud.IsModalOpen || ViewCamera == null) return;
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            if (WorldPointer.IsBlockedByUi(pointer.position.ReadValue())) return;
            if (Physics.Raycast(ViewCamera.ScreenPointToRay(pointer.position.ReadValue()), out var hit, 100)
                && hit.collider.GetComponentInParent<NpcInteraction>() == this) OpenConversation();
        }
    }
}
