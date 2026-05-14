using UnityEngine;

namespace HellpitRampage.Core
{
    public class RunManager : MonoBehaviour
    {
        public enum RunPhase { Idle, Combat, Shop, RunEnd }

        public static RunManager Instance { get; private set; }

        [SerializeField] private int _totalRounds = 30;
        [SerializeField] private int _startingGold = 10;
        [SerializeField] private int _roundEndBonusGold = 5;

        public int CurrentRound { get; private set; }
        public RunPhase CurrentPhase { get; private set; } = RunPhase.Idle;
        public int TotalRounds => _totalRounds;
        public int CurrentGold { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // L-001: persist the root, not the child. Singleton sits under `Managers`.
            DontDestroyOnLoad(transform.root.gameObject);
        }

        private void OnEnable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Subscribe<PlayerDiedEvent>(HandlePlayerDied);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Unsubscribe<PlayerDiedEvent>(HandlePlayerDied);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartNewRun()
        {
            CurrentRound = 1;
            CurrentPhase = RunPhase.Combat;

            int oldGold = CurrentGold;
            CurrentGold = _startingGold;

            if (EventBus.Instance != null)
            {
                // Publish gold first so GoldDisplay shows "Gold: 10" before any subscriber reacts to RunStarted.
                EventBus.Instance.Publish(new GoldChangedEvent { OldAmount = oldGold, NewAmount = CurrentGold });
                EventBus.Instance.Publish(new RunStartedEvent());
                EventBus.Instance.Publish(new RoundStartedEvent { RoundNumber = CurrentRound });
            }
        }

        public void EndCurrentRound()
        {
            if (CurrentPhase != RunPhase.Combat) return;

            CurrentPhase = RunPhase.Shop;
            bool runEnding = CurrentRound >= _totalRounds;

            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new RoundEndedEvent { RoundNumber = CurrentRound });

            // Round-end gold reward, but only if the run continues. On the final round the run-end
            // overlay takes over, so awarding gold post-victory would refresh a dead shop.
            if (!runEnding) AddGold(_roundEndBonusGold);

            if (runEnding) EndRun(victory: true);
        }

        public void AdvanceToNextRound()
        {
            if (CurrentPhase != RunPhase.Shop) return;
            if (CurrentRound >= _totalRounds) return;

            // Gotcha #9: increment BEFORE publish so handlers see the new round number.
            CurrentRound++;
            CurrentPhase = RunPhase.Combat;

            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new RoundStartedEvent { RoundNumber = CurrentRound });
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            int old = CurrentGold;
            CurrentGold += amount;
            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new GoldChangedEvent { OldAmount = old, NewAmount = CurrentGold });
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (CurrentGold < amount) return false;
            int old = CurrentGold;
            CurrentGold -= amount;
            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new GoldChangedEvent { OldAmount = old, NewAmount = CurrentGold });
            return true;
        }

        private void HandlePlayerDied(PlayerDiedEvent _)
        {
            EndRun(victory: false);
        }

        private void EndRun(bool victory)
        {
            CurrentPhase = RunPhase.RunEnd;
            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new RunEndedEvent { Victory = victory });
        }

        // Test-only mutator. Same pattern as SaveManager._savePathOverride.
        internal void SetTotalRoundsForTesting(int rounds) => _totalRounds = Mathf.Max(1, rounds);
        internal void SetStartingGoldForTesting(int gold) => _startingGold = Mathf.Max(0, gold);
    }
}
