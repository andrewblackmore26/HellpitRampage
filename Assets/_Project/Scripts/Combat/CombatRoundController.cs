using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Combat
{
    public class CombatRoundController : MonoBehaviour
    {
        [SerializeField] private EnemySpawner _spawner;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private float _roundDuration = 30f;

        private float _timeRemaining;
        private bool _timerRunning;

        public float TimeRemaining => _timeRemaining;
        public bool TimerRunning => _timerRunning;

        private void OnEnable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Subscribe<RoundStartedEvent>(HandleRoundStarted);
            EventBus.Instance.Subscribe<RoundEndedEvent>(HandleRoundEnded);
            EventBus.Instance.Subscribe<RunEndedEvent>(HandleRunEnded);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Unsubscribe<RoundStartedEvent>(HandleRoundStarted);
            EventBus.Instance.Unsubscribe<RoundEndedEvent>(HandleRoundEnded);
            EventBus.Instance.Unsubscribe<RunEndedEvent>(HandleRunEnded);
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
            _timeRemaining = _roundDuration;
            _timerRunning = true;

            // Gotcha #3: Rigidbody2D.position is the physics-correct teleport API.
            if (_playerTransform != null)
            {
                Rigidbody2D rb = _playerTransform.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = Vector2.zero;
                else _playerTransform.position = Vector3.zero;
            }

            if (_spawner != null) _spawner.StartSpawning();
        }

        private void HandleRoundEnded(RoundEndedEvent evt)
        {
            _timerRunning = false;
            if (_spawner != null) _spawner.StopSpawning();

            ClearActiveEnemies();
            ClearActiveProjectiles();
        }

        private void HandleRunEnded(RunEndedEvent evt)
        {
            _timerRunning = false;
            if (_spawner != null) _spawner.StopSpawning();
            ClearActiveEnemies();
            ClearActiveProjectiles();
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
