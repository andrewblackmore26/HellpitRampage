using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Combat
{
    /// <summary>
    /// Orchestrates a combat round: runs the round timer, drives the spawner, and
    /// clears the field on round/run end. WS-014.B: spawning is gated behind
    /// <see cref="CompanionAppearanceCompleteEvent"/> (the companion beat plays first),
    /// and round 30 is a boss encounter — no timer, no waves, victory on boss death.
    /// </summary>
    public class CombatRoundController : MonoBehaviour
    {
        private const string BossEnemyId = "boss_companion_devil_placeholder";
        private const int BossRound = 30;
        private const float BossScale = 3f;
        private static readonly Color BossTint = new Color(0.5f, 0.1f, 0.12f);
        private static readonly Vector3 BossSpawnOffset = new Vector3(0f, 6f, 0f);

        [SerializeField] private EnemySpawner _spawner;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private float _roundDuration = 30f;

        private float _timeRemaining;
        private bool _timerRunning;

        // WS-014.B: the round whose RoundStartedEvent has fired but whose combat has not
        // started yet — combat waits for CompanionAppearanceCompleteEvent. -1 = none pending.
        private int _pendingRound = -1;
        private bool _bossEncounterActive;

        public float TimeRemaining => _timeRemaining;
        public bool TimerRunning => _timerRunning;

        private void OnEnable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Subscribe<RoundStartedEvent>(HandleRoundStarted);
            EventBus.Instance.Subscribe<CompanionAppearanceCompleteEvent>(HandleCompanionComplete);
            EventBus.Instance.Subscribe<RoundEndedEvent>(HandleRoundEnded);
            EventBus.Instance.Subscribe<RunEndedEvent>(HandleRunEnded);
            EventBus.Instance.Subscribe<EnemyDiedEvent>(HandleEnemyDied);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Unsubscribe<RoundStartedEvent>(HandleRoundStarted);
            EventBus.Instance.Unsubscribe<CompanionAppearanceCompleteEvent>(HandleCompanionComplete);
            EventBus.Instance.Unsubscribe<RoundEndedEvent>(HandleRoundEnded);
            EventBus.Instance.Unsubscribe<RunEndedEvent>(HandleRunEnded);
            EventBus.Instance.Unsubscribe<EnemyDiedEvent>(HandleEnemyDied);
        }

        private void Update()
        {
            // TODO(WS-pause-or-slowmo): migrate to TimeManager.CombatTime.
            if (!_timerRunning) return;
            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                _timerRunning = false;
                if (RunManager.Instance != null) RunManager.Instance.EndCurrentRound();
            }
        }

        private void HandleRoundStarted(RoundStartedEvent evt)
        {
            // WS-014.B: record the round but do NOT start the timer or spawning yet.
            // Combat begins on CompanionAppearanceCompleteEvent so the companion beat
            // always plays out first.
            _pendingRound = evt.RoundNumber;

            // Prime the displayed time so RoundTimerUI shows the full round duration
            // (not a stale 0:00) during the companion grace. _timerRunning stays false,
            // so it does not tick until HandleCompanionComplete.
            _timeRemaining = _roundDuration;

            // Gotcha #3: Rigidbody2D.position is the physics-correct teleport API.
            if (_playerTransform != null)
            {
                Rigidbody2D rb = _playerTransform.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = Vector2.zero;
                else _playerTransform.position = Vector3.zero;
            }
        }

        private void HandleCompanionComplete(CompanionAppearanceCompleteEvent _)
        {
            if (_pendingRound < 0) return;

            if (_pendingRound == BossRound)
            {
                SpawnBoss();
            }
            else
            {
                _timeRemaining = _roundDuration;
                _timerRunning = true;
                if (_spawner != null) _spawner.StartSpawning();
            }

            _pendingRound = -1;
        }

        private void HandleRoundEnded(RoundEndedEvent evt)
        {
            _timerRunning = false;
            _bossEncounterActive = false;
            if (_spawner != null) _spawner.StopSpawning();

            ClearActiveEnemies();
            ClearActiveProjectiles();
        }

        private void HandleRunEnded(RunEndedEvent evt)
        {
            _timerRunning = false;
            _bossEncounterActive = false;
            if (_spawner != null) _spawner.StopSpawning();
            ClearActiveEnemies();
            ClearActiveProjectiles();
        }

        // WS-014.B: round 30 spawns only the boss, so any enemy death during the boss
        // encounter is the boss. No EnemyDiedEvent enemy-reference needed.
        private void HandleEnemyDied(EnemyDiedEvent evt)
        {
            if (!_bossEncounterActive) return;
            _bossEncounterActive = false;
            if (RunManager.Instance != null) RunManager.Instance.EndRunVictory();
        }

        private void SpawnBoss()
        {
            EnemyData bossData = DataRegistry.Instance != null
                ? DataRegistry.Instance.GetEnemy(BossEnemyId)
                : null;
            if (bossData == null || bossData.Prefab == null)
            {
                Debug.LogError($"[CombatRoundController] Boss enemy '{BossEnemyId}' missing from " +
                               "DataRegistry or has no Prefab — round 30 cannot start.");
                return;
            }
            if (PoolManager.Instance == null)
            {
                Debug.LogError("[CombatRoundController] PoolManager.Instance is null — cannot spawn boss.");
                return;
            }

            Transform player = ResolvePlayerTransform();
            Vector3 origin = player != null ? player.position : Vector3.zero;

            GameObject instance = PoolManager.Instance.Get(bossData.Prefab);
            if (instance == null) return;
            instance.transform.position = origin + BossSpawnOffset;
            instance.transform.rotation = Quaternion.identity;

            Enemy boss = instance.GetComponent<Enemy>();
            if (boss != null) boss.Initialize(bossData, player);

            // Placeholder visual: a scaled-up, dark-red basic enemy. Initialize() restores
            // the prefab's default scale/tint first, so this override does not leak back
            // into the shared pool.
            instance.transform.localScale = Vector3.one * BossScale;
            SpriteRenderer sr = instance.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = BossTint;

            _bossEncounterActive = true;
        }

        private Transform ResolvePlayerTransform()
        {
            if (_playerTransform != null) return _playerTransform;
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) _playerTransform = playerGO.transform;
            return _playerTransform;
        }

        private static void ClearActiveEnemies()
        {
            // L-004: no-arg FindObjectsByType, the FindObjectsSortMode overload is deprecated.
            Enemy[] all = Object.FindObjectsByType<Enemy>();
            foreach (var e in all)
            {
                if (e == null) continue;
                if (!e.gameObject.activeInHierarchy) continue;
                if (PoolManager.Instance != null) PoolManager.Instance.Release(e.gameObject);
            }
        }

        private static void ClearActiveProjectiles()
        {
            Projectile[] all = Object.FindObjectsByType<Projectile>();
            foreach (var p in all)
            {
                if (p == null) continue;
                if (!p.gameObject.activeInHierarchy) continue;
                if (PoolManager.Instance != null) PoolManager.Instance.Release(p.gameObject);
            }
        }
    }
}
