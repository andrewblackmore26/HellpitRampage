using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HellpitRampage.UI
{
    /// <summary>
    /// Right-click toggle for the lock state of a grid item or bag. Attached alongside
    /// DragHandler on each rendered item/bag visual by InventoryGridView. Ignored mid-drag
    /// so a player flicking the mouse during a left-drag can't accidentally lock the moving piece.
    /// </summary>
    public class LockToggleHandler : MonoBehaviour, IPointerClickHandler
    {
        public enum TargetKind { Item, Bag }

        public TargetKind Kind { get; set; }
        public ItemInstance Item { get; set; }
        public BagInstance Bag { get; set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;
            // EventSystem flags this true while a drag is in progress (between OnBeginDrag and OnEndDrag).
            // Skip toggles in that window so a mid-drag click doesn't lock the moving piece.
            if (eventData.dragging) return;
            if (InventoryService.Instance == null) return;

            if (Kind == TargetKind.Item && Item != null)
                InventoryService.Instance.ToggleItemLock(Item);
            else if (Kind == TargetKind.Bag && Bag != null)
                InventoryService.Instance.ToggleBagLock(Bag);
        }
    }
}
