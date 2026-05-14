using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// Dynamic sell affordance covering the shop overlay region during an active item drag.
    /// Activates on ItemDragBeganEvent with text "Drag here to sell for Xg" (or "LOCKED — cannot sell"
    /// in red when the dragged item is locked). Drops on the panel sell unlocked items at
    /// ceil(EffectivePrice/2). Bag drags never trigger this panel (bag-selling is WS-012.5).
    /// </summary>
    public class SellModal : MonoBehaviour, IDropHandler
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _label;
        [SerializeField] private Color _normalTextColor = Color.white;
        [SerializeField] private Color _lockedTextColor = new Color(0.9f, 0.3f, 0.3f, 1f);

        private ItemInstance _currentDragItem;
        private int _currentSalePrice;
        private bool _currentLocked;

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<ItemDragBeganEvent>(HandleDragBegan);
                EventBus.Instance.Subscribe<ItemDragEndedEvent>(HandleDragEnded);
            }
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<ItemDragBeganEvent>(HandleDragBegan);
                EventBus.Instance.Unsubscribe<ItemDragEndedEvent>(HandleDragEnded);
            }
        }

        private void HandleDragBegan(ItemDragBeganEvent e)
        {
            if (e.Item == null || e.Item.Data == null) return;
            _currentDragItem = e.Item;
            _currentLocked = e.Item.IsLocked;
            _currentSalePrice = Mathf.CeilToInt(e.Item.Data.EffectivePrice * 0.5f);

            if (_panel != null) _panel.SetActive(true);
            if (_label != null)
            {
                if (_currentLocked)
                {
                    _label.text = "LOCKED — cannot sell";
                    _label.color = _lockedTextColor;
                }
                else
                {
                    _label.text = $"Drag here to sell for {_currentSalePrice}g";
                    _label.color = _normalTextColor;
                }
            }

            // Ensure modal draws above shop slots so its drop handler wins the raycast.
            transform.SetAsLastSibling();
        }

        private void HandleDragEnded(ItemDragEndedEvent _)
        {
            if (_panel != null) _panel.SetActive(false);
            _currentDragItem = null;
            _currentSalePrice = 0;
            _currentLocked = false;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_currentDragItem == null) return;
            if (_currentLocked) return; // text already shows "LOCKED — cannot sell"; item returns via standard cancel.
            if (InventoryService.Instance == null) return;
            if (RunManager.Instance == null) return;

            ItemInstance item = _currentDragItem;
            int price = _currentSalePrice;

            // Order: remove first, then grant gold. If RemoveItem fails, no gold is paid.
            if (!InventoryService.Instance.RemoveItem(item)) return;
            RunManager.Instance.AddGold(price);

            // _currentDragItem is cleared in HandleDragEnded, which fires after this OnDrop.
        }
    }
}
