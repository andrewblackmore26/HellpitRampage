using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012.5: DragModeService state machine. Verifies default mode, toggle behavior,
    /// event publishing, and the ShopPhaseStartedEvent reset path. The "refuse toggle
    /// during active drag" gate is covered via SetDragInProgressForTesting.
    /// </summary>
    public class DragModeServiceTests
    {
        private GameObject _busGO;
        private EventBus _bus;
        private GameObject _serviceGO;
        private DragModeService _service;

        [SetUp]
        public void SetUp()
        {
            _busGO = new GameObject("EventBusHost");
            _bus = _busGO.AddComponent<EventBus>();

            _serviceGO = new GameObject("DragModeServiceHost");
            _service = _serviceGO.AddComponent<DragModeService>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_serviceGO != null) Object.DestroyImmediate(_serviceGO);
            if (_busGO != null) Object.DestroyImmediate(_busGO);
            _serviceGO = null; _service = null; _busGO = null; _bus = null;
        }

        [Test]
        public void DefaultMode_IsItems()
        {
            Assert.AreEqual(DragMode.Items, _service.CurrentMode);
        }

        [Test]
        public void Toggle_SwitchesToBags_ThenBackToItems()
        {
            _service.Toggle();
            Assert.AreEqual(DragMode.Bags, _service.CurrentMode);
            _service.Toggle();
            Assert.AreEqual(DragMode.Items, _service.CurrentMode);
        }

        [Test]
        public void Toggle_PublishesDragModeChangedEvent()
        {
            DragMode? captured = null;
            _bus.Subscribe<DragModeChangedEvent>(e => captured = e.NewMode);

            _service.Toggle();

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(DragMode.Bags, captured.Value);
        }

        [Test]
        public void ShopPhaseStartedEvent_ResetsToItems()
        {
            _service.SetMode(DragMode.Bags);
            Assert.AreEqual(DragMode.Bags, _service.CurrentMode);

            _bus.Publish(new ShopPhaseStartedEvent { RoundNumber = 1 });

            Assert.AreEqual(DragMode.Items, _service.CurrentMode);
        }

        [Test]
        public void Toggle_DuringActiveDrag_IsIgnored()
        {
            _service.SetDragInProgressForTesting(true);
            _service.Toggle();
            Assert.AreEqual(DragMode.Items, _service.CurrentMode,
                "Toggle should be refused while a drag is in progress.");

            _service.SetDragInProgressForTesting(false);
            _service.Toggle();
            Assert.AreEqual(DragMode.Bags, _service.CurrentMode,
                "Toggle should work again once the drag ends.");
        }
    }
}
