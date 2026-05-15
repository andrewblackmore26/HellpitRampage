using HellpitRampage.Inventory;
using HellpitRampage.UI;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012.3: state-transition tests for the unified tooltip controller. The controller
    /// builds its UI tree dynamically in Awake; the test GameObject needs a RectTransform
    /// so EnsureBuilt can root the backdrop/panel under it.
    /// </summary>
    public class TooltipControllerTests
    {
        private TooltipController _controller;
        private ItemData _itemA;
        private ItemData _itemB;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TooltipControllerHost", typeof(RectTransform));
            _controller = go.AddComponent<TooltipController>();
            _itemA = ScriptableObject.CreateInstance<ItemData>();
            _itemB = ScriptableObject.CreateInstance<ItemData>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller != null) Object.DestroyImmediate(_controller.gameObject);
            if (_itemA != null) Object.DestroyImmediate(_itemA);
            if (_itemB != null) Object.DestroyImmediate(_itemB);
        }

        [Test]
        public void DefaultState_NotShowing_NotPinned()
        {
            Assert.IsFalse(_controller.IsShowing);
            Assert.IsFalse(_controller.IsPinned);
        }

        [Test]
        public void ShowHovering_SetsShowing_DoesNotPin()
        {
            _controller.ShowHovering(_itemA, Vector2.zero, source: null);
            Assert.IsTrue(_controller.IsShowing);
            Assert.IsFalse(_controller.IsPinned);
        }

        [Test]
        public void Pin_SetsShowingAndPinned()
        {
            _controller.Pin(_itemA, Vector2.zero);
            Assert.IsTrue(_controller.IsShowing);
            Assert.IsTrue(_controller.IsPinned);
        }

        [Test]
        public void Pin_SameItemTwice_TogglesOff()
        {
            _controller.Pin(_itemA, Vector2.zero);
            Assert.IsTrue(_controller.IsPinned);
            _controller.Pin(_itemA, Vector2.zero);
            Assert.IsFalse(_controller.IsPinned);
            Assert.IsFalse(_controller.IsShowing);
        }

        [Test]
        public void Pin_DifferentItem_StaysPinnedAndSwaps()
        {
            _controller.Pin(_itemA, Vector2.zero);
            _controller.Pin(_itemB, Vector2.zero);
            Assert.IsTrue(_controller.IsPinned);
            Assert.IsTrue(_controller.IsShowing);
        }

        [Test]
        public void Unpin_ClearsAllState()
        {
            _controller.Pin(_itemA, Vector2.zero);
            _controller.Unpin();
            Assert.IsFalse(_controller.IsPinned);
            Assert.IsFalse(_controller.IsShowing);
        }

        [Test]
        public void ShowHovering_WhilePinned_IsIgnored()
        {
            _controller.Pin(_itemA, Vector2.zero);
            Assert.IsTrue(_controller.IsPinned);
            _controller.ShowHovering(_itemB, Vector2.zero, source: null);
            // Pin takes priority — hover does not replace pinned content or clear the pin.
            Assert.IsTrue(_controller.IsPinned);
            Assert.IsTrue(_controller.IsShowing);
        }

        [Test]
        public void HideIfNotPinned_WhilePinned_DoesNothing()
        {
            _controller.Pin(_itemA, Vector2.zero);
            _controller.HideIfNotPinned(source: null);
            Assert.IsTrue(_controller.IsPinned);
            Assert.IsTrue(_controller.IsShowing);
        }
    }
}
