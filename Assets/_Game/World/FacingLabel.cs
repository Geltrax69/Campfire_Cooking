using UnityEngine;
namespace LivingWorld.Game.World
{
    /// <summary>Faces small world-space labels toward the walkthrough camera.</summary>
    public sealed class FacingLabel : MonoBehaviour
    {
        private void LateUpdate() { if (Camera.main != null) transform.rotation = Camera.main.transform.rotation; }
    }
}
