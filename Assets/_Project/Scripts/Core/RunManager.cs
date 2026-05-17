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
        // WS-015: fresh-run player HP. HeroData carries no HP stat yet (WS-018+ hero spec).
        [SerializeField] private float _startingPlayerHp = 100f;

        // WS-013: hero scaffolding. Single hero today; multi-hero unlocks fill this meaningfully later.
        [SerializeField] private HeroData _defaultHero;

        public int CurrentRound { get; private set; }
        public RunPhase CurrentPhase { get; private set; } = RunPhase.Idle;
        public int TotalRounds => _totalRounds;
        public int CurrentGold { get; private set; }
        public HeroData CurrentHero { get; private set; }

        // WS-015: player HP is owned here so it carries across the Combat<->Shop scene
        // transitions (the player GameObject is destroyed on every scene unload). The Combat
        // scene's Health component is seeded from these and writes CurrentHp back on damage.
        public float CurrentHp { get; set; }
        public float MaxHp { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // L-001: persist the root, not the child. Singleton sits under `Managers`.
            // Guarded: DontDestroyOnLoad is play-mode-only and throws in EditMode tests.
            if (Application.isPlaying) DontDestroyOnLoad(transform.root.gameObject);
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
            CurrentHero = _defaultHero;

            MaxHp = _startingPlayerHp;
            CurrentHp = _startingPlayerHp;

            int oldGold = CurrentGold;
            CurrentGold = _startingGold;

            if (EventBus.Instance != null)
            {
                // Publish gold first so GoldDisplay shows "Gold: 10" before any subscriber reacts to RunStarted.
                EventBus.Instance.Publish(new GoldChangedEvent { OldAmount = oldGold, NewAmount = CurrentGold });
                EventBus.Instance.Publish(new RunStartedEvent());
                // WS-015: RoundStartedEvent is published by CombatSceneBootstrap once the
                // Combat scene has loaded — combat-scene subscribers don't exist yet here.
            }
        }

        /// <summary>
        /// WS-013: restores run state from a save without re-firing RunStarted / RoundStarted.
        /// Phase is set to Shop because saves only happen at shop-phase entry, so the player
        /// always resumes there. WS-015: also restores the carried player HP. Publishes
        /// GoldChangedEvent so UI repaints.
        /// </summary>
        public void RestoreFromSave(int round, int gold, HeroData hero, float currentHp, float maxHp)
        {
            CurrentRound = Mathf.Clamp(round, 1, _totalRounds);
            CurrentPhase = RunPhase.Shop;
            CurrentHero = hero != null ? hero : _defaultHero;

            MaxHp = maxHp > 0f ? maxHp : _startingPlayerHp;
            CurrentHp = Mathf.Clamp(currentHp, 0f, MaxHp);

            int oldGold = CurrentGold;
            CurrentGold = Mathf.Max(0, gold);
            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new GoldChangedEvent { OldAmount = oldGold, NewAmount = CurrentGold });
        }

        public void EndCurrentRound()
        {
            if (CurrentPhase != RunPhase.Combat) return;

            CurrentPhase = RunPhase.Shop;
            bool runEnding = CurrentRound >= _totalRounds;

            // RoundEndedEvent drives in-scene combat cleanup (spawner stop, enemy/projectile
            // clear, gold sweep) — published while the Combat scene is still loaded.
            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new RoundEndedEvent { RoundNumber = CurrentRound });

            if (runEnding)
            {
                // Final round: the run-end overlay takes over — no shop, no bonus gold.
                EndRun(victory: true);
                return;
            }

            AddGold(_roundEndBonusGold);

            // WS-015: the shop is its own scene now. Load it; ShopSceneBootstrap publishes
            // ShopPhaseStartedEvent once it is live (drives auto-save, GroundArea, drag mode).
            if (SceneRouter.Instance != null) SceneRouter.Instance.LoadShop();
        }

        public void AdvanceToNextRound()
        {
            if (CurrentPhase != RunPhase.Shop) return;
            if (CurrentRound >= _totalRounds) return;

            CurrentRound++;
            CurrentPhase = RunPhase.Combat;

            // WS-015: load the Combat scene; CombatSceneBootstrap publishes RoundStartedEvent
            // once it is live, so combat-scene subscribers receive it in their own scene.
            if (SceneRouter.Instance != null) SceneRouter.Instance.LoadCombat();
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

        /// <summary>
        /// WS-014.B: ends the run as a victory. Called by CombatRoundController when the
        /// round-30 boss is defeated. Round 30 runs no round timer, so the timed-victory
        /// path in <see cref="EndCurrentRound"/> is bypassed and this is the win trigger.
        /// Idempotent — a no-op once the run has already ended.
        /// </summary>
        public void EndRunVictory()
        {
            if (CurrentPhase == RunPhase.RunEnd) return;
            EndRun(victory: true);
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
