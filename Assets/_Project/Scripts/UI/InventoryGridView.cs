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

        [Header("Style")]
        [SerializeField] private Color _emptyCellColor = new(0.15f, 0.15f, 0.18f, 1f);
        [SerializeField] private Color _bagTintColor = new(0.4f, 0.3f, 0.2f, 0.6f);
        [SerializeField] private Color _itemTintColor = new(0.9f, 0.85f, 0.6f, 1f);
        [SerializeField] private Color _validHighlightColor = new(0.25f, 0.6f, 0.25f, 1f);
        [SerializeField] private Color _invalidHighlightColor = new(0.6f, 0.2f, 0.2f, 1f);

        private readonly List<Image> _cellInstances = new();
        private readonly Dictionary<Vector2Int, Image> _cellByCoord = new();
        private readonly List<Image> _bagInstances = new();
        private readonly List<Image> _itemInstances = new();
        private bool _cellsBuilt;

        public RectTransform GridParent => _gridParent;
        public int CellSizePx => CELL_SIZE_PX;

        private void OnEnable()
        {
            if (!_cellsBuilt) BuildCellGrid();
            RefreshAll();

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<BagPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagMovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemMovedEvent>(HandleAnyChange);
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
            }
        }

        private void HandleAnyChange<T>(T _) where T : IGameEvent => RefreshAll();

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
            bagImg.color = bag.Data.Icon == null ? _bagTintColor : Color.white;
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

            var tt = bagImg.gameObject.AddComponent<TooltipTarget>();
            tt.Bag = bag.Data;

            _bagInstances.Add(bagImg);
        }

        private void RenderItem(ItemInstance item)
        {
            if (item.Data == null || item.Data.Shape == null || _itemPrefab == null || _gridParent == null) return;

            var effective = item.EffectiveCells();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var off in effective)
            {
                Vector2Int c = item.Origin + off;
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            Image itemImg = Instantiate(_itemPrefab, _gridParent);
            itemImg.name = $"Item_{item.Data.ItemName}_{item.InstanceID}";
            itemImg.color = item.Data.Icon == null ? _itemTintColor : Color.white;
            if (item.Data.Icon != null) itemImg.sprite = item.Data.Icon;
            itemImg.raycastTarget = true;

            RectTransform rt = itemImg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            rt.sizeDelta = new Vector2(w * CELL_SIZE_PX, h * CELL_SIZE_PX);
            rt.anchoredPosition = new Vector2(minX * CELL_SIZE_PX, minY * CELL_SIZE_PX);

            var handler = itemImg.gameObject.AddComponent<DragHandler>();
            handler.Kind = DragHandler.DraggableKind.Item;
            handler.Item = item;
            handler.GridParent = _gridParent;
            handler.View = this;

            var tt = itemImg.gameObject.AddComponent<TooltipTarget>();
            tt.Item = item.Data;
            tt.ItemInstance = item;

            _itemInstances.Add(itemImg);
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
