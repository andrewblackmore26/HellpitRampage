using System.Collections.Generic;
using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;

namespace HellpitRampage.Combat
{
    public class PlayerWeapons : MonoBehaviour
    {
        [Tooltip("Fallback test loadout when InventoryService isn't available (PlayMode-only edge case).")]
        [SerializeField] private List<ItemData> _equippedItems = new();

        [Tooltip("Projectiles per weapon to prewarm on spawn/buy.")]
        [SerializeField] private int _prewarmPerWeapon = 30;

        // Primary source of truth: keyed by ItemInstance.InstanceID so duplicates each get their own cooldown.
        private readonly Dictionary<int, float> _cooldownByInstance = new();
        // Per-instance base-attack counter. Increments AFTER each successful FireAt. Behaviors check `count % TriggerCount`.
        private readonly Dictionary<int, int> _attackCountByInstance = new();
        // Fallback path: keyed by ItemData when running without an InventoryService (e.g., a standalone test scene).
        private readonly Dictionary<ItemData, float> _cooldownByData = new();

        private void OnEnable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<ItemPlacedEvent>(HandleItemPlaced);
                EventBus.Instance.Subscribe<ItemRemovedEvent>(HandleItemRemoved);
            }
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<ItemPlacedEvent>(HandleItemPlaced);
                EventBus.Instance.Unsubscribe<ItemRemovedEvent>(HandleItemRemoved);
            }
        }

        private void Start()
        {
            if (InventoryService.Instance != null)
            {
                foreach (var item in InventoryService.Instance.Grid.Items)
                {
                    if (item?.Data == null) continue;
                    _cooldownByInstance[item.InstanceID] = 0f;
                    _attackCountByInstance[item.InstanceID] = 0;
                    if (item.Data.ProjectilePrefab != null && PoolManager.Instance != null)
                        PoolManager.Instance.Prewarm(item.Data.ProjectilePrefab, _prewarmPerWeapon);
                }
                return;
            }

            // Fallback: no inventory service, drive from SerializeField list. Used by Play scenes that bypass Inventory.
            foreach (var item in _equippedItems)
            {
                if (item == null) continue;
                _cooldownByData[item] = 0f;
                if (item.ProjectilePrefab != null && PoolManager.Instance != null)
                    PoolManager.Instance.Prewarm(item.ProjectilePrefab, _prewarmPerWeapon);
            }
        }

        private void HandleItemPlaced(ItemPlacedEvent evt)
        {
            if (evt.Item == null) return;
            _cooldownByInstance[evt.Item.InstanceID] = 0f;
            _attackCountByInstance[evt.Item.InstanceID] = 0;
            if (evt.Item.Data?.ProjectilePrefab != null && PoolManager.Instance != null)
                PoolManager.Instance.Prewarm(evt.Item.Data.ProjectilePrefab, _prewarmPerWeapon);
        }

        private void HandleItemRemoved(ItemRemovedEvent evt)
        {
            if (evt.Item == null) return;
            _cooldownByInstance.Remove(evt.Item.InstanceID);
            _attackCountByInstance.Remove(evt.Item.InstanceID);
        }

        private void Update()
        {
            // TODO(WS-pause-or-slowmo): migrate to TimeManager.CombatTime.
            if (InventoryService.Instance != null)
            {
                foreach (var item in InventoryService.Instance.Grid.Items)
                {
                    if (item?.Data == null) continue;
                    if (item.Data.ProjectilePrefab == null) continue;

                    if (!_cooldownByInstance.TryGetValue(item.InstanceID, out float current))
                    {
                        current = 0f;
                        _cooldownByInstance[item.InstanceID] = 0f;
                    }
                    if (!_attackCountByInstance.ContainsKey(item.InstanceID))
                        _attackCountByInstance[item.InstanceID] = 0;

                    ItemStatModifiers mods = SynergyService.Instance != null
                        ? SynergyService.Instance.GetModifiers(item.InstanceID)
                        : ItemStatModifiers.Identity;

                    float effectiveCooldown = item.Data.Cooldown * mods.CooldownMultiplier;
                    float effectiveRange = item.Data.Range + mods.RangeBonus;

                    Enemy target = null;
                    if (current - Time.deltaTime <= 0f) target = FindNearestEnemyInRange(effectiveRange);

                    float next = WeaponMath.StepCooldown(current, Time.deltaTime, effectiveCooldown, target != null, out bool shouldFire);
                    if (shouldFire && target != null)
                    {
                        FireAt(item.Data, target, mods);
                        _attackCountByInstance[item.InstanceID]++;
                        TriggerInboundBehaviors(item, target, mods);
                    }
                    _cooldownByInstance[item.InstanceID] = next;
                }
                return;
            }

            // Fallback path: drive from _equippedItems when InventoryService is absent. No synergies/behaviors in this path.
            foreach (var item in _equippedItems)
            {
                if (item == null) continue;
                if (item.ProjectilePrefab == null) continue;

                float current = _cooldownByData.TryGetValue(item, out var c) ? c : 0f;

                Enemy target = null;
                if (current - Time.deltaTime <= 0f) target = FindNearestEnemyInRange(item.Range);

                float next = WeaponMath.StepCooldown(current, Time.deltaTime, item.Cooldown, target != null, out bool shouldFire);
                if (shouldFire && target != null) FireAt(item, target, ItemStatModifiers.Identity);
                _cooldownByData[item] = next;
            }
        }

        /// <summary>
        /// Under the v3 unified-target model, behaviors are keyed by RECIPIENT instance ID.
        /// The recipient may be the firing item itself (Target=Self on a starred item that fires)
        /// OR the firing item may be the named recipient of an external starred item's Target=Neighbor
        /// behavior. Either way, this loop is keyed by the firing item's InstanceID — which IS the
        /// recipient under both cases — so the lookup is correct without a separate code path.
        /// </summary>
        private void TriggerInboundBehaviors(ItemInstance item, Enemy target, ItemStatModifiers mods)
        {
            if (SynergyService.Instance == null) return;
            var behaviors = SynergyService.Instance.GetBehaviorsForRecipient(item.InstanceID);
            if (behaviors == null || behaviors.Count == 0) return;

            int currentCount = _attackCountByInstance[item.InstanceID];

            foreach (var ce in behaviors)
            {
                if (ce == null) continue;
                if (!BehaviorMath.ShouldTrigger(currentCount, ce.TriggerCount)) continue;

                switch (ce.BehaviorAction)
                {
                    case BehaviorAction.ExtraProjectile:
                        if (target != null && item.Data.ProjectilePrefab != null)
                            FireAt(item.Data, target, mods);
                        break;
                    case BehaviorAction.AoEPulse:
                        Debug.Log($"[WS-011.5 stub] AoEPulse from {item.Data.ItemName} (magnitude {ce.BehaviorMagnitude}) — not implemented.");
                        break;
                    case BehaviorAction.HealingBurst:
                        Debug.Log($"[WS-011.5 stub] HealingBurst from {item.Data.ItemName} (magnitude {ce.BehaviorMagnitude}) — not implemented.");
                        break;
                }
            }
        }

        private Enemy FindNearestEnemyInRange(float range)
        {
            // L-004: parameterless overload. The FindObjectsSortMode-taking overload is deprecated.
            Enemy[] all = Object.FindObjectsByType<Enemy>();
            if (all.Length == 0) return null;

            Vector2 origin = transform.position;
            float sqrRange = range * range;
            Enemy best = null;
            float bestSqr = float.MaxValue;

            foreach (var e in all)
            {
                if (e == null) continue;
                if (!e.gameObject.activeInHierarchy) continue;
                float sqr = ((Vector2)e.transform.position - origin).sqrMagnitude;
                if (sqr > sqrRange) continue;
                if (sqr < bestSqr) { best = e; bestSqr = sqr; }
            }

            return best;
        }

        private void FireAt(ItemData item, Enemy target, ItemStatModifiers mods)
        {
            if (PoolManager.Instance == null) return;

            GameObject instance = PoolManager.Instance.Get(item.ProjectilePrefab);
            if (instance == null) return;

            instance.transform.position = transform.position;
            Projectile p = instance.GetComponent<Projectile>();
            if (p == null) { PoolManager.Instance.Release(instance); return; }

            Vector2 dir = (Vector2)target.transform.position - (Vector2)transform.position;
            float effectiveDamage = item.Damage + mods.DamageBonus;
            p.Initialize(dir, item.ProjectileSpeed, item.ProjectileLifetime, effectiveDamage);
        }
    }
}
