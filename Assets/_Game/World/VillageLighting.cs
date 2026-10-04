using UnityEngine;
using LivingWorld.Game.Bridge;
namespace LivingWorld.Game.World
{
    /// <summary>Reads game minutes for lighting only, without changing simulation facts.</summary>
    public sealed class VillageLighting : MonoBehaviour
    {
        // Kenney flat colours need restrained direct and ambient light to preserve material contrast in URP.
        public const float NoonIntensity = 0.82f;
        public const float NightIntensity = 0.08f;
        public static readonly Color DayAmbient = new Color(0.34f, 0.38f, 0.36f);
        public static readonly Color NightAmbient = new Color(0.22f, 0.25f, 0.32f);
        public WorldRunner Runner;
        public Light Sun;
        private void Update()
        {
            if (Runner == null || Runner.Snapshot == null || Sun == null) return;
            var hour = (Runner.Snapshot.Minute % 1440) / 60f;
            var daylight = Mathf.Clamp01(Mathf.Sin((hour - 6) / 12f * Mathf.PI));
            Sun.transform.rotation = Quaternion.Euler((hour - 6) * 15, -35, 0);
            Sun.intensity = Mathf.Lerp(NightIntensity, NoonIntensity, daylight);
            RenderSettings.ambientLight = Color.Lerp(NightAmbient, DayAmbient, daylight);
        }
    }
}
