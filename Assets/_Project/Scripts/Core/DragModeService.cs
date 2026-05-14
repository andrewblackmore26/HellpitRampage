using UnityEngine;
using UnityEngine.InputSystem;

namespace HellpitRampage.Core
{
    /// <summary>
    /// WS-012.5: scene-scoped service that tracks the current drag mode (Items vs Bags).
    /// In Items mode, only items respond to drag; bags grey out. Vice versa for Bags mode.
    /// Tab key toggles. Refuses toggle during an active drag so the player can't accidentally
    /// swap mode mid-action. Resets to Items on ShopPhaseStartedEvent.
    /// </summary>
    public class DragModeService : MonoBehaviour
    {
        public static DragModeService Current { get; private set; }
        public DragMode CurrentMode { get; private set; } = DragMode.Items;

        private bool _dragInProgress;

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void OnEnable()
        {
            // L-007: defensive re-assign of the static accessor in case a hot-reload skipped Awake.
            Current = this;

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<ShopPhaseStartedEvent>(HandleShopStarted);
                EventBus.Instance.Subscribe<ItemDragBeganEvent>(HandleItemDragBegan);
                EventBus.Instance.Subscribe<ItemDragEndedEvent>(HandleItemDragEnded);
                EventBus.Instance.Subscribe<BagDragBeganEvent>(HandleBagDragBegan);
                EventBus.Instance.Subscribe<BagDragEndedEvent>(HandleBagDragEnded);
            }
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<ShopPhaseStartedEvent>(HandleShopStarted);
                EventBus.Instance.Unsubscribe<ItemDragBeganEvent>(HandleItemDragBegan);
                EventBus.Instance.Unsubscribe<ItemDragEndedEvent>(HandleItemDragEnded);
                EventBus.Instance.Unsubscribe<BagDragBeganEvent>(HandleBagDragBegan);
                EventBus.Instance.Unsubscribe<BagDragEndedEvent>(HandleBagDragEnded);
            }
        }

        private void HandleShopStarted(ShopPhaseStartedEvent _) => SetMode(DragMode.Items);
        private void HandleItemDragBegan(ItemDragBeganEvent _) => _dragInProgress = true;
        private void HandleItemDragEnded(ItemDragEndedEvent _) => _dragInProgress = false;
        private void HandleBagDragBegan(BagDragBeganEvent _) => _dragInProgress = true;
        private void HandleBagDragEnded(BagDragEndedEvent _) => _dragInProgress = false;

        public void Toggle()
        {
            if (_dragInProgress) return;
            SetMode(CurrentMode == DragMode.Items ? DragMode.Bags : DragMode.Items);
        }

        public void SetMode(DragMode mode)
        {
            if (CurrentMode == mode) return;
            CurrentMode = mode;
            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new DragModeChangedEvent { NewMode = mode });
        }

        private void Update()
        {
            // L-008: new Input System is canon for this project; never use legacy Input.*.
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                Toggle();
        }

        // Test-only injection point so EditMode tests can simulate the "drag in progress" guard
        // without going through the full EventBus + DragHandler dance.
        internal void SetDragInProgressForTesting(bool value) => _dragInProgress = value;
    }
}
