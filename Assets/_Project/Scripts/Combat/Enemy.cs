using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Enemy : MonoBehaviour
    {
        private const float CONTACT_DAMAGE_COOLDOWN = 0.5f;

        private EnemyData _data;
        private Rigidbody2D _rb;
        private Transform _playerTransform;
        private float _contactDamageCooldown;

        // WS-014.B: prefab-default visual state, captured once so a per-spawn override
        // (the round-30 boss scales/tints this prefab) never leaks via the shared pool.
        private SpriteRenderer _spriteRenderer;
        private Vector3 _defaultScale;
        private Color _defaultColor;

        public EnemyData Data => _data;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _defaultScale = transform.localScale;
            if (_spriteRenderer != null) _defaultColor = _spriteRenderer.color;
        }

        private void OnEnable()
        {
            // Reset per-spawn state. OnEnable fires when the pool reactivates the instance.
            _contactDamageCooldown = 0f;
        }

        /// <summary>Called by the spawner immediately after pulling this instance from the pool.</summary>
        public void Initialize(EnemyData data, Transform playerTransform)
        {
            _data = data;
            _playerTransform = playerTransform;
            // Reset velocity from any previous pool usage. Pool deactivation does NOT clear Rigidbody state.
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            // WS-014.B: restore the prefab's default scale/tint. The round-30 boss reuses
            // this prefab with a scale/tint override; resetting here keeps that override
            // from leaking to a later spawn (e.g. a fresh run) via the shared pool.
            transform.localScale = _defaultScale;
            if (_spriteRenderer != null) _spriteRenderer.color = _defaultColor;

            // Seed Health from data, overriding the prefab's default StartingHP.
            Health health = GetComponent<Health>();
            if (health != null) health.Initialize(_data.MaxHP);
        }

        private void Update()
        {
            // TODO(WS-pause-or-slowmo): migrate to TimeManager.CombatTime.
            if (_contactDamageCooldown > 0f) _contactDamageCooldown -= Time.deltaTime;
        }

        private void FixedUpdate()
        {
            if (_playerTransform == null || _data == null) return;

            Vector2 toPlayer = (Vector2)_playerTransform.position - _rb.position;
            Vector2 direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
            _rb.linearVelocity = direction * _data.MoveSpeed;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_contactDamageCooldown > 0f) return;
            if (_data == null) return;

            Health playerHealth = other.GetComponent<Health>();
            if (playerHealth == null) return;

            playerHealth.TakeDamage(_data.ContactDamage);
            _contactDamageCooldown = CONTACT_DAMAGE_COOLDOWN;
        }
    }
}
