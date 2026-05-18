using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-015: RunManager owns the player's HP so it survives the Combat&lt;-&gt;Shop scene
    /// transitions (the player GameObject is destroyed on every scene unload). Covers the
    /// StartNewRun seed and the RestoreFromSave application + clamping.
    /// </summary>
    public class HpCarryoverTests
    {
        private GameObject _eventBusGO;
        private GameObject _runManagerGO;
        private RunManager _runManager;

        [SetUp]
        public void SetUp()
        {
            // EditMode AddComponent does not fire Awake/OnEnable — wake explicitly. EventBus
            // first so RunManager's published events have a live bus.
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
        public void StartNewRun_SeedsCurrentHpToMaxHp()
        {
            _runManager.StartNewRun();
            Assert.Greater(_runManager.MaxHp, 0f, "StartNewRun must seed a positive MaxHp.");
            Assert.AreEqual(_runManager.MaxHp, _runManager.CurrentHp,
                "A fresh run must start at full HP.");
        }

        [Test]
        public void RestoreFromSave_AppliesSavedHp()
        {
            _runManager.RestoreFromSave(3, 10, null, currentHp: 42f, maxHp: 88f);
            Assert.AreEqual(88f, _runManager.MaxHp, 0.0001f);
            Assert.AreEqual(42f, _runManager.CurrentHp, 0.0001f);
        }

        [Test]
        public void RestoreFromSave_ClampsCurrentHpToMax()
        {
            _runManager.RestoreFromSave(3, 10, null, currentHp: 999f, maxHp: 88f);
            Assert.AreEqual(88f, _runManager.CurrentHp, 0.0001f,
                "CurrentHp must clamp to MaxHp on restore.");
        }

        [Test]
        public void RestoreFromSave_NonPositiveMaxHp_FallsBackToStartingHp()
        {
            _runManager.RestoreFromSave(3, 10, null, currentHp: 50f, maxHp: 0f);
            Assert.Greater(_runManager.MaxHp, 0f,
                "A zero saved MaxHp must fall back to the configured starting HP.");
        }

        [Test]
        public void CurrentHp_IsWritable()
        {
            // The Combat scene's Health component writes damage back through this setter.
            _runManager.CurrentHp = 33f;
            Assert.AreEqual(33f, _runManager.CurrentHp, 0.0001f);
        }
    }
}
