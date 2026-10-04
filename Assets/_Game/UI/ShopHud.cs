using LivingWorld.Game.Bridge;
using UnityEngine;
using UnityEngine.UIElements;
namespace LivingWorld.Game.UI
{
    /// <summary>Touch-first contextual apple shop and minimal world status strip.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShopHud : MonoBehaviour, IShopCommands
    {
        [SerializeField] private WorldRunner _runner;
        private ShopPresenter _presenter;
        private UIDocument _document;
        private PanelSettings _ownedSettings;
        private VisualElement _root, _shop, _header, _actions, _dialogue;
        private Label _npcName, _npcOccupation, _npcLine;
        private DialoguePresenter _dialoguePresenter;
        private bool _npcOpen;
        public bool IsModalOpen => _open || _npcOpen;
        private ScrollView _support;
        private bool _compact;
        private Label _status, _stock, _wallet, _message;
        private Button _buy, _take, _pause, _wait;
        private bool _open;
        private int _screenWidth, _screenHeight;
        private Rect _safeArea;
        private DisplaySnapshot _lastSnapshot;
        private static readonly Color Ivory = new Color(0.98f, 0.94f, 0.84f);
        private static readonly Color Brown = new Color(0.16f, 0.11f, 0.075f, 0.97f);

        public void Bind(WorldRunner runner)
        {
            Unsubscribe(); _runner = runner; _lastSnapshot = null;
            if (isActiveAndEnabled && _runner != null) _runner.SnapshotChanged += OnSnapshot;
            Refresh();
        }
        public void ShowShop() { HideNpc(); _open = true; if (_shop != null) _shop.style.display = DisplayStyle.Flex; Refresh(); }
        public void HideShop() { _open = false; if (_shop != null) _shop.style.display = DisplayStyle.None; }
        public void ShowNpc(string npcId)
        {
            HideShop(); _npcOpen = true;
            if (_dialogue == null) return;
            _dialogue.style.display = DisplayStyle.Flex;
            _dialoguePresenter.Open(npcId);
            _npcName.text = "Conversation"; _npcOccupation.text = "";
            var snapshot = _runner == null ? null : _runner.Snapshot;
            if (snapshot != null)
                foreach (var npc in snapshot.Npcs)
                    if (npc.Id == npcId) { _npcName.text = npc.Name; _npcOccupation.text = npc.Occupation; break; }
            _npcLine.text = _dialoguePresenter.Line;
        }
        public void HideNpc() { _npcOpen = false; if (_dialogue != null) _dialogue.style.display = DisplayStyle.None; }
        private void Say(ConversationTopic topic)
        {
            _dialoguePresenter.Say(topic); _npcLine.text = _dialoguePresenter.Line;
        }
        private void OnEnable()
        {
            _lastSnapshot = null;
            _presenter = new ShopPresenter(this);
            _dialoguePresenter = new DialoguePresenter((id, topic) =>
            {
                if (_runner == null || _runner.Snapshot == null) return "Village unavailable. Try again when it has loaded.";
                if (_runner.Failure != null) return "Village stopped: " + _runner.Failure;
                return _runner.TalkToNpc(id, topic);
            });
            Build();
            if (_runner != null) _runner.SnapshotChanged += OnSnapshot;
            Refresh();
        }
        private void OnDisable() { Unsubscribe(); }
        private void OnDestroy() { if (_ownedSettings != null) Destroy(_ownedSettings); }
        private void Unsubscribe() { if (_runner != null) _runner.SnapshotChanged -= OnSnapshot; }
        private void OnSnapshot(DisplaySnapshot snapshot) { Refresh(); }
        private void Update()
        {
            if (_runner != null && _runner.Failure != null && _presenter.Ready) Refresh();
            if (_runner != null && _presenter.Paused != _runner.IsPaused)
            { _presenter.SyncPaused(_runner.IsPaused); Render(); }
            if (_screenWidth != Screen.width || _screenHeight != Screen.height || _safeArea != Screen.safeArea) ApplySafeArea();
        }
        private void Build()
        {
            _document = GetComponent<UIDocument>();
            if (_document.panelSettings == null)
            {
                _ownedSettings = ScriptableObject.CreateInstance<PanelSettings>();
                // Physical sizing approximates iOS points and preserves 48-point touch targets.
                _ownedSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("LivingWorldTheme");
                _ownedSettings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
                _ownedSettings.referenceDpi = 160;
                _ownedSettings.fallbackDpi = 160;
                _document.panelSettings = _ownedSettings;
            }
            _root = _document.rootVisualElement; _root.Clear(); _root.pickingMode = PickingMode.Ignore;
            _root.style.color = Ivory; _root.style.fontSize = 18;
            var strip = new VisualElement(); strip.style.flexDirection = FlexDirection.Row;
            strip.style.backgroundColor = Brown; strip.style.alignSelf = Align.Center;
            strip.style.alignItems = Align.Center; strip.style.paddingLeft = 16; strip.style.paddingRight = 8;
            _status = new Label("Connecting to the village…"); strip.Add(_status);
            _pause = Button("Pause", () => { _presenter.TogglePause(); Render(); }); strip.Add(_pause); _root.Add(strip);
            _shop = new VisualElement(); _shop.name = "apple-shop";
            _shop.style.width = 380; _shop.style.maxWidth = Length.Percent(100);
            _shop.style.maxHeight = Length.Percent(80); _shop.style.alignSelf = Align.FlexEnd;
            _shop.style.marginTop = 24; _shop.style.backgroundColor = Brown;
            _shop.style.paddingLeft = 20; _shop.style.paddingRight = 20;
            _shop.style.paddingTop = 12; _shop.style.paddingBottom = 20;
            _header = new VisualElement(); _header.style.flexDirection = FlexDirection.Row;
            _header.style.alignItems = Align.Center; _shop.Add(_header);
            var heading = new Label("Apple shop"); heading.style.fontSize = 26; heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.flexGrow = 1; _header.Add(heading); _header.Add(Button("Close", HideShop));
            _stock = new Label(); _shop.Add(_stock);
            _wallet = new Label(); _shop.Add(_wallet);
            _actions = new VisualElement(); _shop.Add(_actions);
            _buy = Button("Buy one apple", () => { _presenter.Buy(); Render(); });
            _buy.style.backgroundColor = new Color(0.22f, 0.36f, 0.18f); _actions.Add(_buy);
            _take = Button("Take six · steal", () => { _presenter.Take(); Render(); }); _actions.Add(_take);
            // Primary actions stay outside the scroll region on short landscape displays.
            _header.style.flexShrink = 0; _stock.style.flexShrink = 0;
            _wallet.style.flexShrink = 0; _actions.style.flexShrink = 0;
            _support = new ScrollView(ScrollViewMode.Vertical);
            _support.style.flexShrink = 1; _support.style.minHeight = 0; _shop.Add(_support);
            _message = new Label(); _message.style.whiteSpace = WhiteSpace.Normal;
            _message.style.marginTop = 12; _message.style.marginBottom = 12; _support.Add(_message);
            _wait = Button("Prototype: wait one hour", () => { _presenter.Wait(); Render(); }); _support.Add(_wait);
            var scope = new Label("Shop interaction prototype. Village preview: villagers stay at their workplaces.");
            scope.style.fontSize = 14; scope.style.whiteSpace = WhiteSpace.Normal; scope.style.marginTop = 12; _support.Add(scope);
            BuildDialogue();
            _root.Add(_shop); _shop.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None; ApplySafeArea();
        }
        private void BuildDialogue()
        {
            _dialogue = new VisualElement(); _dialogue.name = "npc-dialogue";
            _dialogue.style.position = Position.Absolute; _dialogue.style.right = 24;
            _dialogue.style.top = 88; _dialogue.style.width = 420;
            _dialogue.style.maxWidth = Length.Percent(80); _dialogue.style.maxHeight = Length.Percent(72);
            _dialogue.style.backgroundColor = Brown; _dialogue.style.paddingLeft = 20;
            _dialogue.style.paddingRight = 20; _dialogue.style.paddingTop = 12; _dialogue.style.paddingBottom = 12;
            var header = new VisualElement(); header.style.flexDirection = FlexDirection.Row; header.style.alignItems = Align.Center;
            _npcName = new Label("Conversation"); _npcName.style.fontSize = 26; _npcName.style.flexGrow = 1;
            header.Add(_npcName); header.Add(Button("Close", HideNpc)); _dialogue.Add(header);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.style.flexShrink = 1; scroll.style.minHeight = 0; _dialogue.Add(scroll);
            _npcOccupation = new Label(); scroll.Add(_npcOccupation);
            _npcLine = new Label(); _npcLine.style.whiteSpace = WhiteSpace.Normal;
            _npcLine.style.marginTop = 12; _npcLine.style.marginBottom = 12; scroll.Add(_npcLine);
            var topics = new VisualElement(); topics.style.flexDirection = FlexDirection.Row; topics.style.flexWrap = Wrap.Wrap;
            scroll.Add(topics);
            foreach (ConversationTopic topic in System.Enum.GetValues(typeof(ConversationTopic)))
            {
                var selected = topic;
                var choice = Button(topic == ConversationTopic.AboutPlayer ? "About me" : topic.ToString() == "ShopStock" ? "Apple stall" : topic.ToString(), () => Say(selected));
                choice.style.marginRight = 6; topics.Add(choice);
            }
            _root.Add(_dialogue); _dialogue.style.display = _npcOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private static Button Button(string text, System.Action action)
        {
            var button = new Button(action) { text = text };
            button.style.minWidth = 48; button.style.minHeight = 48;
            button.style.marginTop = 4; button.style.marginBottom = 4;
            button.style.paddingLeft = 12; button.style.paddingRight = 12;
            button.style.color = Ivory; button.style.backgroundColor = new Color(0.26f, 0.20f, 0.14f);
            button.style.fontSize = 18; return button;
        }
        private void ApplySafeArea()
        {
            if (_root == null) return;
            _screenWidth = Screen.width; _screenHeight = Screen.height; _safeArea = Screen.safeArea;
            // Physical panel units match reference-DPI units; convert pixel insets to those units.
            var dpi = Screen.dpi > 0 ? Screen.dpi : 160;
            var scale = 160f / dpi;
            _root.style.paddingLeft = _safeArea.xMin * scale + 24;
            _root.style.paddingRight = (_screenWidth - _safeArea.xMax) * scale + 24;
            _root.style.paddingTop = (_screenHeight - _safeArea.yMax) * scale + 16;
            _root.style.paddingBottom = _safeArea.yMin * scale + 24;
            _compact = _screenHeight < 500;
            if (_dialogue != null)
            {
                _dialogue.style.right = (_screenWidth - _safeArea.xMax) * scale + 24;
                _dialogue.style.top = (_screenHeight - _safeArea.yMax) * scale + 88;
                _dialogue.style.width = _compact ? Length.Percent(68) : new Length(420);
            }
            _shop.style.width = _compact ? Length.Percent(68) : new Length(380);
            _shop.style.marginTop = _compact ? 8 : 24;
            _shop.style.paddingTop = _compact ? 4 : 12;
            _shop.style.paddingBottom = _compact ? 8 : 20;
            _stock.style.marginTop = _compact ? 2 : 16;
            _wallet.style.marginBottom = _compact ? 4 : 16;
            _actions.style.flexDirection = _compact ? FlexDirection.Row : FlexDirection.Column;
            _buy.style.flexGrow = _compact ? 1 : 0;
            _take.style.flexGrow = _compact ? 1 : 0;
            _buy.style.marginRight = _compact ? 6 : 0;
            Render();
        }
        private void Refresh()
        {
            if (_presenter == null) return;
            var snapshot = _runner == null ? null : _runner.Snapshot;
            if (_runner != null && _runner.Failure != null) _presenter.Unavailable("Village stopped: " + _runner.Failure);
            else if (snapshot == null) _presenter.Unavailable("Village unavailable. Check the scene's WorldRunner and content setup.");
            else if (!ReferenceEquals(snapshot, _lastSnapshot))
            {
                _presenter.Update(snapshot.ShopApples, snapshot.ApplePriceCopper, snapshot.PlayerCopper, _runner.IsPaused, snapshot.PlayerApples);
                _lastSnapshot = snapshot;
            }
            Render();
        }
        private void Render()
        {
            if (_status == null) return;
            var snapshot = _runner == null ? null : _runner.Snapshot;
            _status.text = _runner != null && _runner.Failure != null ? "Village stopped: " + _runner.Failure : snapshot == null ? "Village unavailable" : $"Day {snapshot.Minute / 1440 + 1} · {snapshot.Minute % 1440 / 60:00}:{snapshot.Minute % 60:00} · Millbrook";
            _pause.text = _presenter.Paused ? "Resume" : "Pause";
            _pause.SetEnabled(_presenter.Ready);
            _stock.text = snapshot == null ? "Stock unavailable" : $"{snapshot.ShopApples} apples in stock · {snapshot.ApplePriceCopper} copper each";
            _wallet.text = snapshot == null ? "Inventory unavailable" : (_compact ? $"Your pouch: {snapshot.PlayerCopper} copper · {snapshot.PlayerApples} apples" : $"Your pouch: {snapshot.PlayerCopper} copper\nYour apples: {snapshot.PlayerApples}");
            _message.text = _presenter.Message;
            _buy.SetEnabled(_presenter.CanBuy); _take.SetEnabled(_presenter.CanTake); _wait.SetEnabled(_presenter.Ready && !_presenter.Paused);
        }
        void IShopCommands.Buy(int quantity) { _runner.QueueBuyApples(quantity); }
        void IShopCommands.Take(int quantity) { _runner.QueueStealApples(quantity); }
        void IShopCommands.Wait(int minutes) { _runner.AdvanceMinutes(minutes); }
        void IShopCommands.Pause(bool paused) { _runner.SetPaused(paused); }
    }
}
