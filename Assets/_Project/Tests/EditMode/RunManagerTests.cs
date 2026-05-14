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
            // EventBus must exist BEFORE RunManager so RunManager.OnEnable subscribes to PlayerDiedEvent.
            _eventBusGO = new GameObject("EventBusTestHost");
            _eventBusGO.AddComponent<EventBus>();

            _runManagerGO = new GameObject("RunManagerTestHost");
            _runManager = _runManagerGO.AddComponent<RunManager>();
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
        public void StartNewRun_PublishesRunStartedAndRoundStartedEvents()
        {
            int runStartedCount = 0;
            int roundStartedCount = 0;
            int observedRoundNumber = -1;

            System.Action<RunStartedEvent> runHandler = _ => runStartedCount++;
            System.Action<RoundStartedEvent> roundHandler = evt =>
            {
                roundStartedCount++;
                observedRoundNumber = evt.RoundNumber;
            };
            EventBus.Instance.Subscribe(runHandler);
            EventBus.Instance.Subscribe(roundHandler);

            _runManager.StartNewRun();

            Assert.AreEqual(1, runStartedCount, "RunStartedEvent must fire exactly once.");
            Assert.AreEqual(1, roundStartedCount, "RoundStartedEvent must fire exactly once.");
            Assert.AreEqual(1, observedRoundNumber, "RoundStartedEvent.RoundNumber must equal 1 on a fresh run.");

            EventBus.Instance.Unsubscribe(runHandler);
            EventBus.Instance.Unsubscribe(roundHandler);
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
            int roundStartedCount = 0;
            int lastRoundNumber = -1;
            System.Action<RoundStartedEvent> handler = evt =>
            {
                roundStartedCount++;
                lastRoundNumber = evt.RoundNumber;
            };
            EventBus.Instance.Subscribe(handler);

            _runManager.StartNewRun();    // publishes RoundStartedEvent{1}
            _runManager.EndCurrentRound();
            _runManager.AdvanceToNextRound(); // publishes RoundStartedEvent{2}

            Assert.AreEqual(2, _runManager.CurrentRound);
            Assert.AreEqual(RunManager.RunPhase.Combat, _runManager.CurrentPhase);
            Assert.AreEqual(2, roundStartedCount, "RoundStartedEvent must fire on both start and advance.");
            Assert.AreEqual(2, lastRoundNumber, "Gotcha #9: CurrentRound must be incremented before publish.");

            EventBus.Instance.Unsubscribe(handler);
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
