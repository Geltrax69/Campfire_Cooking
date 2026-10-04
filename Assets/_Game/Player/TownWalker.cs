using LivingWorld.Game.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.UIElements;
namespace LivingWorld.Game.Player
{
    /// <summary>Collision-aware third-person town movement with keyboard and touch controls.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class TownWalker : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _speed = 4f;
        [SerializeField] private float _cameraDistance = 10f;
        private CharacterController _controller;
        private Transform _cameraTarget;
        public AnimationClip IdleClip;
        public AnimationClip WalkClip;
        private Animator _avatarAnimator;
        private PlayableGraph _animationGraph;
        private AnimationMixerPlayable _animationMixer;
        private AnimationClipPlayable _idlePlayable, _walkPlayable;
        private bool _animationReady;
        private CinemachineFollow _follow;
        private ShopHud _hud;
        private Vector2 _touchInput;
        private float _verticalSpeed, _yaw, _stalledSeconds;
        private Vector3? _walkTarget;
        private UIDocument _controlsDocument;
        private PanelSettings _settings;
        private VisualElement _controls;
        public void Bind(Camera camera) { _camera = camera; }
        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _hud = FindFirstObjectByType<ShopHud>();
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _yaw = _camera.transform.eulerAngles.y;
            BuildAnimation();
            BuildCamera();
            BuildControls();
        }
        private void Update()
        {
            bool blocked = _hud != null && _hud.IsModalOpen;
            if (_controls != null) _controls.style.display = blocked ? DisplayStyle.None : DisplayStyle.Flex;
            if (blocked) { _touchInput = Vector2.zero; _walkTarget = null; }
            var keyboard = Keyboard.current;
            Vector2 input = _touchInput;
            if (!blocked && keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1;
            }
            if (!blocked && input.sqrMagnitude > 0) _walkTarget = null;
            if (!blocked) ReadGroundTap();
            if (!blocked && _walkTarget.HasValue)
            {
                var remaining = _walkTarget.Value - transform.position; remaining.y = 0;
                if (remaining.magnitude < 0.35f) _walkTarget = null;
                else
                {
                    var forward = _camera == null ? Vector3.forward : _camera.transform.forward;
                    forward.y = 0; forward.Normalize(); var right = Vector3.Cross(Vector3.up, forward);
                    input = new Vector2(Vector3.Dot(remaining.normalized, right), Vector3.Dot(remaining.normalized, forward));
                }
            }
            var previous = transform.position;
            MoveInput(blocked ? Vector2.zero : input, Time.deltaTime);
            if (_walkTarget.HasValue && Vector3.Distance(previous, transform.position) < 0.001f) _stalledSeconds += Time.deltaTime;
            else _stalledSeconds = 0;
            if (_stalledSeconds > 0.75f) _walkTarget = null;
            var mouse = Mouse.current;
            if (!blocked && mouse != null && mouse.rightButton.isPressed) _yaw += mouse.delta.ReadValue().x * 0.15f;
            if (!blocked && mouse != null) _cameraDistance = Mathf.Clamp(_cameraDistance - mouse.scroll.ReadValue().y * 0.01f, 4f, 12f);
            var safe = Screen.safeArea; float scale = 160f / (Screen.dpi > 0 ? Screen.dpi : 160);
            if (_controls != null)
            { _controls.style.left = safe.xMin * scale + 24; _controls.style.bottom = safe.yMin * scale + 24; }
        }
        private void ReadGroundTap()
        {
            if (_camera == null) return;
            Vector2 pointer;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) pointer = touch.primaryTouch.position.ReadValue();
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) pointer = Mouse.current.position.ReadValue();
            else return;
            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var panel = document.rootVisualElement.panel;
                if (panel == null) continue;
                var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(pointer.x, Screen.height - pointer.y));
                var picked = panel.Pick(point);
                // Panel roots and transparent layout containers cover the screen; only actual controls block a world tap.
                for (var element = picked; element != null && element != document.rootVisualElement; element = element.parent)
                {
                    if (element is Button || element is ScrollView || element.name == "apple-shop" || element.name == "npc-dialogue") return;
                }
            }
            if (Physics.Raycast(_camera.ScreenPointToRay(pointer), out var hit, 250f) && hit.collider.name == "Village ground")
            { _walkTarget = hit.point; _stalledSeconds = 0; }
        }
        /// <summary>Moves through CharacterController; callers cannot bypass modal interaction blocking.</summary>
        public void MoveInput(Vector2 input, float elapsedSeconds)
        {
            if (_controller == null || elapsedSeconds <= 0) return;
            if (_hud != null && _hud.IsModalOpen) input = Vector2.zero;
            Vector3 forward = _camera == null ? Vector3.forward : _camera.transform.forward;
            Vector3 direction = MovementDirection(input, forward);
            if (_controller.isGrounded && _verticalSpeed < 0) _verticalSpeed = -2f;
            _verticalSpeed += -20f * elapsedSeconds;
            _controller.Move((direction * _speed + Vector3.up * _verticalSpeed) * elapsedSeconds);
            if (!_animationReady) BuildAnimation();
            if (_animationReady)
            {
                bool walking = direction.sqrMagnitude > 0.001f;
                _animationMixer.SetInputWeight(0, walking ? 0 : 1);
                _animationMixer.SetInputWeight(1, walking ? 1 : 0);
                LoopClip(_idlePlayable, IdleClip); LoopClip(_walkPlayable, WalkClip);
            }
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        }
        /// <summary>Camera-relative planar movement, clamped so diagonal input never increases speed.</summary>
        public static Vector3 MovementDirection(Vector2 input, Vector3 cameraForward)
        {
            cameraForward.y = 0;
            if (cameraForward.sqrMagnitude < 0.001f) cameraForward = Vector3.forward;
            cameraForward.Normalize(); input = Vector2.ClampMagnitude(input, 1);
            return cameraForward * input.y + Vector3.Cross(Vector3.up, cameraForward) * input.x;
        }
        private void BuildAnimation()
        {
            if (_animationReady || IdleClip == null || WalkClip == null) return;
            _avatarAnimator = GetComponentInChildren<Animator>();
            if (_avatarAnimator == null) return;
            _avatarAnimator.applyRootMotion = false;
            _animationGraph = PlayableGraph.Create("Town player animation");
            _animationGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _idlePlayable = AnimationClipPlayable.Create(_animationGraph, IdleClip);
            _walkPlayable = AnimationClipPlayable.Create(_animationGraph, WalkClip);
            _idlePlayable.SetDuration(double.PositiveInfinity);
            _walkPlayable.SetDuration(double.PositiveInfinity);
            _animationMixer = AnimationMixerPlayable.Create(_animationGraph, 2);
            _animationGraph.Connect(_idlePlayable, 0, _animationMixer, 0);
            _animationGraph.Connect(_walkPlayable, 0, _animationMixer, 1);
            _animationMixer.SetInputWeight(0, 1); _animationMixer.SetInputWeight(1, 0);
            var output = AnimationPlayableOutput.Create(_animationGraph, "Player pose", _avatarAnimator);
            output.SetSourcePlayable(_animationMixer); _animationGraph.Play(); _animationReady = true;
        }
        private static void LoopClip(AnimationClipPlayable playable, AnimationClip clip)
        {
            // Imported clips may not have Loop Time set; loop here without changing vendor metadata.
            if (clip != null && clip.length > 0 && playable.GetTime() >= clip.length)
                playable.SetTime(playable.GetTime() % clip.length);
        }
        private void BuildCamera()
        {
            if (_camera == null) return;
            if (_camera.GetComponent<CinemachineBrain>() == null) _camera.gameObject.AddComponent<CinemachineBrain>();
            var target = new GameObject("Player camera target"); target.transform.SetParent(transform, false);
            target.transform.localPosition = Vector3.up * 1.5f; _cameraTarget = target.transform;
            var rig = new GameObject("Town Cinemachine camera"); rig.transform.SetParent(transform, false);
            var virtualCamera = rig.AddComponent<CinemachineCamera>();
            virtualCamera.Lens.FieldOfView = 50f;
            virtualCamera.Follow = _cameraTarget; virtualCamera.LookAt = _cameraTarget;
            _follow = rig.AddComponent<CinemachineFollow>();
            _follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            _follow.TrackerSettings.PositionDamping = new Vector3(0.15f, 0.15f, 0.15f);
            rig.AddComponent<CinemachineHardLookAt>();
            var deoccluder = rig.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = ~0;
            deoccluder.MinimumDistanceFromTarget = 1f;
            deoccluder.AvoidObstacles.Enabled = true;
            deoccluder.AvoidObstacles.CameraRadius = 0.3f;
            deoccluder.AvoidObstacles.MaximumEffort = 4;
            _follow.FollowOffset = Quaternion.Euler(38, _yaw, 0) * new Vector3(0, 0, -_cameraDistance);
        }
        private void LateUpdate()
        {
            if (_follow != null)
                _follow.FollowOffset = Quaternion.Euler(38, _yaw, 0) * new Vector3(0, 0, -_cameraDistance);
        }
        private void BuildControls()
        {
            var child = new GameObject("Walking controls"); child.transform.SetParent(transform, false);
            _controlsDocument = child.AddComponent<UIDocument>();
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("LivingWorldTheme");
            _settings.scaleMode = PanelScaleMode.ConstantPhysicalSize; _settings.referenceDpi = 160; _settings.fallbackDpi = 160;
            _controlsDocument.panelSettings = _settings; _controlsDocument.sortingOrder = -1;
            var root = _controlsDocument.rootVisualElement; root.pickingMode = PickingMode.Ignore;
            _controls = new VisualElement(); _controls.pickingMode = PickingMode.Ignore; _controls.style.position = Position.Absolute; root.Add(_controls);
            var top = new VisualElement(); top.pickingMode = PickingMode.Ignore; top.style.alignItems = Align.Center; _controls.Add(top);
            top.Add(Direction("Forward", Vector2.up));
            var row = new VisualElement(); row.pickingMode = PickingMode.Ignore; row.style.flexDirection = FlexDirection.Row; _controls.Add(row);
            row.Add(Direction("Left", Vector2.left)); row.Add(Direction("Back", Vector2.down)); row.Add(Direction("Right", Vector2.right));
            var hint = new Label("Tap ground to walk · WASD / arrows\nRight-drag orbit · scroll zoom"); hint.style.fontSize = 12;
            hint.style.color = new Color(0.98f, 0.94f, 0.84f); hint.style.whiteSpace = WhiteSpace.Normal;
            hint.pickingMode = PickingMode.Ignore; hint.style.width = 180; _controls.Add(hint);
        }
        private Button Direction(string name, Vector2 direction)
        {
            var button = new Button { text = name };
            button.style.minWidth = 56; button.style.minHeight = 48; button.style.marginRight = 4; button.style.marginTop = 4;
            button.style.backgroundColor = new Color(0.16f, 0.11f, 0.075f, 0.97f);
            button.style.color = new Color(0.98f, 0.94f, 0.84f);
            button.RegisterCallback<PointerDownEvent>(evt => { _touchInput = direction; button.CapturePointer(evt.pointerId); evt.StopPropagation(); });
            button.RegisterCallback<PointerUpEvent>(evt => { _touchInput = Vector2.zero; button.ReleasePointer(evt.pointerId); evt.StopPropagation(); });
            button.RegisterCallback<PointerCaptureOutEvent>(evt => _touchInput = Vector2.zero);
            return button;
        }
        private void OnDestroy()
        {
            if (_animationGraph.IsValid()) _animationGraph.Destroy();
            if (_settings != null) Destroy(_settings);
        }
    }
}
