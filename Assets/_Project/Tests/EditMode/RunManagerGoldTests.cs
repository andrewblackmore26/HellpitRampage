using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// Unit tests for the WS-010 gold tracking on RunManager. Same EventBus+RunManager test-host
    /// pattern as RunManagerTests so OnEnable wiring is exercised.
    /// </summary>
    public class RunManagerGoldTests
    {
        private GameObject _eventBusGO;
        private GameObject _runManagerGO;
        private RunManager _runManager;

        [SetUp]
        public void SetUp()
        {
            // EditMode AddComponent does not fire Awake/OnEnable — wake each component
            // explicitly (see EditModeLifecycle). EventBus first so RunManager.OnEnable
            // can subscribe to it.
            _eventBusGO = new GameObject("EventBusTestHost");
            EditModeLifecycle.Wake(_eventBusGO.AddComponent<EventBus>());

            _runManagerGO = new GameObject("RunManagerTestHost");
            _runManager = _runManagerGO.AddComponent<RunManager>();
            EditModeLifecycle.Wake(_runManager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_runManagerGO != null) Object.DestroyImmediate(_runManagerGO);
            if (_eventBusGO != null) Object.DestroyImmediate(_eventBusGO);
            _runManagerGO = null;
            _runManager = null;
            _eventBusGO = null;
        }

        [Test]
        public void AddGold_IncreasesCurrentGold_AndPublishesEvent()
        {
            int eventCount = 0;
            int observedNewAmount = -1;
            System.Action<GoldChangedEvent> handler = evt => { eventCount++; observedNewAmount = evt.NewAmount; };
            EventBus.Instance.Subscribe(handler);

            _runManager.AddGold(7);

            Assert.AreEqual(7, _runManager.CurrentGold);
            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(7, observedNewAmount);

            EventBus.Instance.Unsubscribe(handler);
        }

        [Test]
        public void SpendGold_DecreasesCurrentGold_AndReturnsTrue_WhenAffordable()
        {
            _runManager.AddGold(10);
            bool result = _runManager.SpendGold(4);

            Assert.IsTrue(result);
            Assert.AreEqual(6, _runManager.CurrentGold);
        }

        [Test]
        public void SpendGold_InsufficientGold_ReturnsFalse_AndDoesNotChange()
        {
            _runManager.AddGold(3);
            int eventCountBefore = 0;
            System.Action<GoldChangedEvent> handler = _ => eventCountBefore++;
            EventBus.Instance.Subscribe(handler);

            bool result = _runManager.SpendGold(10);

            Assert.IsFalse(result, "SpendGold must return false when gold is insufficient.");
            Assert.AreEqual(3, _runManager.CurrentGold, "Gold must not change on failed spend.");
            Assert.AreEqual(0, eventCountBefore, "No GoldChangedEvent on failed spend.");

            EventBus.Instance.Unsubscribe(handler);
        }

        [Test]
        public void StartNewRun_ResetsGoldToStartingAmount()
        {
            _runManager.SetStartingGoldForTesting(12);
            _runManager.AddGold(50); // pollute prior state

            _runManager.StartNewRun();

            Assert.AreEqual(12, _runManager.CurrentGold, "StartNewRun must reset CurrentGold to _startingGold.");
        }
    }
}
