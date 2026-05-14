using HellpitRampage.Combat;
using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// Unit tests for <see cref="Health"/>. Covers the WS-006 guards: zero-damage no-op,
    /// double-death no-republish, pool-reuse reset via Initialize, and OnHealthChanged emission.
    /// EventBus is required because Health.HandleDeath publishes through it on death.
    /// </summary>
    public class HealthTests
    {
        private GameObject _eventBusGO;
        private GameObject _healthGO;
        private Health _health;

        [SetUp]
        public void SetUp()
        {
            // EventBus is required because Health.HandleDeath publishes EnemyDiedEvent / PlayerDiedEvent.
            _eventBusGO = new GameObject("EventBusTestHost");
            _eventBusGO.AddComponent<EventBus>();

            _healthGO = new GameObject("HealthTestHost");
            _health = _healthGO.AddComponent<Health>();
            // OnEnable already fired (active GameObject + AddComponent). _startingHP defaults to 100.
            // Tests that need a specific MaxHP call Initialize(...) explicitly.
        }

        [TearDown]
        public void TearDown()
        {
            if (_healthGO != null) Object.DestroyImmediate(_healthGO);
            if (_eventBusGO != null) Object.DestroyImmediate(_eventBusGO);
            _healthGO = null;
            _health = null;
            _eventBusGO = null;
        }

        [Test]
        public void TakeDamage_ReducesCurrentHP()
        {
            _health.Initialize(10f);
            _health.TakeDamage(3f);

            Assert.AreEqual(7f, _health.CurrentHP, 0.0001f);
            Assert.IsFalse(_health.IsDead);
        }

        [Test]
        public void TakeDamage_ClampsAtZero()
        {
            _health.Initialize(5f);
            _health.TakeDamage(100f);

            Assert.AreEqual(0f, _health.CurrentHP, 0.0001f, "HP must clamp at zero, never negative.");
            Assert.IsTrue(_health.IsDead, "Health should mark IsDead once HP reaches zero.");
        }

        [Test]
        public void TakeDamage_Zero_DoesNotPublishDeathOrChangeHP()
        {
            _health.Initialize(10f);

            int deathCount = 0;
            System.Action<EnemyDiedEvent> handler = _ => deathCount++;
            EventBus.Instance.Subscribe(handler);

            _health.TakeDamage(0f);

            Assert.AreEqual(10f, _health.CurrentHP, 0.0001f, "Zero damage must not change HP.");
            Assert.IsFalse(_health.IsDead, "Zero damage must not flip IsDead.");
            Assert.AreEqual(0, deathCount, "Zero damage must not publish a death event.");

            EventBus.Instance.Unsubscribe(handler);
        }

        [Test]
        public void TakeDamage_AfterDeath_DoesNotRePublish()
        {
            _health.Initialize(1f);

            int deathCount = 0;
            System.Action<EnemyDiedEvent> handler = _ => deathCount++;
            EventBus.Instance.Subscribe(handler);

            _health.TakeDamage(5f); // dies, publishes once
            _health.TakeDamage(5f); // already dead, must be a no-op

            Assert.AreEqual(1, deathCount, "EnemyDiedEvent must publish exactly once across multiple post-death damage calls.");

            EventBus.Instance.Unsubscribe(handler);
        }

        [Test]
        public void Initialize_ResetsHP_AndClearsDeadFlag()
        {
            _health.Initialize(5f);
            _health.TakeDamage(10f); // kills

            Assert.IsTrue(_health.IsDead, "Sanity: should be dead before re-Initialize.");

            _health.Initialize(20f);

            Assert.AreEqual(20f, _health.CurrentHP, 0.0001f, "Re-Initialize must restore HP to max.");
            Assert.AreEqual(20f, _health.MaxHP, 0.0001f);
            Assert.IsFalse(_health.IsDead, "Re-Initialize must clear the dead flag (pool-reuse contract).");
        }

        [Test]
        public void OnHealthChanged_FiredOnEachDamage()
        {
            _health.Initialize(10f);

            int fired = 0;
            UnityEngine.Events.UnityAction<float, float> handler = (cur, max) => fired++;
            _health.OnHealthChanged.AddListener(handler);

            _health.TakeDamage(3f);
            _health.TakeDamage(3f);

            Assert.GreaterOrEqual(fired, 2, "OnHealthChanged must fire at least once per non-zero damage application.");

            _health.OnHealthChanged.RemoveListener(handler);
        }
    }
}
