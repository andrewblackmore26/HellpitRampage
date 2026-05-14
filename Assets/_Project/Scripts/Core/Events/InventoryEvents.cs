using HellpitRampage.Inventory;
using UnityEngine;

namespace HellpitRampage.Core
{
    public struct BagPlacedEvent : IGameEvent { public BagInstance Bag; }
    public struct BagRemovedEvent : IGameEvent { public BagInstance Bag; }
    public struct ItemPlacedEvent : IGameEvent { public ItemInstance Item; }
    public struct ItemRemovedEvent : IGameEvent { public ItemInstance Item; }

    public struct BagMovedEvent : IGameEvent
    {
        public BagInstance Bag;
        public Vector2Int OldOrigin;
        public Vector2Int NewOrigin;
    }

    public struct ItemMovedEvent : IGameEvent
    {
        public ItemInstance Item;
        public Vector2Int OldOrigin;
        public Vector2Int NewOrigin;
        public Rotation OldRotation;
        public Rotation NewRotation;
    }

    // WS-012: per-instance lock state changed. InventoryGridView re-renders to add/remove the lock icon.
    public struct ItemLockChangedEvent : IGameEvent { public ItemInstance Item; }
    public struct BagLockChangedEvent : IGameEvent { public BagInstance Bag; }

    // WS-012: drag lifecycle on grid items / bags. SellModal listens for item drags only;
    // bag events are reserved for WS-012.5 (bag-selling via spillover) and have no consumers yet.
    public struct ItemDragBeganEvent : IGameEvent { public ItemInstance Item; }
    public struct ItemDragEndedEvent : IGameEvent { public ItemInstance Item; public bool WasCancelled; }
    public struct BagDragBeganEvent : IGameEvent { public BagInstance Bag; }
    public struct BagDragEndedEvent : IGameEvent { public BagInstance Bag; public bool WasCancelled; }
}
