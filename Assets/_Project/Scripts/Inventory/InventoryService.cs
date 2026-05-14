using System.Collections.Generic;
using HellpitRampage.Core;
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
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
    }
}
