using System.Collections.Generic;
using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// Renders star indicators for a single "focused" starred item — the one currently being
    /// dragged (grid or shop) or whose detail tooltip is open. Stars are dim when their target
    /// cell has no matching neighbor and bright gold when active. Hidden entirely when no
    /// starred item is focused (WS-012.1 third fix-pass: stars are NOT shown ambiently).
    ///
    /// Focus priority each frame:
    ///   1. Grid drag (DragHandler.Active) of an owned starred item → preview at snapped origin.
    ///   2. Shop drag (ShopSlotDragHandler.Active) of an unbought starred offer → preview via
    ///      a synthetic ItemInstance slotted into the grid for the resolver call.
    ///   3. Detail tooltip open on a starred item → committed-state stars at the item's origin.
    /// </summary>
    public class StarIndicatorOverlay : MonoBehaviour
    {
        private const int CELL_SIZE_PX = 56;
        private const int STAR_SIZE_PX = 32;

        [Header("Wiring")]
        [SerializeField] private RectTransform _gridParent;
        [Tooltip("Optional placeholder sprite. If null, the star renders as a tinted square (Unity default fallback).")]
        [SerializeField] private Sprite _starSprite;

        [Header("Style")]
        [SerializeField] private Color _idleColor = new(1f, 1f, 1f, 0.4f);
        [SerializeField] private Color _activeColor = new(0.95f, 0.85f, 0.35f, 1f);

        private readonly List<GameObject> _starInstances = new();

        // Tracks the last-rendered focus so we don't rebuild every frame when nothing has changed.
        // `_focusSource` is one of: a DragHandler, a ShopSlotDragHandler, the DetailTooltipController, or null.
        private object _focusSource;
        private ItemInstance _focusItem;
        private Vector2Int _lastPreviewOrigin;
        private Rotation _lastPreviewRotation;

        // Reused across frames so shop drags don't allocate a new ItemInstance per cursor move.
        private ItemInstance _shopPreviewItem;

        private void OnEnable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<BagPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagMovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemMovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<SynergyChangedEvent>(HandleAnyChange);
            }
            ResetFocus();
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<BagPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<BagRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<BagMovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<ItemPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<ItemRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<ItemMovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<SynergyChangedEvent>(HandleAnyChange);
            }
            ClearStars();
            ResetFocus();
        }

        /// <summary>
        /// Committed-state inventory/synergy events. Only meaningful while a tooltip is open
        /// (so its star's active/idle visual stays current); drag previews own their own
        /// per-frame refresh and ignore the cache.
        /// </summary>
        private void HandleAnyChange<T>(T _) where T : IGameEvent
        {
            if (DragHandler.Active != null || ShopSlotDragHandler.Active != null) return;
            var tooltip = DetailTooltipController.Current;
            if (tooltip != null && IsStarred(tooltip.ShownItem))
            {
                RenderFocus(tooltip, tooltip.ShownItem, activeStars: null);
            }
            else
            {
                ClearFocus();
            }
        }

        private void Update()
        {
            // 1. Grid drag of an owned starred item.
            var gridDrag = DragHandler.Active;
            if (gridDrag != null && IsStarred(gridDrag.CurrentItem))
            {
                bool changed = (object)gridDrag != _focusSource
                    || gridDrag.CurrentSnappedOrigin != _lastPreviewOrigin
                    || gridDrag.CurrentRotation != _lastPreviewRotation;
                if (changed)
                {
                    _lastPreviewOrigin = gridDrag.CurrentSnappedOrigin;
                    _lastPreviewRotation = gridDrag.CurrentRotation;
                    RenderGridDragPreview(gridDrag);
                }
                return;
            }

            // 2. Shop drag of an unbought starred offer.
            var shopDrag = ShopSlotDragHandler.Active;
            if (shopDrag != null && IsStarred(shopDrag.CurrentItemData))
            {
                bool changed = (object)shopDrag != _focusSource
                    || shopDrag.CurrentSnappedOrigin != _lastPreviewOrigin
                    || shopDrag.CurrentRotation != _lastPreviewRotation;
                if (changed)
                {
                    _lastPreviewOrigin = shopDrag.CurrentSnappedOrigin;
                    _lastPreviewRotation = shopDrag.CurrentRotation;
                    RenderShopDragPreview(shopDrag);
                }
                return;
            }

            // 3. Detail tooltip showing a starred item.
            var tooltip = DetailTooltipController.Current;
            if (tooltip != null && IsStarred(tooltip.ShownItem))
            {
                if ((object)tooltip != _focusSource || tooltip.ShownItem != _focusItem)
                {
                    RenderFocus(tooltip, tooltip.ShownItem, activeStars: null);
                }
                return;
            }

            // 4. Nothing focused → hide.
            if (_focusSource != null) ClearFocus();
        }

        // ---------- Render paths ----------

        private void RenderGridDragPreview(DragHandler drag)
        {
            if (InventoryService.Instance == null) { ClearFocus(); return; }
            var grid = InventoryService.Instance.Grid;
            var dragItem = drag.CurrentItem;
            if (!InventoryService.Instance.ContainsItem(dragItem))
            {
                ClearFocus();
                return;
            }

            Vector2Int savedOrigin = dragItem.Origin;
            Rotation savedRotation = dragItem.Rotation;
            try
            {
                dragItem.Origin = drag.CurrentSnappedOrigin;
                dragItem.Rotation = drag.CurrentRotation;
                var preview = SynergyResolver.Resolve(grid);
                RenderFocus(drag, dragItem, preview.ActiveStars);
            }
            finally
            {
                dragItem.Origin = savedOrigin;
                dragItem.Rotation = savedRotation;
            }
        }

        /// <summary>
        /// Shop drags don't yet have an <see cref="ItemInstance"/> — only a <see cref="ItemData"/>
        /// offer. We synthesize a transient ItemInstance, slot it via the preview-only helpers,
        /// resolve, then remove. The synthetic uses <see cref="int.MinValue"/> as a sentinel ID so
        /// it can't collide with the grid's running positive <c>_nextInstanceID</c>.
        /// </summary>
        private void RenderShopDragPreview(ShopSlotDragHandler shopDrag)
        {
            if (InventoryService.Instance == null) { ClearFocus(); return; }
            var grid = InventoryService.Instance.Grid;
            var offerData = shopDrag.CurrentItemData;
            if (offerData == null || offerData.Shape == null) { ClearFocus(); return; }

            if (_shopPreviewItem == null)
            {
                _shopPreviewItem = new ItemInstance(int.MinValue, offerData,
                    shopDrag.CurrentSnappedOrigin, hostBag: null, shopDrag.CurrentRotation);
            }
            else
            {
                _shopPreviewItem.Data = offerData;
                _shopPreviewItem.Origin = shopDrag.CurrentSnappedOrigin;
                _shopPreviewItem.Rotation = shopDrag.CurrentRotation;
                _shopPreviewItem.HostBag = null;
            }

            grid.AddItemDirect(_shopPreviewItem);
            try
            {
                var preview = SynergyResolver.Resolve(grid);
                RenderFocus(shopDrag, _shopPreviewItem, preview.ActiveStars);
            }
            finally
            {
                grid.RemoveItemDirect(_shopPreviewItem);
            }
        }

        /// <summary>
        /// Renders stars for a single focused item. <paramref name="activeStars"/> from a
        /// preview-mode resolve is consulted first; null falls through to the committed-state
        /// <see cref="SynergyService"/> cache.
        /// </summary>
        private void RenderFocus(object source, ItemInstance focusItem,
            HashSet<(int starredID, Vector2Int cell, EdgeDirection dir)> activeStars)
        {
            ClearStars();
            _focusSource = source;
            _focusItem = focusItem;
            if (focusItem == null || _gridParent == null) return;
            if (focusItem.Data?.StarredEdges == null) return;
            if (focusItem.Data.StarredEdges.Count == 0) return;

            var grid = InventoryService.Instance != null ? InventoryService.Instance.Grid : null;
            if (grid == null) return;

            foreach (var star in focusItem.EffectiveStarredEdges())
            {
                Vector2Int absStarCell = focusItem.Origin + star.Cell;
                Vector2Int targetCell = absStarCell + DirectionOffset(star.Direction);

                if (!grid.IsCellInBounds(targetCell)) continue;
                if (focusItem.OccupiesCell(targetCell)) continue;

                bool active = activeStars != null
                    ? activeStars.Contains((focusItem.InstanceID, star.Cell, star.Direction))
                    : (SynergyService.Instance != null &&
                       SynergyService.Instance.IsStarActive(focusItem.InstanceID, star.Cell, star.Direction));

                SpawnStar(active, targetCell);
            }
        }

        // ---------- Focus bookkeeping ----------

        private void ResetFocus()
        {
            _focusSource = null;
            _focusItem = null;
        }

        private void ClearFocus()
        {
            ClearStars();
            ResetFocus();
        }

        private static bool IsStarred(ItemInstance item) =>
            item?.Data?.StarredEdges != null && item.Data.StarredEdges.Count > 0;

        private static bool IsStarred(ItemData data) =>
            data?.StarredEdges != null && data.StarredEdges.Count > 0;

        // ---------- Helpers ----------

        private void ClearStars()
        {
            foreach (var go in _starInstances) if (go != null) Destroy(go);
            _starInstances.Clear();
        }

        private void SpawnStar(bool active, Vector2Int targetCell)
        {
            GameObject go = new GameObject($"Star_{targetCell.x}_{targetCell.y}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_gridParent, false);
            var rt = (RectTransform)go.transform;
            var img = go.GetComponent<Image>();
            if (_starSprite != null) img.sprite = _starSprite;
            img.raycastTarget = false;
            img.color = active ? _activeColor : _idleColor;

            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(STAR_SIZE_PX, STAR_SIZE_PX);
            float cx = (targetCell.x + 0.5f) * CELL_SIZE_PX;
            float cy = (targetCell.y + 0.5f) * CELL_SIZE_PX;
            rt.anchoredPosition = new Vector2(cx, cy);

            // L-009: overlays in mixed-content parents must render last so cell backgrounds don't cover them.
            go.transform.SetAsLastSibling();

            _starInstances.Add(go);
        }

        private static Vector2Int DirectionOffset(EdgeDirection dir) => dir switch
        {
            EdgeDirection.Up => new Vector2Int(0, 1),
            EdgeDirection.Down => new Vector2Int(0, -1),
            EdgeDirection.Left => new Vector2Int(-1, 0),
            EdgeDirection.Right => new Vector2Int(1, 0),
            _ => Vector2Int.zero
        };
    }
}
