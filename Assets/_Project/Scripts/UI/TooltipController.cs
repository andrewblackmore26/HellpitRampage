using HellpitRampage.Core;
using HellpitRampage.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.3: unified hover-preview + click-to-pin tooltip. One panel, two modes.
    /// Hover follows the cursor with the action row hidden and the panel non-blocking
    /// (so clicks pass through to items underneath). Pinning freezes the panel at the
    /// click position, activates the backdrop, and shows the book/lock action row for
    /// owned items. The full UI tree is built in code in <see cref="EnsureBuilt"/>; the
    /// scene only needs the root GameObject + the lock-sprite/tint SerializeFields.
    /// </summary>
    public class TooltipController : MonoBehaviour
    {
        public static TooltipController Current { get; private set; }

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
        private Image _panelImage;
        private GameObject _actionRowGO;

        private Image _iconImage;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _typeText;
        private TextMeshProUGUI _descriptionText;
        private TextMeshProUGUI _statsText;
        private TextMeshProUGUI _effectsText;

        private Button _bookButton;
        private Button _lockButton;
        private Image _lockButtonImage;

        private RecipesComingSoonModal _modal;

        private enum SourceKind { None, ShopItem, ShopBag, OwnedItem, OwnedBag }
        private SourceKind _source = SourceKind.None;
        private ItemData _currentItemData;
        private BagData _currentBagData;
        private ItemInstance _currentItem;
        private BagInstance _currentBag;
        private InspectableItem _currentInspector;

        private bool _built;
        private bool _eventsSubscribed;
        private bool _dragInProgress;

        public bool IsShowing { get; private set; }
        public bool IsPinned { get; private set; }

        /// <summary>
        /// The owned grid item currently focused by the tooltip (hover or pinned).
        /// Null when showing a bag, a shop offer, or hidden. Read by
        /// <see cref="StarIndicatorOverlay"/> to render star indicators.
        /// </summary>
        public ItemInstance ShownItem => _source == SourceKind.OwnedItem ? _currentItem : null;

        private void Awake()
        {
            Current = this;
            EnsureBuilt();
            HideAll();
        }

        private void OnEnable()
        {
            // L-007: hot-reload-during-Play skips Awake; reassert singleton and re-subscribe.
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

            // L-008: legacy Input is dead in this project.
            if (IsPinned && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Unpin();
                return;
            }

            // Hover follows the cursor; pinned stays put.
            if (IsShowing && !IsPinned && Mouse.current != null)
            {
                PositionPanelAtScreen(Mouse.current.position.ReadValue());
            }
        }

        // ---------- Public API: hover ----------

        public void ShowHovering(ItemData data, Vector2 screenPos, InspectableItem source)
        {
            if (_dragInProgress || IsPinned || data == null) return;
            EnsureBuilt();
            _source = SourceKind.ShopItem;
            _currentItemData = data;
            _currentBagData = null;
            _currentItem = null;
            _currentBag = null;
            _currentInspector = source;
            PopulateFromItemData(data);
            ShowHoverPanel(screenPos);
        }

        public void ShowHovering(BagData data, Vector2 screenPos, InspectableItem source)
        {
            if (_dragInProgress || IsPinned || data == null) return;
            EnsureBuilt();
            _source = SourceKind.ShopBag;
            _currentBagData = data;
            _currentItemData = null;
            _currentItem = null;
            _currentBag = null;
            _currentInspector = source;
            PopulateFromBagData(data);
            ShowHoverPanel(screenPos);
        }

        public void ShowHovering(ItemInstance item, Vector2 screenPos, InspectableItem source)
        {
            if (_dragInProgress || IsPinned || item == null || item.Data == null) return;
            EnsureBuilt();
            _source = SourceKind.OwnedItem;
            _currentItem = item;
            _currentItemData = null;
            _currentBagData = null;
            _currentBag = null;
            _currentInspector = source;
            PopulateFromItemInstance(item);
            ShowHoverPanel(screenPos);
        }

        public void ShowHovering(BagInstance bag, Vector2 screenPos, InspectableItem source)
        {
            if (_dragInProgress || IsPinned || bag == null || bag.Data == null) return;
            EnsureBuilt();
            _source = SourceKind.OwnedBag;
            _currentBag = bag;
            _currentItem = null;
            _currentItemData = null;
            _currentBagData = null;
            _currentInspector = source;
            PopulateFromBagInstance(bag);
            ShowHoverPanel(screenPos);
        }

        /// <summary>
        /// Stale-source guard: a PointerExit from item A is ignored if a fresh PointerEnter
        /// from item B already swapped the displayed content. Prevents flicker during fast scans.
        /// </summary>
        public void HideIfNotPinned(InspectableItem source)
        {
            if (IsPinned) return;
            if (source != null && _currentInspector != null && source != _currentInspector) return;
            HideAll();
        }

        // ---------- Public API: pin ----------

        public void Pin(ItemData data, Vector2 screenPos)
        {
            if (_dragInProgress || data == null) return;
            EnsureBuilt();
            if (IsPinned && _source == SourceKind.ShopItem && _currentItemData == data) { Unpin(); return; }
            _source = SourceKind.ShopItem;
            _currentItemData = data;
            _currentBagData = null;
            _currentItem = null;
            _currentBag = null;
            PopulateFromItemData(data);
            ShowPinnedPanel(screenPos, ownedSource: false);
        }

        public void Pin(BagData data, Vector2 screenPos)
        {
            if (_dragInProgress || data == null) return;
            EnsureBuilt();
            if (IsPinned && _source == SourceKind.ShopBag && _currentBagData == data) { Unpin(); return; }
            _source = SourceKind.ShopBag;
            _currentBagData = data;
            _currentItemData = null;
            _currentItem = null;
            _currentBag = null;
            PopulateFromBagData(data);
            ShowPinnedPanel(screenPos, ownedSource: false);
        }

        public void Pin(ItemInstance item, Vector2 screenPos)
        {
            if (_dragInProgress || item == null || item.Data == null) return;
            EnsureBuilt();
            if (IsPinned && _source == SourceKind.OwnedItem && _currentItem == item) { Unpin(); return; }
            _source = SourceKind.OwnedItem;
            _currentItem = item;
            _currentBag = null;
            _currentItemData = null;
            _currentBagData = null;
            PopulateFromItemInstance(item);
            UpdateLockButtonVisual();
            ShowPinnedPanel(screenPos, ownedSource: true);
        }

        public void Pin(BagInstance bag, Vector2 screenPos)
        {
            if (_dragInProgress || bag == null || bag.Data == null) return;
            EnsureBuilt();
            if (IsPinned && _source == SourceKind.OwnedBag && _currentBag == bag) { Unpin(); return; }
            _source = SourceKind.OwnedBag;
            _currentBag = bag;
            _currentItem = null;
            _currentItemData = null;
            _currentBagData = null;
            PopulateFromBagInstance(bag);
            UpdateLockButtonVisual();
            ShowPinnedPanel(screenPos, ownedSource: true);
        }

        public void Unpin() => HideAll();

        // ---------- Mode-switch helpers ----------

        private void ShowHoverPanel(Vector2 screenPos)
        {
            if (_panelGO == null) return;
            _panelGO.SetActive(true);
            if (_backdropGO != null) _backdropGO.SetActive(false);
            if (_actionRowGO != null) _actionRowGO.SetActive(false);
            if (_panelImage != null) _panelImage.raycastTarget = false;
            PositionPanelAtScreen(screenPos);
            if (_panelRT != null) _panelRT.SetAsLastSibling();
            IsShowing = true;
            IsPinned = false;
        }

        private void ShowPinnedPanel(Vector2 screenPos, bool ownedSource)
        {
            if (_panelGO == null) return;
            _panelGO.SetActive(true);
            if (_backdropGO != null) _backdropGO.SetActive(true);
            if (_actionRowGO != null) _actionRowGO.SetActive(ownedSource);
            if (_panelImage != null) _panelImage.raycastTarget = true;
            PositionPanelAtScreen(screenPos);
            if (_panelRT != null) _panelRT.SetAsLastSibling();
            IsShowing = true;
            IsPinned = true;
        }

        private void HideAll()
        {
            IsShowing = false;
            IsPinned = false;
            _source = SourceKind.None;
            _currentItem = null;
            _currentBag = null;
            _currentItemData = null;
            _currentBagData = null;
            _currentInspector = null;
            if (_panelGO != null) _panelGO.SetActive(false);
            if (_backdropGO != null) _backdropGO.SetActive(false);
            if (_actionRowGO != null) _actionRowGO.SetActive(false);
            if (_modal != null) _modal.Close();
        }

        // ---------- Population ----------

        private void PopulateFromItemData(ItemData data)
        {
            TooltipContent content = TooltipContent.FromItem(data);
            ApplyContent(content, data.Icon, fallbackIconTint: new Color(0.4f, 0.4f, 0.4f, 1f));
        }

        private void PopulateFromBagData(BagData data)
        {
            TooltipContent content = TooltipContent.FromBag(data);
            ApplyContent(content, data.Icon, fallbackIconTint: new Color(0.4f, 0.3f, 0.2f, 1f), suppressEffects: true);
        }

        private void PopulateFromItemInstance(ItemInstance item)
        {
            TooltipContent content = InventoryService.Instance != null
                ? TooltipContent.FromItemInstance(item, InventoryService.Instance.Grid)
                : TooltipContent.FromItem(item.Data);
            ApplyContent(content, item.Data.Icon, fallbackIconTint: new Color(0.4f, 0.4f, 0.4f, 1f));
        }

        private void PopulateFromBagInstance(BagInstance bag)
        {
            TooltipContent content = TooltipContent.FromBag(bag.Data);
            ApplyContent(content, bag.Data.Icon, fallbackIconTint: new Color(0.4f, 0.3f, 0.2f, 1f), suppressEffects: true);
        }

        private void ApplyContent(TooltipContent content, Sprite icon, Color fallbackIconTint, bool suppressEffects = false)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.color = icon == null ? fallbackIconTint : Color.white;
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
            if (_effectsText != null) _effectsText.text = suppressEffects ? string.Empty : content.SynergiesText;
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

        private void PositionPanelAtScreen(Vector2 screenPos)
        {
            if (_panelRT == null || _canvasRT == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRT, screenPos, null, out Vector2 cursorLocal);
            var (pivot, pos) = ComputeAnchor(cursorLocal, _panelRT.sizeDelta, _canvasRT.rect);
            _panelRT.pivot = pivot;
            _panelRT.anchoredPosition = pos;
        }

        /// <summary>
        /// WS-012.4 anchor: panel's bottom-left corner sits at the cursor (+12 px offset right and
        /// up). If the panel would overflow the right edge, the pivot flips to (1, _) so it extends
        /// leftward. If it would overflow the top, the y pivot flips to 1 so it extends downward.
        /// Pure-math helper — exposed for <see cref="TooltipAnchorTests"/> without instantiating a
        /// Canvas. Pivot must be set BEFORE anchoredPosition (anchoredPosition is interpreted
        /// relative to the current pivot).
        /// </summary>
        public static (Vector2 pivot, Vector2 pos) ComputeAnchor(Vector2 cursor, Vector2 panelSize, Rect canvasRect)
        {
            const float offset = 12f;
            Vector2 pivot = Vector2.zero;
            Vector2 pos = cursor + new Vector2(offset, offset);
            if (pos.x + panelSize.x > canvasRect.xMax) { pivot.x = 1f; pos.x = cursor.x - offset; }
            if (pos.y + panelSize.y > canvasRect.yMax) { pivot.y = 1f; pos.y = cursor.y - offset; }
            return (pivot, pos);
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
                EventBus.Instance.Subscribe<ItemDragEndedEvent>(HandleItemDragEnded);
                EventBus.Instance.Subscribe<BagDragBeganEvent>(HandleBagDragBegan);
                EventBus.Instance.Subscribe<BagDragEndedEvent>(HandleBagDragEnded);
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
                EventBus.Instance.Unsubscribe<ItemDragEndedEvent>(HandleItemDragEnded);
                EventBus.Instance.Unsubscribe<BagDragBeganEvent>(HandleBagDragBegan);
                EventBus.Instance.Unsubscribe<BagDragEndedEvent>(HandleBagDragEnded);
                EventBus.Instance.Unsubscribe<ItemLockChangedEvent>(HandleItemLockChanged);
                EventBus.Instance.Unsubscribe<BagLockChangedEvent>(HandleBagLockChanged);
            }
            _eventsSubscribed = false;
        }

        private void HandleItemDragBegan(ItemDragBeganEvent _) { _dragInProgress = true; HideAll(); }
        private void HandleItemDragEnded(ItemDragEndedEvent _) { _dragInProgress = false; }
        private void HandleBagDragBegan(BagDragBeganEvent _)   { _dragInProgress = true; HideAll(); }
        private void HandleBagDragEnded(BagDragEndedEvent _)   { _dragInProgress = false; }

        private void HandleItemLockChanged(ItemLockChangedEvent e)
        {
            if (_currentItem == e.Item) UpdateLockButtonVisual();
        }

        private void HandleBagLockChanged(BagLockChangedEvent e)
        {
            if (_currentBag == e.Bag) UpdateLockButtonVisual();
        }

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
            img.color = new Color(0f, 0f, 0f, 0f);
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

            _panelImage = _panelGO.GetComponent<Image>();
            _panelImage.color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
            _panelImage.raycastTarget = true;

            BuildPanelContent(_panelRT);
        }

        private void BuildPanelContent(RectTransform panel)
        {
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

            // L-019: stack the five text fields in a VerticalLayoutGroup so each row sizes itself
            // to its content. Predecessor used hardcoded anchored Y positions and TMP Overflow mode,
            // which caused rects to overlap (Stats / Effects shared 56 px) and content to bleed past
            // its slot into neighbouring text. The VLG owns the geometry now; each text's
            // ContentSizeFitter (set in BuildText) drives its own row height.
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(panel, false);
            var contentRT = (RectTransform)contentGO.transform;
            contentRT.anchorMin = new Vector2(0f, 0f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            // Stretch under the icon (top at -120) down to just above the ActionRow (56 px tall) with
            // an 8 px buffer. offsetMin = (left, bottom_inset), offsetMax = (right_inset, -top_inset).
            contentRT.offsetMin = new Vector2(0f, 64f);
            contentRT.offsetMax = new Vector2(0f, -120f);

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 4, 4);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childScaleWidth = false;
            vlg.childScaleHeight = false;

            _nameText        = BuildText(contentRT, "Name",        24, FontStyles.Bold,   TextAlignmentOptions.Center,  Color.white);
            _typeText        = BuildText(contentRT, "Type",        16, FontStyles.Normal, TextAlignmentOptions.Center,  new Color(0.75f, 0.75f, 0.75f, 1f));
            _descriptionText = BuildText(contentRT, "Description", 18, FontStyles.Italic, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.85f, 0.85f, 1f));
            _statsText       = BuildText(contentRT, "Stats",       18, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(1f,    0.95f, 0.85f, 1f));
            _effectsText     = BuildText(contentRT, "Effects",     18, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.75f, 0.85f, 1f,    1f));

            BuildActionRow(panel);
        }

        private void BuildActionRow(RectTransform panel)
        {
            // Wraps the buttons so we can hide/show them with one SetActive — hidden during
            // hover and for shop offers, visible only when pinned on an owned item or bag.
            _actionRowGO = new GameObject("ActionRow", typeof(RectTransform));
            _actionRowGO.transform.SetParent(panel, false);
            var rowRT = (RectTransform)_actionRowGO.transform;
            rowRT.anchorMin = new Vector2(0f, 0f);
            rowRT.anchorMax = new Vector2(1f, 0f);
            rowRT.pivot = new Vector2(0.5f, 0f);
            rowRT.sizeDelta = new Vector2(0f, 56f);
            rowRT.anchoredPosition = Vector2.zero;
            _actionRowGO.SetActive(false);

            _bookButton = BuildIconButton(rowRT, "BookButton",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 0f), pivot: new Vector2(0f, 0f),
                size: new Vector2(40f, 40f), pos: new Vector2(12f, 8f),
                sprite: null, tint: _bookTint, out _);

            _lockButton = BuildIconButton(rowRT, "LockButton",
                anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f), pivot: new Vector2(1f, 0f),
                size: new Vector2(40f, 40f), pos: new Vector2(-12f, 8f),
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

        private TextMeshProUGUI BuildText(RectTransform parent, string name,
            int fontSize, FontStyles style, TextAlignmentOptions align, Color color)
        {
            // L-019: width is driven by the parent VerticalLayoutGroup; height is driven by the
            // ContentSizeFitter so the rect tracks the rendered preferred height of the wrapped text.
            // Empty content collapses to height 0 (LayoutGroup respects that), so empty Description /
            // Effects don't leave whitespace.
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = align;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            // Truncate is a defensive safety net — with ContentSizeFitter the rect grows to fit, so
            // this only fires if a future item exceeds the panel height entirely. Was Overflow, which
            // caused text to bleed into adjacent rects when content didn't fit (the original bug).
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            text.text = string.Empty;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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
