using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HellpitRampage.UI
{
    public class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ItemData Item;
        public BagData Bag;
        public ShopSlot Slot;
        public ItemInstance ItemInstance;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Tooltip.Instance == null) return;
            TooltipContent content = BuildContent();
            if (string.IsNullOrEmpty(content.Title)) return;
            Tooltip.Instance.RequestShow(content);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Tooltip.Instance != null) Tooltip.Instance.Hide();
        }

        private TooltipContent BuildContent()
        {
            if (Slot != null && Slot.CurrentOffer != null)
            {
                if (Slot.CurrentOffer is ItemData itemFromSlot) return TooltipContent.FromItem(itemFromSlot);
                if (Slot.CurrentOffer is BagData bagFromSlot) return TooltipContent.FromBag(bagFromSlot);
            }
            if (ItemInstance != null && InventoryService.Instance != null)
                return TooltipContent.FromItemInstance(ItemInstance, InventoryService.Instance.Grid);
            if (Item != null) return TooltipContent.FromItem(Item);
            if (Bag != null) return TooltipContent.FromBag(Bag);
            return default;
        }
    }
}
