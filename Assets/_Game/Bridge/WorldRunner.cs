using System;
using System.IO;
using UnityEngine;
namespace LivingWorld.Game.Bridge
{
    /// <summary>Unity time adapter; mutable simulation state remains private.</summary>
    public sealed class WorldRunner : MonoBehaviour
    {
        private AppleSlice _slice;
        private readonly BridgeClock _clock = new BridgeClock();
        public DisplaySnapshot Snapshot { get; private set; }
        public bool IsPaused => _clock.Paused;
        public string Failure { get; private set; }
        public event Action<DisplaySnapshot> SnapshotChanged;
        public event Action<string> Failed;
        private void Awake()
        {
            try
            {
                string content = Path.Combine(Application.streamingAssetsPath, "Content");
#if UNITY_EDITOR
                content = Path.GetFullPath(Path.Combine(Application.dataPath, "../Content"));
#endif
                _slice = new AppleSlice(content);
                Publish();
            }
            catch (Exception failure) { Fault(failure); }
        }
        private void Update()
        {
            if (_slice == null || Failure != null) return;
            int ticks = _clock.Accumulate(Time.unscaledDeltaTime);
            AdvanceMinutes(ticks);
        }
        public void SetPaused(bool paused) { if (Failure == null) _clock.Paused = paused; }
        public void QueueBuyApples(int quantity) { RequireReady(); _slice.QueueBuyApples(quantity); }
        public void QueueStealApples(int quantity) { RequireReady(); _slice.QueueStealApples(quantity); }
        public void AdvanceMinutes(int minutes)
        {
            if (minutes < 0 || minutes > 4320) throw new ArgumentOutOfRangeException(nameof(minutes));
            if (minutes == 0 || IsPaused || Failure != null) return;
            RequireReady();
            try { for (int i = 0; i < minutes; i++) _slice.Tick(); Publish(); }
            catch (Exception failure) { Fault(failure); }
        }
        private void Publish() { Snapshot = _slice.Capture(); SnapshotChanged?.Invoke(Snapshot); }
        private void RequireReady() { if (_slice == null || Failure != null) throw new InvalidOperationException(Failure ?? "World is not initialized."); }
        private void Fault(Exception failure) { Failure = failure.Message; _clock.Paused = true; Debug.LogException(failure, this); Failed?.Invoke(Failure); }
    }
}
