using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// Unit tests for <see cref="RunManager"/>. Covers WS-007 state-machine transitions
    /// (Combat -> Shop -> Combat, Combat -> RunEnd via timeout victory, Combat -> RunEnd via player death)
    /// and event publishing. EventBus is required because RunManager publishes through it.
    /// </summary>
    public class RunManagerTests
    {
        private GameObject _eventBusGO;
        private GameObject _runManagerGO;
        private RunManager _runManager;

        [SetUp]
        public void SetUp()
        {
            // EditMode AddComponent does not fire Awake/OnEnable — wake each component
            // explicitly (see EditModeLifecycle). EventBus first so RunManager.OnEnable can
            // subscribe to it for PlayerDiedEvent.
            _eventBusGO = new GameObject("EventBusTestHost");
            EditModeLifecycle.Wake(_eventBusGO.AddComponent<EventBus>());

            _runManagerGO = new GameObject("RunManagerTestHost");
            _runManager = _runManagerGO.AddComponent<RunManager>();
            EditModeLifecycle.Wake(_runManager);
        }

        [TearDown]
        public void TearDown()
        {
            // RunManager first so its OnDisable still sees a live EventBus.Instance to unsubscribe from.
            if (_runManagerGO != null) Object.DestroyImmediate(_runManagerGO);
            if (_eventBusGO != null) Object.DestroyImmediate(_eventBusGO);
            _runManagerGO = null;
            _runManager = null;
            _eventBusGO = null;
        }

        [Test]
        public void StartNewRun_SetsRoundOneAndCombatPhase()
        {
            _runManager.StartNewRun();

            Assert.AreEqual(1, _runManager.CurrentRound);
            Assert.AreEqual(RunManager.RunPhase.Combat, _runManager.CurrentPhase);
        }

        [Test]
        public void StartNewRun_PublishesRunStartedEvent()
        {
            // WS-015: StartNewRun publishes RunStartedEvent; RoundStartedEvent is now
            // published by CombatSceneBootstrap once the Combat scene has loaded.
            int runStartedCount = 0;
            System.Action<RunStartedEvent> runHandler = _ => runStartedCount++;
            EventBus.Instance.Subscribe(runHandler);

            _runManager.StartNewRun();

            Assert.AreEqual(1, runStartedCount, "RunStartedEvent must fire exactly once.");

            EventBus.Instance.Unsubscribe(runHandler);
        }

        [Test]
        public void EndCurrentRound_TransitionsToShop_AndPublishesRoundEnded()
        {
            int roundEndedCount = 0;
            int observedRoundNumber = -1;
            System.Action<RoundEndedEvent> handler = evt =>
            {
                roundEndedCount++;
                observedRoundNumber = evt.RoundNumber;
            };
            EventBus.Instance.Subscribe(handler);

            _runManager.StartNewRun();
            _runManager.EndCurrentRound();

            Assert.AreEqual(RunManager.RunPhase.Shop, _runManager.CurrentPhase);
            Assert.AreEqual(1, roundEndedCount, "RoundEndedEvent must fire exactly once.");
            Assert.AreEqual(1, observedRoundNumber);

            EventBus.Instance.Unsubscribe(handler);
        }

        [Test]
        public void AdvanceToNextRound_IncrementsAndReturnsToCombat()
        {
            // WS-015: AdvanceToNextRound increments the round and re-enters the Combat
            // phase, then loads the Combat scene via SceneRouter (null/no-op in EditMode).
            // RoundStartedEvent is published by CombatSceneBootstrap, not here.
            _runManager.StartNewRun();
            _runManager.EndCurrentRound();
            _runManager.AdvanceToNextRound();

            Assert.AreEqual(2, _runManager.CurrentRound);
            Assert.AreEqual(RunManager.RunPhase.Combat, _runManager.CurrentPhase);
        }

        [Test]
        public void EndCurrentRound_OnLastRound_PublishesRunEndedWithVictoryTrue()
        {
            _runManager.SetTotalRoundsForTesting(1);

            int runEndedCount = 0;
            bool? observedVictory = null;
            System.Action<RunEndedEvent> handler = evt =>
            {
                runEndedCount++;
                observedVictory = evt.Victory;
            };
            EventBus.Instance.Subscribe(handler);

            _runManager.StartNewRun();
            _runManager.EndCurrentRound();

            Assert.AreEqual(1, runEndedCount, "RunEndedEvent must fire when the final round ends.");
            Assert.IsTrue(observedVictory.HasValue && observedVictory.Value, "Final-round completion must publish Victory=true.");
            Assert.AreEqual(RunManager.RunPhase.RunEnd, _runManager.CurrentPhase);

            EventBus.Instance.Unsubscribe(handler);
        }

        [Test]
        public void HandlePlayerDied_PublishesRunEndedWithVictoryFalse()
        {
            int runEndedCount = 0;
            bool? observedVictory = null;
            System.Action<RunEndedEvent> handler = evt =>
            {
                runEndedCount++;
                observedVictory = evt.Victory;
            };
            EventBus.Instance.Subscribe(handler);

            _runManager.StartNewRun();
            EventBus.Instance.Publish(new PlayerDiedEvent { PlayerObject = null });

            Assert.AreEqual(1, runEndedCount, "PlayerDiedEvent must cause exactly one RunEndedEvent.");
            Assert.IsTrue(observedVictory.HasValue && !observedVictory.Value, "Player death must publish Victory=false.");
            Assert.AreEqual(RunManager.RunPhase.RunEnd, _runManager.CurrentPhase);

            EventBus.Instance.Unsubscribe(handler);
        }
    }
}
