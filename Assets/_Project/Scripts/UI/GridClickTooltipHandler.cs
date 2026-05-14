using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.1: attached to owned grid items and bags. Left-click without drag opens the
    /// detail tooltip popup for the represented item or bag. Replaces WS-012's
    /// LockToggleHandler — locking now lives on the detail tooltip's action icon.
    /// </summary>
    public class GridClickTooltipHandler : MonoBehaviour, IPointerClickHandler
    {
        public enum TargetKind { Item, Bag }

        public TargetKind Kind { get; set; }
        public ItemInstance Item { get; set; }
        public BagInstance Bag { get; set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // EventSystem flags this true while a drag is in progress; suppress so a drag-end click
            // doesn't immediately re-open a tooltip on the just-dropped target.
            if (eventData.dragging) return;
            if (DetailTooltipController.Current == null) return;

            if (Kind == TargetKind.Item && Item != null)
                DetailTooltipController.Current.ShowForItem(Item);
            else if (Kind == TargetKind.Bag && Bag != null)
                DetailTooltipController.Current.ShowForBag(Bag);
        }
    }
}
