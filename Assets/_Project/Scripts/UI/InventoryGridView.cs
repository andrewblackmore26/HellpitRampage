using System.Collections.Generic;
using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    public class InventoryGridView : MonoBehaviour
    {
        private const int CELL_SIZE_PX = 56;

        [Header("Wiring")]
        [SerializeField] private RectTransform _gridParent;
        [SerializeField] private Image _cellPrefab;
        [SerializeField] private Image _bagPrefab;
        [SerializeField] private Image _itemPrefab;
        // WS-012: lock-icon overlay applied to locked items/bags. 16×16, top-left of bounding box.
        [SerializeField] private Sprite _lockIconSprite;

        [Header("Style")]
        [SerializeField] private Color _emptyCellColor = new(0.15f, 0.15f, 0.18f, 1f);
        [SerializeField] private Color _bagTintColor = new(0.4f, 0.3f, 0.2f, 0.6f);
        [SerializeField] private Color _validHighlightColor = new(0.25f, 0.6f, 0.25f, 1f);
        [SerializeField] private Color _invalidHighlightColor = new(0.6f, 0.2f, 0.2f, 1f);

        private readonly List<Image> _cellInstances = new();
        private readonly Dictionary<Vector2Int, Image> _cellByCoord = new();
        private readonly List<Image> _bagInstances = new();
        private readonly List<Image> _itemInstances = new();
        private bool _cellsBuilt;

        // WS-012.5: alpha multipliers driven by DragModeService. Items mode → items full, bags 0.4;
        // Bags mode → bags full, items 0.4. Applied in RenderItem/RenderBag and refreshed on toggle.
        private float _itemAlpha = 1f;
        private float _bagAlpha = 1f;
        private bool _itemsInteractable = true;
        private bool _bagsInteractable = true;

        public RectTransform GridParent => _gridParent;
        public int CellSizePx => CELL_SIZE_PX;

        private void OnEnable()
        {
            if (!_cellsBuilt) BuildCellGrid();
            // WS-012.5: pull current mode at enable time so spawning into Bags mode greys out items.
            if (DragModeService.Current != null)
                ApplyDragMode(DragModeService.Current.CurrentMode);
            RefreshAll();

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<BagPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagMovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemMovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemLockChangedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagLockChangedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<DragModeChangedEvent>(HandleDragModeChanged);
            }
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<BagPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<BagRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<ItemPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<ItemRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<BagMovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<ItemMovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<ItemLockChangedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<BagLockChangedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<DragModeChangedEvent>(HandleDragModeChanged);
            }
        }

        private void HandleAnyChange<T>(T _) where T : IGameEvent => RefreshAll();

        private void HandleDragModeChanged(DragModeChangedEvent e)
        {
            ApplyDragMode(e.NewMode);
            RefreshAll();
        }

        private void ApplyDragMode(DragMode mode)
        {
            bool itemsMode = mode == DragMode.Items;
            _itemAlpha = itemsMode ? 1f : 0.4f;
            _bagAlpha = itemsMode ? 0.4f : 1f;
            _itemsInteractable = itemsMode;
            _bagsInteractable = !itemsMode;
        }

        private void BuildCellGrid()
        {
            foreach (var c in _cellInstances) if (c != null) Destroy(c.gameObject);
            _cellInstances.Clear();
            _cellByCoord.Clear();

            if (_gridParent == null || _cellPrefab == null) { _cellsBuilt = true; return; }

            for (int y = 0; y < InventoryGrid.HEIGHT; y++)
            {
                for (int x = 0; x < InventoryGrid.WIDTH; x++)
                {
                    Image cell = Instantiate(_cellPrefab, _gridParent);
                    cell.name = $"Cell_{x}_{y}";
                    cell.color = _emptyCellColor;
                    RectTransform rt = cell.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                    rt.pivot = new Vector2(0, 0);
                    rt.sizeDelta = new Vector2(CELL_SIZE_PX, CELL_SIZE_PX);
                    rt.anchoredPosition = new Vector2(x * CELL_SIZE_PX, y * CELL_SIZE_PX);
                    _cellInstances.Add(cell);
                    _cellByCoord[new Vector2Int(x, y)] = cell;
                }
            }
            _cellsBuilt = true;
        }

        private void RefreshAll()
        {
            foreach (var b in _bagInstances) if (b != null) Destroy(b.gameObject);
            foreach (var i in _itemInstances) if (i != null) Destroy(i.gameObject);
            _bagInstances.Clear();
            _itemInstances.Clear();

            if (InventoryService.Instance == null) return;
            var grid = InventoryService.Instance.Grid;

            foreach (var bag in grid.Bags) RenderBag(bag);
            foreach (var item in grid.Items) RenderItem(item);
        }

        private void RenderBag(BagInstance bag)
        {
            if (bag.Data == null || bag.Data.Shape == null || _bagPrefab == null || _gridParent == null) return;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var off in bag.Data.Shape.Cells)
            {
                Vector2Int c = bag.Origin + off;
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            Image bagImg = Instantiate(_bagPrefab, _gridParent);
            bagImg.name = $"Bag_{bag.Data.BagName}_{bag.InstanceID}";
            Color baseBagColor = bag.Data.Icon == null ? _bagTintColor : Color.white;
            bagImg.color = new Color(baseBagColor.r, baseBagColor.g, baseBagColor.b, baseBagColor.a * _bagAlpha);
            if (bag.Data.Icon != null) bagImg.sprite = bag.Data.Icon;
            bagImg.raycastTarget = true;

            RectTransform rt = bagImg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            rt.sizeDelta = new Vector2(w * CELL_SIZE_PX, h * CELL_SIZE_PX);
            rt.anchoredPosition = new Vector2(minX * CELL_SIZE_PX, minY * CELL_SIZE_PX);

            var handler = bagImg.gameObject.AddComponent<DragHandler>();
            handler.Kind = DragHandler.DraggableKind.Bag;
            handler.Bag = bag;
            handler.GridParent = _gridParent;
            handler.View = this;
            handler.enabled = _bagsInteractable;

            // WS-012.1: left-click opens the detail tooltip; hover on grid bags does nothing.
            // Lock state is now toggled via the detail tooltip's lock button, not right-click.
            var clickHandler = bagImg.gameObject.AddComponent<GridClickTooltipHandler>();
            clickHandler.Kind = GridClickTooltipHandler.TargetKind.Bag;
            clickHandler.Bag = bag;

            if (bag.IsLocked) AttachLockIcon(bagImg.transform);

            _bagInstances.Add(bagImg);
        }

        private void RenderItem(ItemInstance item)
        {
            if (item.Data == null || item.Data.Shape == null || _itemPrefab == null || _gridParent == null) return;

            var effective = item.EffectiveCells();
            if (effective.Count == 0) return;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var off in effective)
            {
                Vector2Int c = item.Origin + off;
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            // WS-012.2: root holds interaction (DragHandler, click). Per-cell Image children own
            // the visuals so non-rectangular shapes don't tint their bounding-box empty cells.
            // Root keeps a transparent Image so raycast resolves to it (cell children disable raycast).
            Image rootImg = Instantiate(_itemPrefab, _gridParent);
            rootImg.name = $"Item_{item.Data.ItemName}_{item.InstanceID}";
            rootImg.color = new Color(0f, 0f, 0f, 0f);
            rootImg.sprite = null;
            // WS-012.5: in Bags mode, items must NOT intercept drag/drop events. Disable raycast
            // on the root + cell children (set in BuildItemCellChildren via _itemAlpha visible test).
            rootImg.raycastTarget = _itemsInteractable;

            RectTransform rt = rootImg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            rt.sizeDelta = new Vector2(w * CELL_SIZE_PX, h * CELL_SIZE_PX);
            rt.anchoredPosition = new Vector2(minX * CELL_SIZE_PX, minY * CELL_SIZE_PX);

            BuildItemCellChildren(rootImg.transform, effective, item.Data);
            // WS-012.5: tint the per-cell children to match current mode alpha. BuildItemCellChildren
            // sets the placeholder color at full alpha; multiply by _itemAlpha so Bags mode dims items.
            ApplyItemAlphaToCellChildren(rootImg.transform);

            var handler = rootImg.gameObject.AddComponent<DragHandler>();
            handler.Kind = DragHandler.DraggableKind.Item;
            handler.Item = item;
            handler.GridParent = _gridParent;
            handler.View = this;
            handler.enabled = _itemsInteractable;

            // WS-012.1: left-click opens the detail tooltip; hover on grid items does nothing.
            // Lock state is now toggled via the detail tooltip's lock button, not right-click.
            var clickHandler = rootImg.gameObject.AddComponent<GridClickTooltipHandler>();
            clickHandler.Kind = GridClickTooltipHandler.TargetKind.Item;
            clickHandler.Item = item;

            if (item.IsLocked) AttachLockIcon(rootImg.transform);

            _itemInstances.Add(rootImg);
        }

        /// <summary>
        /// WS-012.2: spawns one tinted Image child per occupied cell, anchored to the root's
        /// bottom-left at <c>(cellOffset * CELL_SIZE_PX)</c>. Called both by <see cref="RenderItem"/>
        /// on initial render and by <see cref="DragHandler"/> on rotation during drag.
        /// Destroys any existing cell children (name prefix "Cell_") before rebuilding;
        /// leaves the LockIcon child intact.
        /// </summary>
        public void BuildItemCellChildren(Transform root, IReadOnlyList<Vector2Int> normalizedCells, ItemData data)
        {
            if (root == null || data == null) return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child != null && child.name != null && child.name.StartsWith("Cell_"))
                    Destroy(child.gameObject);
            }

            foreach (var off in normalizedCells)
            {
                var cellGO = new GameObject($"Cell_{off.x}_{off.y}",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cellGO.transform.SetParent(root, false);
                var crt = (RectTransform)cellGO.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0f, 0f);
                crt.pivot = new Vector2(0f, 0f);
                crt.sizeDelta = new Vector2(CELL_SIZE_PX, CELL_SIZE_PX);
                crt.anchoredPosition = new Vector2(off.x * CELL_SIZE_PX, off.y * CELL_SIZE_PX);

                var img = cellGO.GetComponent<Image>();
                img.color = data.PlaceholderColor;
                if (data.Icon != null) img.sprite = data.Icon;
                img.raycastTarget = false;
            }
        }

        // WS-012.5: walk the per-cell child Images and multiply their alpha by _itemAlpha so
        // Bags mode dims items uniformly. Called immediately after BuildItemCellChildren.
        private void ApplyItemAlphaToCellChildren(Transform root)
        {
            if (root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null || child.name == null || !child.name.StartsWith("Cell_")) continue;
                var img = child.GetComponent<Image>();
                if (img == null) continue;
                Color c = img.color;
                c.a *= _itemAlpha;
                img.color = c;
            }
        }

        // WS-012: small lock icon at the top-left of the locked item/bag's bounding box.
        // Parented to the item/bag Image so it auto-renders above the body but below
        // the star overlay siblings added by InventoryGridView (which are added later in the parent).
        private void AttachLockIcon(Transform parent)
        {
            if (_lockIconSprite == null)
            {
                Debug.LogWarning("[InventoryGridView] _lockIconSprite is not assigned; locked items will render without an icon.");
                return;
            }

            GameObject lockGO = new GameObject("LockIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lockGO.transform.SetParent(parent, false);
            var lockImg = lockGO.GetComponent<Image>();
            lockImg.sprite = _lockIconSprite;
            lockImg.raycastTarget = false;
            var lockRT = (RectTransform)lockGO.transform;
            lockRT.anchorMin = new Vector2(0f, 1f);
            lockRT.anchorMax = new Vector2(0f, 1f);
            lockRT.pivot = new Vector2(0f, 1f);
            lockRT.sizeDelta = new Vector2(16f, 16f);
            lockRT.anchoredPosition = new Vector2(4f, -4f);
        }

        public void HighlightCellsValid(IEnumerable<Vector2Int> cells)
        {
            foreach (var c in cells)
            {
                if (_cellByCoord.TryGetValue(c, out var img) && img != null)
                    img.color = _validHighlightColor;
            }
        }

        public void HighlightCellsInvalid(IEnumerable<Vector2Int> cells)
        {
            foreach (var c in cells)
            {
                if (_cellByCoord.TryGetValue(c, out var img) && img != null)
                    img.color = _invalidHighlightColor;
            }
        }

        public void ResetCellHighlights()
        {
            foreach (var kv in _cellByCoord)
                if (kv.Value != null) kv.Value.color = _emptyCellColor;
        }
    }
}
