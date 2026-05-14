using System.Collections.Generic;
using UnityEngine;

namespace HellpitRampage.Inventory
{
    public class InventoryGrid
    {
        // WS-011.5 v3: landscape orientation (9 wide × 6 tall) — was 6 wide × 9 tall.
        public const int WIDTH = 9;
        public const int HEIGHT = 6;

        private readonly List<BagInstance> _bags = new();
        private readonly List<ItemInstance> _items = new();
        private int _nextInstanceID = 1;

        public IReadOnlyList<BagInstance> Bags => _bags;
        public IReadOnlyList<ItemInstance> Items => _items;

        // ---------------- Bag operations ----------------

        public bool CanPlaceBag(BagData data, Vector2Int origin)
        {
            if (data == null || data.Shape == null) return false;
            foreach (var offset in data.Shape.Cells)
            {
                Vector2Int cell = origin + offset;
                if (!IsInBounds(cell)) return false;
                if (GetBagAt(cell) != null) return false;
            }
            return true;
        }

        public BagInstance PlaceBag(BagData data, Vector2Int origin)
        {
            if (!CanPlaceBag(data, origin)) return null;
            var bag = new BagInstance(_nextInstanceID++, data, origin);
            _bags.Add(bag);
            return bag;
        }

        public bool RemoveBag(BagInstance bag)
        {
            if (bag == null) return false;
            _items.RemoveAll(i => i.HostBag == bag);
            return _bags.Remove(bag);
        }

        public BagInstance GetBagAt(Vector2Int cell)
        {
            foreach (var bag in _bags)
            {
                if (bag.Data == null || bag.Data.Shape == null) continue;
                foreach (var offset in bag.Data.Shape.Cells)
                {
                    if (bag.Origin + offset == cell) return bag;
                }
            }
            return null;
        }

        public bool MoveBag(BagInstance bag, Vector2Int newOrigin)
        {
            if (bag == null || bag.Data == null || bag.Data.Shape == null) return false;
            if (bag.Origin == newOrigin) return true;

            if (!CanPlaceBagIgnoringSelf(bag, newOrigin)) return false;

            bag.Origin = newOrigin;
            return true;
        }

        public bool CanPlaceBagIgnoringSelf(BagInstance bag, Vector2Int newOrigin)
        {
            if (bag == null || bag.Data == null || bag.Data.Shape == null) return false;
            foreach (var offset in bag.Data.Shape.Cells)
            {
                Vector2Int cell = newOrigin + offset;
                if (!IsInBounds(cell)) return false;
                BagInstance occupant = GetBagAt(cell);
                if (occupant != null && occupant != bag) return false;
            }
            return true;
        }

        // ---------------- Item operations ----------------

        public bool CanPlaceItem(ItemData data, Vector2Int origin, Rotation rotation = Rotation.Deg0, ItemInstance ignore = null)
        {
            if (data == null || data.Shape == null) return false;

            var effectiveCells = ShapeMath.Rotate(data.Shape.Cells, rotation);

            BagInstance hostBag = null;

            foreach (var offset in effectiveCells)
            {
                Vector2Int cell = origin + offset;
                if (!IsInBounds(cell)) return false;

                BagInstance bagAtCell = GetBagAt(cell);
                if (bagAtCell == null) return false;

                if (hostBag == null) hostBag = bagAtCell;
                else if (hostBag != bagAtCell) return false;

                ItemInstance occupant = GetItemAt(cell);
                if (occupant != null && occupant != ignore) return false;
            }

            return hostBag != null;
        }

        public ItemInstance PlaceItem(ItemData data, Vector2Int origin, Rotation rotation = Rotation.Deg0)
        {
            if (!CanPlaceItem(data, origin, rotation)) return null;

            var effectiveCells = ShapeMath.Rotate(data.Shape.Cells, rotation);
            Vector2Int firstCell = origin + effectiveCells[0];
            BagInstance host = GetBagAt(firstCell);

            var item = new ItemInstance(_nextInstanceID++, data, origin, host, rotation);
            _items.Add(item);
            return item;
        }

        public bool RemoveItem(ItemInstance item) => _items.Remove(item);

        public ItemInstance GetItemAt(Vector2Int cell)
        {
            foreach (var item in _items)
            {
                if (item.Data == null || item.Data.Shape == null) continue;
                foreach (var offset in item.EffectiveCells())
                {
                    if (item.Origin + offset == cell) return item;
                }
            }
            return null;
        }

        public bool MoveItem(ItemInstance item, Vector2Int newOrigin, Rotation newRotation)
        {
            if (item == null || item.Data == null || item.Data.Shape == null) return false;
            if (item.Origin == newOrigin && item.Rotation == newRotation) return true;

            if (!CanPlaceItem(item.Data, newOrigin, newRotation, ignore: item)) return false;

            var effective = ShapeMath.Rotate(item.Data.Shape.Cells, newRotation);
            Vector2Int firstCell = newOrigin + effective[0];
            BagInstance newHost = GetBagAt(firstCell);

            item.Origin = newOrigin;
            item.Rotation = newRotation;
            item.HostBag = newHost;
            return true;
        }

        // ---------------- Adjacency ----------------

        private static readonly Vector2Int[] ORTHOGONAL = {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public List<ItemInstance> GetAdjacentItems(ItemInstance item)
        {
            var result = new List<ItemInstance>();
            if (item == null || item.Data == null || item.Data.Shape == null) return result;

            var ownCells = new HashSet<Vector2Int>();
            foreach (var offset in item.EffectiveCells()) ownCells.Add(item.Origin + offset);

            var seen = new HashSet<int>();
            foreach (var cell in ownCells)
            {
                foreach (var dir in ORTHOGONAL)
                {
                    Vector2Int neighbor = cell + dir;
                    if (ownCells.Contains(neighbor)) continue;
                    ItemInstance other = GetItemAt(neighbor);
                    if (other == null || other == item) continue;
                    if (seen.Add(other.InstanceID)) result.Add(other);
                }
            }

            return result;
        }

        // ---------------- Helpers ----------------

        public static bool IsInBounds(Vector2Int cell) =>
            cell.x >= 0 && cell.x < WIDTH && cell.y >= 0 && cell.y < HEIGHT;

        // Instance-method shim — spec §4.9 / §4.14 call this form. Delegates to the static check.
        public bool IsCellInBounds(Vector2Int cell) => IsInBounds(cell);

        // ---------------- Preview-only helpers (WS-012.1 fix-pass) ----------------
        // Direct mutation of the items list bypassing validation and event publishing. Intended
        // ONLY for transient drag-preview overlay computation in StarIndicatorOverlay — the
        // caller must guarantee a paired Remove inside a try/finally so the grid state stays
        // consistent. Never use these for real placement; PlaceItem/RemoveItem own that path.
        public void AddItemDirect(ItemInstance item)
        {
            if (item != null) _items.Add(item);
        }

        public bool RemoveItemDirect(ItemInstance item)
        {
            return item != null && _items.Remove(item);
        }

        public void Clear()
        {
            _bags.Clear();
            _items.Clear();
            _nextInstanceID = 1;
        }
    }
}
