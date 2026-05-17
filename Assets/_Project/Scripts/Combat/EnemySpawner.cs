using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Combat
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("What to spawn")]
        [SerializeField] private EnemyData _enemyData;

        [Header("Spawn cadence")]
        [Tooltip("Enemies spawned per second.")]
        [SerializeField] private float _spawnRate = 1f;

        [Tooltip("Distance from the player at which to spawn enemies.")]
        [SerializeField] private float _spawnDistance = 12f;

        [Header("Targeting")]
        [Tooltip("Optional explicit player reference. If null, found by Player tag on first spawn.")]
        [SerializeField] private Transform _playerTransform;

        [Header("Pool")]
        [SerializeField] private int _prewarmCount = 20;

        private float _spawnAccumulator;
        private bool _spawning;

        private void Start()
        {
            if (_enemyData != null && _enemyData.Prefab != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.Prewarm(_enemyData.Prefab, _prewarmCount);
            }
            // WS-014.B: spawning is driven exclusively by CombatRoundController (it starts
            // only after the companion appearance completes). No self-start here — a
            // self-start would race the round-start gating depending on Start() order.
        }

        public void StartSpawning() => _spawning = true;
        public void StopSpawning() => _spawning = false;

        private void Update()
        {
            // TODO(WS-pause-or-slowmo): migrate Time.deltaTime to TimeManager.CombatTime once TimeManager exists.
            if (!_spawning) return;
            if (_enemyData == null || _enemyData.Prefab == null) return;
            if (PoolManager.Instance == null) return;

            if (_playerTransform == null)
            {
                GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
                if (playerGO == null) return; // no player yet, retry next frame
                _playerTransform = playerGO.transform;
            }

            _spawnAccumulator += Time.deltaTime * _spawnRate;
            while (_spawnAccumulator >= 1f)
            {
                SpawnOne();
                _spawnAccumulator -= 1f;
            }
        }

        private void SpawnOne()
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir == Vector2.zero) dir = Vector2.right; // Random.insideUnitCircle can land exactly on origin
            Vector2 spawnPos = (Vector2)_playerTransform.position + dir * _spawnDistance;

            GameObject instance = PoolManager.Instance.Get(_enemyData.Prefab);
            if (instance == null) return;
            instance.transform.position = spawnPos;
            instance.transform.rotation = Quaternion.identity;

            Enemy enemy = instance.GetComponent<Enemy>();
            if (enemy != null) enemy.Initialize(_enemyData, _playerTransform);
        }
    }
}
