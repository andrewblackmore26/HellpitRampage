using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.3: attached to anything that should respond to hover-preview and click-to-pin
    /// tooltips — shop slots, grid items, grid bags, ground items. Replaces both
    /// <c>TooltipTarget</c> (hover-only) and <c>GridClickTooltipHandler</c> (click-only).
    /// The renamed .cs.meta preserves the scene-baked GUID so the 5 shop slot GameObjects
    /// that previously held TooltipTarget resolve to this component with their <see cref="Slot"/>
    /// field intact; non-shop sites populate <see cref="Item"/>/<see cref="Bag"/> at runtime.
    /// </summary>
    public class InspectableItem : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public enum Kind { ShopItem, OwnedItem, OwnedBag }

        // Set explicitly by runtime callers (InventoryGridView, GroundManager).
        public Kind ItemKind { get; set; } = Kind.OwnedItem;
        public ItemData Data { get; set; }
        public ItemInstance Item { get; set; }
        public BagInstance Bag { get; set; }

        // Scene-baked on shop slots; when non-null we read CurrentOffer at hover/click time
        // so the InspectableItem always reflects the live offer (rerolls, sold, etc.).
        public ShopSlot Slot;

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Suppress hover during any drag (grid, ground, OR shop drag — shop drags don't
            // publish ItemDragBeganEvent so the controller's _dragInProgress flag won't catch
            // them. EventSystem sets eventData.dragging on every PointerEnter while a drag
            // is in progress, regardless of source — single check covers all drag types).
            if (eventData.dragging) return;
            var ctrl = TooltipController.Current;
            if (ctrl == null) return;
            DispatchShow(ctrl, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var ctrl = TooltipController.Current;
            if (ctrl == null) return;
            ctrl.HideIfNotPinned(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // EventSystem flags this while a drag is in progress; suppress so a drag-end click
            // doesn't immediately pin the just-dropped target.
            if (eventData.dragging) return;
            var ctrl = TooltipController.Current;
            if (ctrl == null) return;
            DispatchPin(ctrl, eventData.position);
        }

        private void DispatchShow(TooltipController ctrl, Vector2 screenPos)
        {
            if (Slot != null && Slot.CurrentOffer != null)
            {
                if (Slot.CurrentOffer is ItemData itemFromSlot) ctrl.ShowHovering(itemFromSlot, screenPos, this);
                else if (Slot.CurrentOffer is BagData bagFromSlot) ctrl.ShowHovering(bagFromSlot, screenPos, this);
                return;
            }
            switch (ItemKind)
            {
                case Kind.ShopItem:
                    if (Data != null) ctrl.ShowHovering(Data, screenPos, this);
                    break;
                case Kind.OwnedItem:
                    if (Item != null) ctrl.ShowHovering(Item, screenPos, this);
                    break;
                case Kind.OwnedBag:
                    if (Bag != null) ctrl.ShowHovering(Bag, screenPos, this);
                    break;
            }
        }

        private void DispatchPin(TooltipController ctrl, Vector2 screenPos)
        {
            if (Slot != null && Slot.CurrentOffer != null)
            {
                if (Slot.CurrentOffer is ItemData itemFromSlot) ctrl.Pin(itemFromSlot, screenPos);
                else if (Slot.CurrentOffer is BagData bagFromSlot) ctrl.Pin(bagFromSlot, screenPos);
                return;
            }
            switch (ItemKind)
            {
                case Kind.ShopItem:
                    if (Data != null) ctrl.Pin(Data, screenPos);
                    break;
                case Kind.OwnedItem:
                    if (Item != null) ctrl.Pin(Item, screenPos);
                    break;
                case Kind.OwnedBag:
                    if (Bag != null) ctrl.Pin(Bag, screenPos);
                    break;
            }
        }
    }
}
