using System.Collections.Generic;
using HellpitRampage.Core;
using HellpitRampage.UI;
using UnityEngine;

namespace HellpitRampage.Inventory
{
    public class InventoryService : MonoBehaviour
    {
        public static InventoryService Instance { get; private set; }

        public InventoryGrid Grid { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Grid = new InventoryGrid();
            // WS-015: persistent across the Combat<->Shop scene transitions so inventory
            // and ground-item state survive. Guarded — DontDestroyOnLoad is play-mode-only
            // and throws when instantiated in an EditMode test.
            if (Application.isPlaying) DontDestroyOnLoad(transform.root.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // WS-015: ground-item state lives on this persistent service (not the scene-scoped
        // GroundManager) so it survives the Combat<->Shop scene transitions. GroundManager
        // owns the visuals; after every change it mirrors its full snapshot here via
        // SyncGroundItems, and rebuilds its visuals from this list when the Shop scene loads.
        private readonly List<GroundItemSnapshot> _groundItems = new();
        public IReadOnlyList<GroundItemSnapshot> GroundItems => _groundItems;

        /// <summary>
        /// Replaces the persisted ground-item list wholesale with GroundManager's current
        /// snapshot. Wholesale (rather than per-item add/remove) so an in-place lock toggle
        /// on a ground item is captured without snapshot-identity matching.
        /// </summary>
        public void SyncGroundItems(IReadOnlyList<GroundItemSnapshot> snapshots)
        {
            _groundItems.Clear();
            if (snapshots != null) _groundItems.AddRange(snapshots);
        }

        /// <summary>Clears persisted ground items — called on a fresh run.</summary>
        public void ClearGroundItems() => _groundItems.Clear();

        // WS-013: full reset before a run-restore. Bypasses per-item event publishing because
        // the restore controller rebuilds the grid in one batch and the UI redraws from the
        // resulting state — emitting N removal events would only thrash the renderer. We still
        // publish a single BagRemovedEvent per bag at the end so downstream listeners (e.g.,
        // gold display, synergy resolver) reset cleanly.
        public void ClearAll()
        {
            if (Grid == null) return;

            // Snapshot bags before clearing so we can publish post-clear without iterating a
            // mutating collection.
            var bagsToNotify = new List<BagInstance>(Grid.Bags);
            Grid.Clear();

            if (EventBus.Instance != null)
            {
                foreach (var bag in bagsToNotify)
                    EventBus.Instance.Publish(new BagRemovedEvent { Bag = bag });
            }
        }

        public BagInstance PlaceBag(BagData data, Vector2Int origin)
        {
            BagInstance bag = Grid.PlaceBag(data, origin);
            if (bag != null && EventBus.Instance != null)
                EventBus.Instance.Publish(new BagPlacedEvent { Bag = bag });
            return bag;
        }

        public bool RemoveBag(BagInstance bag)
        {
            var itemsInBag = new List<ItemInstance>();
            foreach (var item in Grid.Items)
                if (item.HostBag == bag) itemsInBag.Add(item);

            if (!Grid.RemoveBag(bag)) return false;

            if (EventBus.Instance != null)
            {
                foreach (var item in itemsInBag)
                    EventBus.Instance.Publish(new ItemRemovedEvent { Item = item });
                EventBus.Instance.Publish(new BagRemovedEvent { Bag = bag });
            }
            return true;
        }

        public ItemInstance PlaceItem(ItemData data, Vector2Int origin)
        {
            return PlaceItem(data, origin, Rotation.Deg0);
        }

        public ItemInstance PlaceItem(ItemData data, Vector2Int origin, Rotation rotation)
        {
            ItemInstance item = Grid.PlaceItem(data, origin, rotation);
            if (item != null && EventBus.Instance != null)
                EventBus.Instance.Publish(new ItemPlacedEvent { Item = item });
            return item;
        }

        public bool RemoveItem(ItemInstance item)
        {
            if (!Grid.RemoveItem(item)) return false;
            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new ItemRemovedEvent { Item = item });
            return true;
        }

        public bool MoveBagAndItems(BagInstance bag, Vector2Int newOrigin)
        {
            if (bag == null) return false;
            Vector2Int oldOrigin = bag.Origin;
            if (oldOrigin == newOrigin) return true;

            Vector2Int delta = newOrigin - oldOrigin;

            var itemsInBag = new List<ItemInstance>();
            var proposed = new List<Vector2Int>();
            foreach (var item in Grid.Items)
            {
                if (item.HostBag == bag)
                {
                    itemsInBag.Add(item);
                    proposed.Add(item.Origin + delta);
                }
            }

            if (!Grid.MoveBag(bag, newOrigin)) return false;

            for (int i = 0; i < itemsInBag.Count; i++)
            {
                if (!Grid.CanPlaceItem(itemsInBag[i].Data, proposed[i], itemsInBag[i].Rotation, ignore: itemsInBag[i]))
                {
                    Grid.MoveBag(bag, oldOrigin);
                    return false;
                }
            }

            for (int i = 0; i < itemsInBag.Count; i++)
            {
                Vector2Int oldItemOrigin = itemsInBag[i].Origin;
                itemsInBag[i].Origin = proposed[i];
                if (EventBus.Instance != null)
                {
                    EventBus.Instance.Publish(new ItemMovedEvent
                    {
                        Item = itemsInBag[i],
                        OldOrigin = oldItemOrigin,
                        NewOrigin = proposed[i],
                        OldRotation = itemsInBag[i].Rotation,
                        NewRotation = itemsInBag[i].Rotation
                    });
                }
            }

            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new BagMovedEvent { Bag = bag, OldOrigin = oldOrigin, NewOrigin = newOrigin });

            return true;
        }

