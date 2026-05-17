using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Inventory
{
    /// <summary>
    /// Seeds the hero's starting bag + item at the beginning of a run. WS-015: persistent
    /// (lives under Managers in Boot) and driven by <see cref="RunStartedEvent"/> — which
    /// fires exactly once per fresh run (from <c>RunManager.StartNewRun</c>) and never on a
    /// resume (<c>RunRestoreController</c> owns the inventory then). Hardcoded for now; the
    /// hero system (WS-018+) will replace this with per-hero loadout data.
    /// </summary>
    public class HeroStartingLoadout : MonoBehaviour
    {
        [SerializeField] private BagData _startingBag;
        [SerializeField] private Vector2Int _bagOrigin = new(1, 3);
        [SerializeField] private ItemData _startingItem;
        [SerializeField] private Vector2Int _itemOrigin = new(2, 4);

        private void OnEnable()
        {
            EventBus.Instance?.Subscribe<RunStartedEvent>(HandleRunStarted);
        }

        private void OnDisable()
        {
            EventBus.Instance?.Unsubscribe<RunStartedEvent>(HandleRunStarted);
        }

        private void HandleRunStarted(RunStartedEvent _)
        {
            if (InventoryService.Instance == null)
            {
                Debug.LogError("HeroStartingLoadout: InventoryService.Instance is null.");
                return;
            }

            // Fresh run — wipe any prior inventory + ground state, then place starting gear.
            InventoryService.Instance.Grid.Clear();
            InventoryService.Instance.ClearGroundItems();
            if (_startingBag != null) InventoryService.Instance.PlaceBag(_startingBag, _bagOrigin);
            if (_startingItem != null) InventoryService.Instance.PlaceItem(_startingItem, _itemOrigin);
        }
    }
}
