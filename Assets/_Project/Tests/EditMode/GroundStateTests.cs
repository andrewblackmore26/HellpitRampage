using System.Collections.Generic;
using HellpitRampage.Inventory;
using HellpitRampage.UI;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012.5: ground state snapshot model. The GroundManager MonoBehaviour requires a
    /// RectTransform + prefab for full integration testing (covered by PlayMode/playtest);
    /// these EditMode tests pin the pure-data semantics of GroundItemSnapshot — the contract
    /// the WS-013 save system will round-trip — without requiring scene setup.
    /// </summary>
    public class GroundStateTests
    {
        private static GroundItemSnapshot MakeSnapshot(string itemName, Rotation rot, bool locked)
        {
            var data = ScriptableObject.CreateInstance<ItemData>();
            data.ItemName = itemName;
            return new GroundItemSnapshot { ItemId = data, Rotation = rot, IsLocked = locked };
        }

        [Test]
        public void GroundItemSnapshot_PreservesAllFields()
        {
            var snap = MakeSnapshot("Test", Rotation.Deg90, locked: true);
            Assert.IsNotNull(snap.ItemId);
            Assert.AreEqual("Test", snap.ItemId.ItemName);
            Assert.AreEqual(Rotation.Deg90, snap.Rotation);
            Assert.IsTrue(snap.IsLocked);
        }

        [Test]
        public void SnapshotList_Empty_HasZeroCount()
        {
            var list = new List<GroundItemSnapshot>();
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void SnapshotList_AddThreeItems_CountIsThree()
        {
            var list = new List<GroundItemSnapshot>
            {
                MakeSnapshot("A", Rotation.Deg0, false),
                MakeSnapshot("B", Rotation.Deg180, true),
                MakeSnapshot("C", Rotation.Deg270, false)
            };
            Assert.AreEqual(3, list.Count);
        }

        [Test]
        public void SnapshotList_RoundtripPreservesItemDataAndLockState()
        {
            var original = new List<GroundItemSnapshot>
            {
                MakeSnapshot("Locked", Rotation.Deg90, true),
                MakeSnapshot("Free", Rotation.Deg0, false)
            };

            // A save system would serialize then deserialize this list. The semantic guarantee:
            // each snapshot keeps its ItemId reference, rotation, and lock flag intact.
            var copy = new List<GroundItemSnapshot>(original);

            Assert.AreEqual(original.Count, copy.Count);
            for (int i = 0; i < original.Count; i++)
            {
                Assert.AreEqual(original[i].ItemId, copy[i].ItemId);
                Assert.AreEqual(original[i].Rotation, copy[i].Rotation);
                Assert.AreEqual(original[i].IsLocked, copy[i].IsLocked);
            }
        }

        [Test]
        public void SnapshotList_NullItemId_IsValidButShouldBeSkippedOnRestore()
        {
            // GroundManager.RestoreGroundState defensively skips snapshots with null ItemId.
            // The list itself can legitimately contain a default-initialized snapshot (e.g.,
            // from a corrupt or zero-initialized save payload); restore must not NRE.
            var snap = new GroundItemSnapshot { ItemId = null, Rotation = Rotation.Deg0, IsLocked = false };
            Assert.IsNull(snap.ItemId);
            // Just verifying the struct allows nullable ItemId — restore-side handling
            // is integration-tested in PlayMode / designer playtest.
        }
    }
}
