using UnityEngine;

namespace HellpitRampage.Inventory
{
    /// <summary>
    /// Places the hero's starting bag + item at the beginning of a run. Replaces WS-008's
    /// InventoryTestSeeder. Hardcoded for now; the hero system (WS-018+) will replace this with
    /// per-hero loadout data.
    /// </summary>
    public class HeroStartingLoadout : MonoBehaviour
    {
        [SerializeField] private BagData _startingBag;
        [SerializeField] private Vector2Int _bagOrigin = new(1, 3);
        [SerializeField] private ItemData _startingItem;
        [SerializeField] private Vector2Int _itemOrigin = new(2, 4);

        private void Start()
        {
            if (InventoryService.Instance == null)
            {
                Debug.LogError("HeroStartingLoadout: InventoryService.Instance is null.");
                return;
            }

            InventoryService.Instance.Grid.Clear();
            if (_startingBag != null) InventoryService.Instance.PlaceBag(_startingBag, _bagOrigin);
            if (_startingItem != null) InventoryService.Instance.PlaceItem(_startingItem, _itemOrigin);
        }
    }
}
