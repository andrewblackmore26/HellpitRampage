using HellpitRampage.Core;
using UnityEngine;
using UnityEngine.Events;

namespace HellpitRampage.Combat
{
    /// <summary>
    /// Generic HP container for damageable entities (player, enemies, future bosses).
    /// Lifetime-aware: resets in OnEnable so pooled instances start fresh.
    /// Publishes EnemyDiedEvent or PlayerDiedEvent on death depending on Owner.
    /// </summary>
    public class Health : MonoBehaviour
    {
        public enum Owner { Enemy, Player }

        [SerializeField] private Owner _owner = Owner.Enemy;
        [SerializeField] private float _startingHP = 100f;

        /// <summary>Local UnityEvent for direct subscribers (e.g., HP bar UI). EventBus carries the death event globally.</summary>
        // Initialized explicitly so AddComponent<Health>() in tests (no serialization round-trip) has a usable event.
        public UnityEvent<float, float> OnHealthChanged = new UnityEvent<float, float>();

        private float _maxHP;
        private float _currentHP;
        private bool _isDead;

        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsDead => _isDead;

        private void OnEnable()
        {
            // Reset state on every (re)activation so pooled instances are clean.
            // Initialize(float) may override _maxHP afterward; this is the default.
            _maxHP = _startingHP;
            _currentHP = _maxHP;
            _isDead = false;
            OnHealthChanged?.Invoke(_currentHP, _maxHP);
        }

        /// <summary>Optional override for spawners that source MaxHP from a data asset (e.g., EnemyData).</summary>
        public void Initialize(float maxHP)
        {
            _maxHP = Mathf.Max(1f, maxHP);
            _currentHP = _maxHP;
            _isDead = false;
            OnHealthChanged?.Invoke(_currentHP, _maxHP);
        }

        public void TakeDamage(float amount)
        {
            if (_isDead) return;
            if (amount <= 0f) return;

            _currentHP = Mathf.Max(0f, _currentHP - amount);
            OnHealthChanged?.Invoke(_currentHP, _maxHP);

            if (_currentHP <= 0f) HandleDeath();
        }

        private void HandleDeath()
        {
            _isDead = true;

            if (_owner == Owner.Enemy)
            {
                int goldAmount = 0;
                Enemy enemy = GetComponent<Enemy>();
                if (enemy != null && enemy.Data != null) goldAmount = enemy.Data.GoldDropAmount;

                // Capture position BEFORE Release deactivates the GameObject — transform.position is still valid here.
                if (EventBus.Instance != null)
                {
                    EventBus.Instance.Publish(new EnemyDiedEvent
                    {
                        Position = transform.position,
                        GoldAmount = goldAmount
                    });
                }

                if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
            }
            else if (_owner == Owner.Player)
            {
                if (EventBus.Instance != null)
                    EventBus.Instance.Publish(new PlayerDiedEvent { PlayerObject = gameObject });
            }
        }
    }
}
