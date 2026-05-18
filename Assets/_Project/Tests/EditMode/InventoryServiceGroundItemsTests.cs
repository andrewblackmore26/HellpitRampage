using System.Collections.Generic;
using HellpitRampage.Inventory;
using HellpitRampage.UI;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-015: InventoryService.GroundItems — the persistent ground-item snapshot list that
    /// survives the Combat&lt;-&gt;Shop scene transitions. GroundManager mirrors its live
    /// state here wholesale via SyncGroundItems.
    /// </summary>
    public class InventoryServiceGroundItemsTests
    {
        private GameObject _go;
        private InventoryService _service;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("InventoryServiceTestHost");
            _service = _go.AddComponent<InventoryService>();
            EditModeLifecycle.Wake(_service);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
            _service = null;
        }

        private static List<GroundItemSnapshot> Snapshots(int count)
        {
            var list = new List<GroundItemSnapshot>();
            for (int i = 0; i < count; i++)
                list.Add(new GroundItemSnapshot { Rotation = Rotation.Deg0, IsLocked = false });
            return list;
        }

        [Test]
        public void GroundItems_StartsEmpty()
        {
            Assert.AreEqual(0, _service.GroundItems.Count);
        }

        [Test]
        public void SyncGroundItems_StoresAllSnapshots()
        {
            _service.SyncGroundItems(Snapshots(3));
            Assert.AreEqual(3, _service.GroundItems.Count);
        }

        [Test]
        public void SyncGroundItems_ReplacesPreviousWholesale()
        {
            _service.SyncGroundItems(Snapshots(3));
            _service.SyncGroundItems(Snapshots(1));
            Assert.AreEqual(1, _service.GroundItems.Count, "SyncGroundItems must replace, not append.");
        }

        [Test]
        public void SyncGroundItems_Null_ClearsList()
        {
            _service.SyncGroundItems(Snapshots(2));
            _service.SyncGroundItems(null);
            Assert.AreEqual(0, _service.GroundItems.Count);
        }

        [Test]
        public void ClearGroundItems_EmptiesList()
        {
            _service.SyncGroundItems(Snapshots(2));
            _service.ClearGroundItems();
            Assert.AreEqual(0, _service.GroundItems.Count);
        }
    }
}
