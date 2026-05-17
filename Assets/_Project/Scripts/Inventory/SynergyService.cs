using System.Collections.Generic;
using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Inventory
{
    /// <summary>
    /// Scene-scoped service. Owns the cached <see cref="SynergyResolver.Resolution"/>;
    /// rebuilds on every inventory event and publishes <see cref="SynergyChangedEvent"/>
    /// so visual subscribers (StarIndicatorOverlay) can repaint without re-querying state.
    /// No registry dependency in WS-011.5+ — conditional effects live on ItemData itself.
    /// </summary>
    public class SynergyService : MonoBehaviour
    {
        public static SynergyService Instance { get; private set; }

        private SynergyResolver.Resolution _current = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // WS-015: persistent across the Combat<->Shop scene transitions so combat code
            // can query the cached synergy resolution while the Shop scene is unloaded.
            // Guarded — DontDestroyOnLoad is play-mode-only and throws in EditMode tests.
            if (Application.isPlaying) DontDestroyOnLoad(transform.root.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            // L-007: domain reload during Play skips Awake; re-assert Instance idempotently.
            if (Instance == null) Instance = this;

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<ItemPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemMovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagMovedEvent>(HandleAnyChange);
            }

            // Defensive recompute so a domain reload mid-Play repopulates the cache from existing grid state.
            Recompute();
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Unsubscribe<ItemPlacedEvent>(HandleAnyChange);
            EventBus.Instance.Unsubscribe<ItemRemovedEvent>(HandleAnyChange);
            EventBus.Instance.Unsubscribe<ItemMovedEvent>(HandleAnyChange);
            EventBus.Instance.Unsubscribe<BagPlacedEvent>(HandleAnyChange);
            EventBus.Instance.Unsubscribe<BagRemovedEvent>(HandleAnyChange);
            EventBus.Instance.Unsubscribe<BagMovedEvent>(HandleAnyChange);
        }

        private void Start() => Recompute();

        private void HandleAnyChange<T>(T _) where T : IGameEvent => Recompute();

        private void Recompute()
        {
            _current = InventoryService.Instance != null
                ? SynergyResolver.Resolve(InventoryService.Instance.Grid)
                : new SynergyResolver.Resolution();

            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new SynergyChangedEvent());
        }

        public ItemStatModifiers GetModifiers(int instanceID) =>
            _current.Modifiers.TryGetValue(instanceID, out var m) ? m : ItemStatModifiers.Identity;

        public IReadOnlyList<ConditionalEffect> GetBehaviorsForRecipient(int recipientID) =>
            _current.BehaviorsByRecipient.TryGetValue(recipientID, out var list) ? list : System.Array.Empty<ConditionalEffect>();

        public bool IsStarActive(int starredID, Vector2Int cell, EdgeDirection dir) =>
            _current.ActiveStars.Contains((starredID, cell, dir));
    }
}
