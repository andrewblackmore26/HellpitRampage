using HellpitRampage.Core;
using HellpitRampage.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012 lock state: ToggleItemLock / ToggleBagLock flip IsLocked, publish the
    /// corresponding LockChangedEvent, and are null-tolerant. Same EventBus + Service
    /// test-host pattern as RunManagerGoldTests.
    /// </summary>
    public class ItemLockTests
    {
        private GameObject _eventBusGO;
        private GameObject _serviceGO;
        private InventoryService _service;

        [SetUp]
        public void SetUp()
        {
            // EditMode AddComponent does not fire Awake/OnEnable — wake each component
            // explicitly (see EditModeLifecycle). EventBus first so the service's
            // LockChanged events have a live bus to publish through.
            _eventBusGO = new GameObject("EventBusTestHost");
            EditModeLifecycle.Wake(_eventBusGO.AddComponent<EventBus>());

            _serviceGO = new GameObject("InventoryServiceTestHost");
            _service = _serviceGO.AddComponent<InventoryService>();
            EditModeLifecycle.Wake(_service);
        }

        [TearDown]
        public void TearDown()
        {
            if (_serviceGO != null) Object.DestroyImmediate(_serviceGO);
            if (_eventBusGO != null) Object.DestroyImmediate(_eventBusGO);
            _serviceGO = null;
            _service = null;
            _eventBusGO = null;
        }

        private static ItemInstance MakeItem()
        {
            var shape = ScriptableObject.CreateInstance<ItemShape>();
            shape.Cells.Add(new Vector2Int(0, 0));
            var data = ScriptableObject.CreateInstance<ItemData>();
            data.ItemName = "TestItem";
            data.Shape = shape;
            return new ItemInstance(1, data, Vector2Int.zero, hostBag: null);
        }

        private static BagInstance MakeBag()
        {
            var shape = ScriptableObject.CreateInstance<ItemShape>();
            shape.Cells.Add(new Vector2Int(0, 0));
            var data = ScriptableObject.CreateInstance<BagData>();
            data.BagName = "TestBag";
            data.Shape = shape;
            return new BagInstance(1, data, Vector2Int.zero);
        }

        [Test]
        public void ToggleItemLock_FlipsIsLocked()
        {
            var item = MakeItem();
            Assert.IsFalse(item.IsLocked, "Items must start unlocked.");

            _service.ToggleItemLock(item);
            Assert.IsTrue(item.IsLocked, "First toggle must lock.");

            _service.ToggleItemLock(item);
            Assert.IsFalse(item.IsLocked, "Second toggle must unlock.");
        }

        [Test]
        public void ToggleBagLock_FlipsIsLocked()
        {
            var bag = MakeBag();
            Assert.IsFalse(bag.IsLocked, "Bags must start unlocked.");

            _service.ToggleBagLock(bag);
            Assert.IsTrue(bag.IsLocked);

            _service.ToggleBagLock(bag);
            Assert.IsFalse(bag.IsLocked);
        }

        [Test]
        public void ToggleItemLock_PublishesItemLockChangedEvent()
        {
            var item = MakeItem();
            int firedCount = 0;
            ItemInstance lastSeen = null;
            System.Action<ItemLockChangedEvent> handler = e =>
            {
                firedCount++;
                lastSeen = e.Item;
            };
            EventBus.Instance.Subscribe(handler);

            _service.ToggleItemLock(item);

            Assert.AreEqual(1, firedCount, "Exactly one ItemLockChangedEvent must fire per toggle.");
            Assert.AreSame(item, lastSeen, "Event payload must reference the toggled item.");

            EventBus.Instance.Unsubscribe(handler);
        }

        [Test]
        public void ToggleBagLock_PublishesBagLockChangedEvent()
        {
            var bag = MakeBag();
            int firedCount = 0;
            BagInstance lastSeen = null;
            System.Action<BagLockChangedEvent> handler = e =>
            {
                firedCount++;
                lastSeen = e.Bag;
            };
            EventBus.Instance.Subscribe(handler);

            _service.ToggleBagLock(bag);

            Assert.AreEqual(1, firedCount);
            Assert.AreSame(bag, lastSeen);

            EventBus.Instance.Unsubscribe(handler);
        }

        [Test]
        public void ToggleItemLock_NullItem_DoesNotThrow()
        {
            // Null-tolerance so right-click handlers don't need defensive guards.
            Assert.DoesNotThrow(() => _service.ToggleItemLock(null));
        }
    }
}
