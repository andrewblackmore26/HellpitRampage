using System.Collections.Generic;
using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// v3: stars hover IN the target cell (the cell that, if occupied by a matching-tag neighbor,
    /// would activate one of the starred item's conditional effects). Dim when no valid neighbor
    /// is present; bright gold when active. Larger size (32×32) than v2's edge-midpoint stars.
    ///
    /// WS-012.1 fix-pass: while a drag is active, stars rebuild every frame against a PREVIEW
    /// state that overlays the dragged item at its current snapped origin + rotation. This
    /// makes a Whetstone's stars follow it during a drag, and makes any stationary starred
    /// item's stars light up live as a matching weapon ghost passes through their target cell.
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

        // Drag-preview bookkeeping — invalidates the cached "I already rendered this" snapshot
        // when the drag's snapped origin or rotation changes. Tracks both grid drags (DragHandler)
        // and shop drags (ShopSlotDragHandler) so unbought item drags get the same preview.
        private object _trackedDrag;          // either DragHandler or ShopSlotDragHandler
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
            Rebuild(activeStars: null);
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
        }

        private void HandleAnyChange<T>(T _) where T : IGameEvent
        {
            // While ANY drag is active, the per-frame Update path owns the star state;
            // ignore committed-state events so we don't fight with the preview pass.
            if (DragHandler.Active != null || ShopSlotDragHandler.Active != null) return;
            Rebuild(activeStars: null);
        }

        private void Update()
        {
            // Grid drag (existing owned item being moved).
            var gridDrag = DragHandler.Active;
            if (gridDrag != null && gridDrag.CurrentItem != null)
            {
                bool changed = (object)gridDrag != _trackedDrag
                    || gridDrag.CurrentSnappedOrigin != _lastPreviewOrigin
                    || gridDrag.CurrentRotation != _lastPreviewRotation;
                if (changed)
                {
                    _trackedDrag = gridDrag;
                    _lastPreviewOrigin = gridDrag.CurrentSnappedOrigin;
                    _lastPreviewRotation = gridDrag.CurrentRotation;
                    RebuildWithDragPreview(gridDrag.CurrentItem, gridDrag.CurrentSnappedOrigin, gridDrag.CurrentRotation);
                }
                return;
            }

            // Shop drag (unbought offer being dragged from a shop slot).
            var shopDrag = ShopSlotDragHandler.Active;
            if (shopDrag != null && shopDrag.CurrentItemData != null)
            {
                bool changed = (object)shopDrag != _trackedDrag
                    || shopDrag.CurrentSnappedOrigin != _lastPreviewOrigin
                    || shopDrag.CurrentRotation != _lastPreviewRotation;
                if (changed)
                {
                    _trackedDrag = shopDrag;
                    _lastPreviewOrigin = shopDrag.CurrentSnappedOrigin;
                    _lastPreviewRotation = shopDrag.CurrentRotation;
                    RebuildWithShopDragPreview(shopDrag.CurrentItemData, shopDrag.CurrentSnappedOrigin, shopDrag.CurrentRotation);
                }
                return;
            }

            if (_trackedDrag != null)
            {
                _trackedDrag = null;
                // Drag ended — revert to committed state. The ItemPlacedEvent (if a purchase
                // committed) or ItemMovedEvent (if a grid move committed) will also fire Rebuild,
                // but doing it here too keeps rendering correct on cancelled drags.
                Rebuild(activeStars: null);
            }
        }

        /// <summary>
        /// Renders stars from committed grid state. <paramref name="activeStars"/> may be supplied
        /// by the preview path so we don't repeat resolver work; null falls back to the
        /// SynergyService cache.
        /// </summary>
        private void Rebuild(HashSet<(int starredID, Vector2Int cell, EdgeDirection dir)> activeStars)
        {
            ClearStars();
            if (InventoryService.Instance == null || _gridParent == null) return;
            var grid = InventoryService.Instance.Grid;

            foreach (var item in grid.Items)
            {
                if (item?.Data?.StarredEdges == null) continue;
                if (item.Data.StarredEdges.Count == 0) continue;

                foreach (var star in item.EffectiveStarredEdges())
                {
                    Vector2Int absStarCell = item.Origin + star.Cell;
                    Vector2Int targetCell = absStarCell + DirectionOffset(star.Direction);

                    if (!grid.IsCellInBounds(targetCell)) continue;
                    if (item.OccupiesCell(targetCell)) continue;

                    bool active = activeStars != null
                        ? activeStars.Contains((item.InstanceID, star.Cell, star.Direction))
                        : (SynergyService.Instance != null &&
                           SynergyService.Instance.IsStarActive(item.InstanceID, star.Cell, star.Direction));

                    SpawnStar(active, targetCell);
                }
            }
        }

        /// <summary>
        /// Temporarily relocates the dragged item to its current snapped origin/rotation, asks the
        /// resolver to recompute active stars against that hypothetical placement, then restores
        /// the item's state. Pure read-only over the grid — no events fire from <see cref="SynergyResolver.Resolve"/>.
        /// </summary>
        private void RebuildWithDragPreview(ItemInstance dragItem, Vector2Int previewOrigin, Rotation previewRotation)
        {
            if (InventoryService.Instance == null) { ClearStars(); return; }
            var grid = InventoryService.Instance.Grid;

            // Skip preview when the item isn't in the grid (e.g., during a one-frame inconsistency
            // after Sell-modal removal) — fall back to committed state. Reuses the WS-012 helper
            // so we don't pay an extra LINQ allocation just to check containment.
            if (!InventoryService.Instance.ContainsItem(dragItem))
            {
                Rebuild(activeStars: null);
                return;
            }

            Vector2Int savedOrigin = dragItem.Origin;
            Rotation savedRotation = dragItem.Rotation;
            try
            {
                dragItem.Origin = previewOrigin;
                dragItem.Rotation = previewRotation;
                var preview = SynergyResolver.Resolve(grid);
                Rebuild(preview.ActiveStars);
            }
            finally
            {
                dragItem.Origin = savedOrigin;
                dragItem.Rotation = savedRotation;
            }
        }

        /// <summary>
        /// Shop drags don't yet have an <see cref="ItemInstance"/> — only a <see cref="ItemData"/>
        /// offer. To make the resolver see the unbought item at the preview cell, we synthesize a
        /// transient ItemInstance, slot it into <see cref="InventoryGrid"/> via the preview-only
        /// helpers, resolve, then remove. Sentinel ID = int.MinValue avoids collision with the
        /// grid's running <c>_nextInstanceID</c> (which only grows positive from 1).
        /// </summary>
        private void RebuildWithShopDragPreview(ItemData offerData, Vector2Int previewOrigin, Rotation previewRotation)
        {
            if (InventoryService.Instance == null) { ClearStars(); return; }
            var grid = InventoryService.Instance.Grid;
            if (offerData == null || offerData.Shape == null) { ClearStars(); return; }

            // Reuse the synthetic instance across frames so we don't allocate per cursor move.
            if (_shopPreviewItem == null)
            {
                _shopPreviewItem = new ItemInstance(int.MinValue, offerData, previewOrigin, hostBag: null, previewRotation);
            }
            else
            {
                _shopPreviewItem.Data = offerData;
                _shopPreviewItem.Origin = previewOrigin;
                _shopPreviewItem.Rotation = previewRotation;
                _shopPreviewItem.HostBag = null;
            }

            grid.AddItemDirect(_shopPreviewItem);
            try
            {
                var preview = SynergyResolver.Resolve(grid);
                Rebuild(preview.ActiveStars);
            }
            finally
            {
                grid.RemoveItemDirect(_shopPreviewItem);
            }
        }

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
            // Center of the target cell in grid-local pixels.
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
