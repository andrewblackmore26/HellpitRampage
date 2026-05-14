using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// Dynamic sell affordance covering the shop overlay region during an active drag.
    /// Activates on <see cref="ItemDragBeganEvent"/> for items and (WS-012.5) on
    /// <see cref="BagDragBeganEvent"/> for bags. Text shows the sale price or
    /// "LOCKED — cannot sell" in red for locked targets. Item drop: remove + grant gold.
    /// Bag drop: spill contents to ground, grant gold, remove the empty bag.
    /// </summary>
    public class SellModal : MonoBehaviour, IDropHandler
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _label;
        [SerializeField] private Color _normalTextColor = Color.white;
        [SerializeField] private Color _lockedTextColor = new Color(0.9f, 0.3f, 0.3f, 1f);

        private ItemInstance _currentDragItem;
        private BagInstance _currentDragBag;
        private int _currentSalePrice;
        private bool _currentLocked;
        private bool _currentIsBag;

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (EventBus.Instance != null)
            {
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
                EventBus.Instance.Unsubscribe<ItemDragBeganEvent>(HandleItemDragBegan);
                EventBus.Instance.Unsubscribe<ItemDragEndedEvent>(HandleItemDragEnded);
                EventBus.Instance.Unsubscribe<BagDragBeganEvent>(HandleBagDragBegan);
                EventBus.Instance.Unsubscribe<BagDragEndedEvent>(HandleBagDragEnded);
            }
        }

        private void HandleItemDragBegan(ItemDragBeganEvent e)
        {
            if (e.Item == null || e.Item.Data == null) return;
            _currentDragItem = e.Item;
            _currentDragBag = null;
            _currentIsBag = false;
            _currentLocked = e.Item.IsLocked;
            _currentSalePrice = Mathf.CeilToInt(e.Item.Data.EffectivePrice * 0.5f);
            ShowPanel(_currentLocked ? "LOCKED — cannot sell" : $"Drag here to sell for {_currentSalePrice}g");
        }

        private void HandleBagDragBegan(BagDragBeganEvent e)
        {
            if (e.Bag == null || e.Bag.Data == null) return;
            _currentDragBag = e.Bag;
            _currentDragItem = null;
            _currentIsBag = true;
            _currentLocked = e.Bag.IsLocked;
            _currentSalePrice = Mathf.CeilToInt(e.Bag.Data.EffectivePrice * 0.5f);
            ShowPanel(_currentLocked
                ? "LOCKED — cannot sell"
                : $"Drag here to sell for {_currentSalePrice}g (spills contents)");
        }

        private void HandleItemDragEnded(ItemDragEndedEvent _) => HidePanel();
        private void HandleBagDragEnded(BagDragEndedEvent _) => HidePanel();

        private void ShowPanel(string text)
        {
            if (_panel != null) _panel.SetActive(true);
            if (_label != null)
            {
                _label.text = text;
                _label.color = _currentLocked ? _lockedTextColor : _normalTextColor;
            }
            // Ensure modal draws above shop slots so its drop handler wins the raycast.
            transform.SetAsLastSibling();
        }

        private void HidePanel()
        {
            if (_panel != null) _panel.SetActive(false);
            _currentDragItem = null;
            _currentDragBag = null;
            _currentIsBag = false;
            _currentSalePrice = 0;
            _currentLocked = false;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_currentLocked) return;
            if (InventoryService.Instance == null || RunManager.Instance == null) return;

            if (_currentIsBag) { SellBag(_currentDragBag, _currentSalePrice); return; }
            if (_currentDragItem != null) SellItem(_currentDragItem, _currentSalePrice);
        }

        private void SellItem(ItemInstance item, int price)
        {
            // Order: remove first, then grant gold. If RemoveItem fails, no gold is paid.
            if (!InventoryService.Instance.RemoveItem(item)) return;
            RunManager.Instance.AddGold(price);
        }

        private void SellBag(BagInstance bag, int price)
        {
            if (bag == null) return;
            if (GroundManager.Current == null)
            {
                // Defensive: a ground manager must be in scene before a bag can be sold.
                // Without it, contents can't spill — fall back to canceling the sale.
                Debug.LogWarning("[SellModal] Bag sale aborted: GroundManager.Current is null. Wire a GroundArea/GroundManager in the scene.");
                return;
            }

            // Capture in-bag items BEFORE any state mutation, so the order
            // "gold → spill → remove bag" can run cleanly.
            var items = InventoryService.Instance.GetItemsInBag(bag);
            int spillCount = items.Count;

            // Gold first — visible to the player immediately.
            RunManager.Instance.AddGold(price);

            // Spill each item to the ground with horizontal scatter so they don't stack perfectly.
            RectTransform groundRT = GroundManager.Current.GroundAreaRT;
            if (groundRT != null)
            {
                float halfW = groundRT.rect.width * 0.5f;
                float halfH = groundRT.rect.height * 0.5f;
                float spawnY = halfH - 20f;
                foreach (var item in items)
                {
                    float spawnX = Random.Range(-halfW * 0.7f, halfW * 0.7f);
                    float vx = Random.Range(-150f, 150f);
                    GroundManager.Current.AddItem(item.Data, item.Rotation, item.IsLocked,
                        new Vector2(spawnX, spawnY), new Vector2(vx, -200f));
                    InventoryService.Instance.RemoveItemSilent(item);
                }
            }

            // Bag last, without cascade — by now no items reference it.
            InventoryService.Instance.RemoveBagWithoutCascade(bag);

            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new BagSoldEvent { BagData = bag.Data, ItemCountSpilled = spillCount });
        }
    }
}
