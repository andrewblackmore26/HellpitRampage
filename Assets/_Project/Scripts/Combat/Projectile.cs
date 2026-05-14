using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        private Rigidbody2D _rb;
        private float _lifetimeRemaining;
        private float _damage;
        private bool _isDespawned;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            // Reset the despawn guard on every (re)activation so pool reuse starts clean.
            // Must be in OnEnable, not Initialize: ObjectPool.actionOnGet fires SetActive(true)
            // BEFORE Initialize, and a stale `true` here would make the projectile undamageable.
            _isDespawned = false;
        }

        /// <summary>Called by the firing weapon right after pulling this instance from the pool.</summary>
        public void Initialize(Vector2 direction, float speed, float lifetime, float damage)
        {
            // Reset velocity per WS-004 Gotcha #8 - pool reuse retains rigidbody state.
            _rb.linearVelocity = Vector2.zero;

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _rb.linearVelocity = dir * speed;

            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);

            _lifetimeRemaining = lifetime;
            _damage = damage;
        }

        private void Update()
        {
            // TODO(WS-pause-or-slowmo): migrate Time.deltaTime to TimeManager.CombatTime.
            if (_isDespawned) return;
            _lifetimeRemaining -= Time.deltaTime;
            if (_lifetimeRemaining <= 0f) Despawn();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Unity batches collision callbacks within a physics step, so a projectile
            // overlapping multiple enemies triggers OnTriggerEnter2D once per enemy BEFORE
            // SetActive(false) takes effect. Early-out on the second-and-later hits so we
            // don't double-release and don't apply damage past the design's one-hit cap.
            if (_isDespawned) return;

            // Ignore projectile-projectile contact (per Gotcha #5).
            if (other.GetComponent<Projectile>() != null) return;

            // Only damage Enemy-tagged targets.
            if (!other.CompareTag("Enemy")) return;

            Health targetHealth = other.GetComponent<Health>();
            if (targetHealth != null) targetHealth.TakeDamage(_damage);

            Despawn();
        }

        private void Despawn()
        {
            if (_isDespawned) return;
            _isDespawned = true;

            if (PoolManager.Instance == null) return;
            _rb.linearVelocity = Vector2.zero;
            PoolManager.Instance.Release(gameObject);
        }
    }
}