        // WS-012: lock toggles. Publishes the corresponding *LockChangedEvent on success.
        // Null-tolerant so right-click handlers don't need to defensively guard.
        public void ToggleItemLock(ItemInstance item)
        {
            if (item == null) return;
            item.IsLocked = !item.IsLocked;
            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new ItemLockChangedEvent { Item = item });
        }

        public void ToggleBagLock(BagInstance bag)
        {
            if (bag == null) return;
            bag.IsLocked = !bag.IsLocked;
            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new BagLockChangedEvent { Bag = bag });
        }

        // WS-012: helper for DragHandler's "did my item get sold mid-drop" defensive check.
        // The sell modal removes the item during OnDrop, which fires before DragHandler.OnEndDrag.
        public bool ContainsItem(ItemInstance item)
        {
            if (item == null || Grid == null) return false;
            foreach (var i in Grid.Items)
                if (i == item) return true;
            return false;
        }

        public bool MoveItem(ItemInstance item, Vector2Int newOrigin, Rotation newRotation)
        {
            if (item == null) return false;
            Vector2Int oldOrigin = item.Origin;
            Rotation oldRotation = item.Rotation;

            if (!Grid.MoveItem(item, newOrigin, newRotation)) return false;

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Publish(new ItemMovedEvent
                {
                    Item = item,
                    OldOrigin = oldOrigin,
                    NewOrigin = newOrigin,
                    OldRotation = oldRotation,
                    NewRotation = newRotation
                });
            }
            return true;
        }

        // WS-012.5: returns the items currently parented to the given bag (HostBag == bag).
        // Used by the bag-sale flow to capture the spill list BEFORE removing the bag, so the
        // items can be re-spawned on the ground rather than destroyed.
        public List<ItemInstance> GetItemsInBag(BagInstance bag)
        {
            var result = new List<ItemInstance>();
            if (bag == null || Grid == null) return result;
            foreach (var item in Grid.Items)
                if (item.HostBag == bag) result.Add(item);
            return result;
        }

        // WS-012.5: removes an item from the grid WITHOUT publishing ItemRemovedEvent.
        // Used by the ground-deposit flow (DragHandler / ShopSlotDragHandler / SellModal bag spill)
        // which publishes its own GroundItemAddedEvent — letting both fire would cause
        // InventoryGridView to RefreshAll twice and briefly flash a missing item.
        public bool RemoveItemSilent(ItemInstance item)
        {
            if (item == null) return false;
            return Grid.RemoveItem(item);
        }

        // WS-012.5: removes a bag from the grid without touching items inside it. The
        // bag-sale flow uses this AFTER it has captured the item list and re-homed each
        // item to the ground via RemoveItemSilent — by the time this fires, no items
        // reference the bag. Standard RemoveBag cascades item destruction; this variant
        // is the explicit non-cascading path so the spill order (gold → spill → bag) is
        // unambiguous.
        public bool RemoveBagWithoutCascade(BagInstance bag)
        {
            if (bag == null || Grid == null) return false;
            if (!Grid.RemoveBagOnly(bag)) return false;

            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new BagRemovedEvent { Bag = bag });

            return true;
        }
    }
}
