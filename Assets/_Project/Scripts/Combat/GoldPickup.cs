using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class GoldPickup : MonoBehaviour
    {
        [SerializeField] private int _amount = 1;
        [SerializeField] private float _lifetime = 30f;

        private float _lifeRemaining;
        // L-005: pooled object self-releases from BOTH the trigger callback AND the Update lifetime
        // path. Both can fire in the same physics step. Guard against re-entry.
        private bool _isDespawned;

        public void Initialize(int amount)
        {
            _amount = amount;
            _lifeRemaining = _lifetime;
        }

        private void OnEnable()
        {
            _lifeRemaining = _lifetime;
            _isDespawned = false;
        }

        private void Update()
        {
            if (_isDespawned) return;
            _lifeRemaining -= Time.deltaTime;
            if (_lifeRemaining <= 0f) Despawn();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isDespawned) return;
            if (!other.CompareTag("Player")) return;
            if (RunManager.Instance != null) RunManager.Instance.AddGold(_amount);
            Despawn();
        }

        private void Despawn()
        {
            if (_isDespawned) return;
            _isDespawned = true;
            if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
        }
    }
}
