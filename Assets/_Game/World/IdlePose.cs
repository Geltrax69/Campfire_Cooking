using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LivingWorld.Game.World
{
    /// <summary>Plays the imported idle clip for a static workplace preview.</summary>
    public sealed class IdlePose : MonoBehaviour
    {
        public AnimationClip Clip;
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private float _elapsed;
        private void Start()
        {
            if (Clip == null) return;
            var animator = GetComponentInChildren<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            _graph = PlayableGraph.Create("Workplace idle");
            _playable = AnimationClipPlayable.Create(_graph, Clip);
            _playable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(_graph, "Idle", animator).SetSourcePlayable(_playable);
            _graph.Play();
        }
        private void Update()
        {
            if (!_graph.IsValid() || Clip.length <= 0) return;
            _elapsed = (_elapsed + Time.deltaTime) % Clip.length;
            _playable.SetTime(_elapsed);
        }
        private void OnDestroy() { if (_graph.IsValid()) _graph.Destroy(); }
    }
}
