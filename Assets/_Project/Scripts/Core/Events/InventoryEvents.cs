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
}
