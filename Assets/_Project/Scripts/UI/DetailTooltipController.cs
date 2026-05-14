using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.1: scene-scoped manager for the detail tooltip popup shown on left-click of an
    /// owned grid item or bag. Auto-builds its full UI tree (backdrop, panel, icon, name,
    /// type, description, stats, conditional-effects list, book/lock action row) in
    /// <see cref="EnsureBuilt"/> so only a single root GameObject needs to live in the scene.
    /// Dismissed by backdrop click, Escape key, drag-began, or a fresh ShowFor* call.
    /// </summary>
    public class DetailTooltipController : MonoBehaviour
    {
        public static DetailTooltipController Current { get; private set; }

        // Placeholder sprite reuse per WS-012.1 §2 OUT-of-scope notes — same WS-012 lock icon,
        // tinted differently for the locked vs unlocked button state.
        [Header("Sprites (placeholder — both default to lock_icon.png)")]
        [SerializeField] private Sprite _lockedSprite;
        [SerializeField] private Sprite _unlockedSprite;

        [Header("Layout")]
        [SerializeField] private Vector2 _panelSize = new(480f, 360f);

        [Header("Tint colors")]
        [SerializeField] private Color _lockedTint = new(0.95f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color _unlockedTint = new(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private Color _bookTint = new(0.95f, 0.8f, 0.4f, 1f);

        private RectTransform _selfRT;
        private RectTransform _canvasRT;
        private GameObject _backdropGO;
        private GameObject _panelGO;
        private RectTransform _panelRT;

        private Image _iconImage;
        private Text _nameText;
        private Text _typeText;
        private Text _descriptionText;
        private Text _statsText;
        private Text _effectsText;

        private Button _bookButton;
        private Button _lockButton;
        private Image _lockButtonImage;

        private RecipesComingSoonModal _modal;

        private ItemInstance _currentItem;
        private BagInstance _currentBag;
        private bool _built;
        private bool _eventsSubscribed;

        /// <summary>
        /// The grid item this tooltip is currently showing (null when the tooltip is hidden
        /// or showing a bag). Read by <see cref="StarIndicatorOverlay"/> to render star
        /// indicators for the clicked starred item.
        /// </summary>
        public ItemInstance ShownItem => _currentItem;

        private void Awake()
        {
            Current = this;
            EnsureBuilt();
            HidePanel();
        }

        private void OnEnable()
        {
            // L-007: domain reload during Play skips Awake, so child UI refs would be null and
            // _built would be false. Re-running EnsureBuilt from OnEnable would duplicate the
            // backdrop/panel/modal children. Per the project's documented stance for singletons,
            // hot-reload-during-Play is "restart Play" territory — we just publish Current here
            // and let event wiring fail safely (Hide is a no-op if children are null).
            Current = this;
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void Update()
        {
            if (_panelGO == null || !_panelGO.activeInHierarchy) return;
            // L-008: legacy Input is dead in this project; use Keyboard.current.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Hide();
        }

        public void ShowForItem(ItemInstance item)
        {
            if (item == null || item.Data == null) return;
            EnsureBuilt();
            _currentItem = item;
            _currentBag = null;
            PopulateFromItem(item);
            UpdateLockButtonVisual();
            ShowPanelAtCursor();
        }

        public void ShowForBag(BagInstance bag)
        {
            if (bag == null || bag.Data == null) return;
            EnsureBuilt();
            _currentBag = bag;
            _currentItem = null;
            PopulateFromBag(bag);
            UpdateLockButtonVisual();
            ShowPanelAtCursor();
        }

        public void Hide()
        {
            _currentItem = null;
            _currentBag = null;
            HidePanel();
            if (_modal != null) _modal.Close();
        }

        // ---------- Population ----------

        private void PopulateFromItem(ItemInstance item)
        {
            TooltipContent content = InventoryService.Instance != null
                ? TooltipContent.FromItemInstance(item, InventoryService.Instance.Grid)
                : TooltipContent.FromItem(item.Data);

            if (_iconImage != null)
            {
                _iconImage.sprite = item.Data.Icon;
                _iconImage.color = item.Data.Icon == null ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
                _iconImage.gameObject.SetActive(true);
            }
            if (_nameText != null) _nameText.text = content.Title;
            if (_typeText != null)
            {
                _typeText.text = content.RarityLabel;
                _typeText.color = content.RarityColor;
            }
            if (_descriptionText != null) _descriptionText.text = content.Description;
            if (_statsText != null) _statsText.text = content.StatLines;
            if (_effectsText != null) _effectsText.text = content.SynergiesText;
        }

        private void PopulateFromBag(BagInstance bag)
        {
            TooltipContent content = TooltipContent.FromBag(bag.Data);

            if (_iconImage != null)
            {
                _iconImage.sprite = bag.Data.Icon;
                _iconImage.color = bag.Data.Icon == null ? new Color(0.4f, 0.3f, 0.2f, 1f) : Color.white;
                _iconImage.gameObject.SetActive(true);
            }
            if (_nameText != null) _nameText.text = content.Title;
            if (_typeText != null)
            {
                _typeText.text = content.RarityLabel;
                _typeText.color = content.RarityColor;
            }
            if (_descriptionText != null) _descriptionText.text = content.Description;
            if (_statsText != null) _statsText.text = content.StatLines;
            if (_effectsText != null) _effectsText.text = string.Empty;
        }

        private void UpdateLockButtonVisual()
        {
            if (_lockButtonImage == null) return;
            bool locked = (_currentItem != null && _currentItem.IsLocked)
                          || (_currentBag != null && _currentBag.IsLocked);
            _lockButtonImage.sprite = locked && _lockedSprite != null ? _lockedSprite : (_unlockedSprite != null ? _unlockedSprite : _lockedSprite);
            _lockButtonImage.color = locked ? _lockedTint : _unlockedTint;
        }

        // ---------- Positioning ----------

        private void ShowPanelAtCursor()
        {
            if (_panelGO == null || _backdropGO == null || _panelRT == null || _canvasRT == null) return;
            _backdropGO.SetActive(true);
            _panelGO.SetActive(true);

            // L-008: legacy Input.mousePosition returns zero in this project; use Mouse.current.
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRT, mousePos, null, out Vector2 local);

            Vector2 panelSize = _panelRT.sizeDelta;
            Rect canvasRect = _canvasRT.rect;

            // Pivot is centered (0.5, 0.5) so clamp the center inside [edge + half-size, edge - half-size].
            float halfW = panelSize.x * 0.5f;
            float halfH = panelSize.y * 0.5f;
            local.x = Mathf.Clamp(local.x, canvasRect.xMin + halfW, canvasRect.xMax - halfW);
            local.y = Mathf.Clamp(local.y, canvasRect.yMin + halfH, canvasRect.yMax - halfH);

            _panelRT.anchoredPosition = local;
            _panelRT.SetAsLastSibling();
        }

        /// <summary>
        /// Pure-math version of <see cref="ShowPanelAtCursor"/>'s clamp — extracted for testability.
        /// </summary>
        public static Vector2 ClampPanelCenter(Vector2 desiredCenter, Vector2 panelSize, Rect canvasRect)
        {
            float halfW = panelSize.x * 0.5f;
            float halfH = panelSize.y * 0.5f;
            return new Vector2(
                Mathf.Clamp(desiredCenter.x, canvasRect.xMin + halfW, canvasRect.xMax - halfW),
                Mathf.Clamp(desiredCenter.y, canvasRect.yMin + halfH, canvasRect.yMax - halfH)
            );
        }

        private void HidePanel()
        {
            if (_panelGO != null) _panelGO.SetActive(false);
            if (_backdropGO != null) _backdropGO.SetActive(false);
        }

        // ---------- Event subscriptions ----------

        private void SubscribeEvents()
        {
            if (_eventsSubscribed) return;
            if (_bookButton != null) _bookButton.onClick.AddListener(OnBookClicked);
            if (_lockButton != null) _lockButton.onClick.AddListener(OnLockClicked);
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<ItemDragBeganEvent>(HandleItemDragBegan);
                EventBus.Instance.Subscribe<BagDragBeganEvent>(HandleBagDragBegan);
                EventBus.Instance.Subscribe<ItemLockChangedEvent>(HandleItemLockChanged);
                EventBus.Instance.Subscribe<BagLockChangedEvent>(HandleBagLockChanged);
            }
            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed) return;
            if (_bookButton != null) _bookButton.onClick.RemoveListener(OnBookClicked);
            if (_lockButton != null) _lockButton.onClick.RemoveListener(OnLockClicked);
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<ItemDragBeganEvent>(HandleItemDragBegan);
                EventBus.Instance.Unsubscribe<BagDragBeganEvent>(HandleBagDragBegan);
                EventBus.Instance.Unsubscribe<ItemLockChangedEvent>(HandleItemLockChanged);
                EventBus.Instance.Unsubscribe<BagLockChangedEvent>(HandleBagLockChanged);
            }
            _eventsSubscribed = false;
        }

        private void HandleItemDragBegan(ItemDragBeganEvent _) => Hide();
        private void HandleBagDragBegan(BagDragBeganEvent _) => Hide();
        private void HandleItemLockChanged(ItemLockChangedEvent e) { if (_currentItem == e.Item) UpdateLockButtonVisual(); }
        private void HandleBagLockChanged(BagLockChangedEvent e) { if (_currentBag == e.Bag) UpdateLockButtonVisual(); }

        private void OnBookClicked()
        {
            if (_modal != null) _modal.Show();
        }

        private void OnLockClicked()
        {
            if (InventoryService.Instance == null) return;
            if (_currentItem != null) InventoryService.Instance.ToggleItemLock(_currentItem);
            else if (_currentBag != null) InventoryService.Instance.ToggleBagLock(_currentBag);
            UpdateLockButtonVisual();
        }

        // ---------- UI build ----------

        private void EnsureBuilt()
        {
            if (_built) return;
            _selfRT = transform as RectTransform;
            if (_selfRT == null) return;

            // Make this controller's GO stretch across the canvas so backdrop and panel anchor cleanly.
            _selfRT.anchorMin = Vector2.zero;
            _selfRT.anchorMax = Vector2.one;
            _selfRT.offsetMin = Vector2.zero;
            _selfRT.offsetMax = Vector2.zero;

            _canvasRT = _selfRT.parent as RectTransform;

            BuildBackdrop();
            BuildPanel();
            BuildModal();

            _built = true;
        }

        private void BuildBackdrop()
        {
            _backdropGO = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _backdropGO.transform.SetParent(_selfRT, false);
            var rt = (RectTransform)_backdropGO.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = _backdropGO.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // invisible but raycast-blocking
            img.raycastTarget = true;
            _backdropGO.AddComponent<TooltipBackdropClickHandler>();
        }

        private void BuildPanel()
        {
            _panelGO = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelGO.transform.SetParent(_selfRT, false);
            _panelRT = (RectTransform)_panelGO.transform;
            _panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRT.pivot = new Vector2(0.5f, 0.5f);
            _panelRT.sizeDelta = _panelSize;
            _panelRT.anchoredPosition = Vector2.zero;

            var img = _panelGO.GetComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
            // Panel must consume its own clicks so they don't bubble to the backdrop (spec §3 #7).
            img.raycastTarget = true;

            BuildPanelContent(_panelRT);
        }

        private void BuildPanelContent(RectTransform panel)
        {
            // Icon (96×96 top-center)
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(panel, false);
            var iconRT = (RectTransform)iconGO.transform;
            iconRT.anchorMin = new Vector2(0.5f, 1f);
            iconRT.anchorMax = new Vector2(0.5f, 1f);
            iconRT.pivot = new Vector2(0.5f, 1f);
            iconRT.sizeDelta = new Vector2(96f, 96f);
            iconRT.anchoredPosition = new Vector2(0f, -16f);
            _iconImage = iconGO.GetComponent<Image>();
            _iconImage.raycastTarget = false;
            _iconImage.preserveAspect = true;

            // Name (24pt, bold, centered)
            _nameText = BuildText(panel, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-32f, 36f), new Vector2(0f, -120f), 24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);

            // Type/rarity (16pt, muted, centered)
            _typeText = BuildText(panel, "Type", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-32f, 24f), new Vector2(0f, -160f), 16, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.75f, 0.75f, 1f));

            // Description (18pt, italic, left)
            _descriptionText = BuildText(panel, "Description", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-32f, 50f), new Vector2(0f, -192f), 18, FontStyle.Italic, TextAnchor.UpperLeft,
                new Color(0.85f, 0.85f, 0.85f, 1f));

            // Stats (18pt, body, left)
            _statsText = BuildText(panel, "Stats", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-32f, 90f), new Vector2(0f, -244f), 18, FontStyle.Normal, TextAnchor.UpperLeft,
                new Color(1f, 0.95f, 0.85f, 1f));

            // Conditional effects (18pt, left)
            _effectsText = BuildText(panel, "Effects", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-32f, 88f), new Vector2(0f, 60f), 18, FontStyle.Normal, TextAnchor.LowerLeft,
                new Color(0.75f, 0.85f, 1f, 1f));

            BuildActionRow(panel);
        }

        private void BuildActionRow(RectTransform panel)
        {
            // Book button — bottom-left
            _bookButton = BuildIconButton(panel, "BookButton",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 0f), pivot: new Vector2(0f, 0f),
                size: new Vector2(40f, 40f), pos: new Vector2(12f, 12f),
                sprite: null, tint: _bookTint, out _);

            // Lock button — bottom-right
            _lockButton = BuildIconButton(panel, "LockButton",
                anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f), pivot: new Vector2(1f, 0f),
                size: new Vector2(40f, 40f), pos: new Vector2(-12f, 12f),
                sprite: _unlockedSprite != null ? _unlockedSprite : _lockedSprite,
                tint: _unlockedTint, out _lockButtonImage);
        }

        private Button BuildIconButton(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos,
            Sprite sprite, Color tint, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = image;
            return btn;
        }

        private Text BuildText(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 sizeDelta, Vector2 anchoredPos,
            int fontSize, FontStyle style, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = align;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private void BuildModal()
        {
            var modalGO = new GameObject("RecipesComingSoonModal", typeof(RectTransform));
            modalGO.transform.SetParent(_selfRT, false);
            _modal = modalGO.AddComponent<RecipesComingSoonModal>();
        }
    }
}
