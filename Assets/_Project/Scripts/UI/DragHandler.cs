using System.Collections.Generic;
using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HellpitRampage.UI
{
    public class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const int CELL_SIZE_PX = 56;

        public enum DraggableKind { Bag, Item }

        public DraggableKind Kind { get; set; }
        public BagInstance Bag { get; set; }
        public ItemInstance Item { get; set; }
        public RectTransform GridParent { get; set; }
        public InventoryGridView View { get; set; }

        private RectTransform _rt;
        private CanvasGroup _canvasGroup;
        private Vector2 _originalAnchoredPos;
        private Rotation _originalRotation;
        private Vector2 _grabOffset;
        private bool _dragging;
        private Vector2Int _currentSnappedOrigin;
        private Rotation _currentRotation;
        // WS-012: tracks whether the current drag committed (move/sell) or was cancelled.
        // Flipped to true in TryCommitDrop success paths AND by sell-modal removal detection.
        // Published on the *DragEndedEvent so subscribers (SellModal, future spillover) know intent.
        private bool _dropCommitted;

        private void Awake()
        {
            _rt = transform as RectTransform;
            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_rt == null) return;
            if (Tooltip.Instance != null) Tooltip.Instance.Hide();
            _dragging = true;
            _dropCommitted = false;
            _originalAnchoredPos = _rt.anchoredPosition;
            _originalRotation = Kind == DraggableKind.Item && Item != null ? Item.Rotation : Rotation.Deg0;
            _currentRotation = _originalRotation;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(GridParent, eventData.position, null, out Vector2 local);
            _grabOffset = local - _rt.anchoredPosition;

            _canvasGroup.alpha = 0.7f;
            _canvasGroup.blocksRaycasts = false;
            transform.SetAsLastSibling();

            _currentSnappedOrigin = CursorToSnappedOrigin(eventData.position);
            UpdateValidationOverlay();

            // WS-012: publish AFTER ghost setup so SellModal activating immediately sees a valid drag.
            // Item drags vs bag drags use distinct events — SellModal only consumes ItemDragBeganEvent.
            if (EventBus.Instance != null)
            {
                if (Kind == DraggableKind.Item && Item != null)
                    EventBus.Instance.Publish(new ItemDragBeganEvent { Item = Item });
                else if (Kind == DraggableKind.Bag && Bag != null)
                    EventBus.Instance.Publish(new BagDragBeganEvent { Bag = Bag });
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _rt == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(GridParent, eventData.position, null, out Vector2 local);
            _rt.anchoredPosition = local - _grabOffset;

            _currentSnappedOrigin = CursorToSnappedOrigin(eventData.position);
            UpdateValidationOverlay();
        }

        private void Update()
        {
            if (!_dragging) return;

            if (Kind == DraggableKind.Item && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                _currentRotation = ShapeMath.Next(_currentRotation);
                UpdateValidationOverlay();
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelDrag();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;

            // WS-012: if SellModal.OnDrop already removed this item, skip revert / move attempt.
            // OnDrop fires BEFORE OnEndDrag, so the item may no longer be in the inventory.
            bool soldOut = Kind == DraggableKind.Item
                           && Item != null
                           && InventoryService.Instance != null
                           && !InventoryService.Instance.ContainsItem(Item);
            if (soldOut)
            {
                _dropCommitted = true;
            }
            else
            {
                bool committed = TryCommitDrop();
                if (committed) _dropCommitted = true;
                if (!committed) ReturnToOriginal();
            }

            _dragging = false;
            if (View != null) View.ResetCellHighlights();

            PublishDragEnded(wasCancelled: !_dropCommitted);
        }

        private void CancelDrag()
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            ReturnToOriginal();
            _dragging = false;
            if (View != null) View.ResetCellHighlights();

            // Cancel always counts as not-committed.
            PublishDragEnded(wasCancelled: true);
        }

        // WS-012: publish drag-ended event. Item path may also flow through here when the item
        // was sold during OnDrop (Item is still our cached reference even after removal from grid).
        private void PublishDragEnded(bool wasCancelled)
        {
            if (EventBus.Instance == null) return;
            if (Kind == DraggableKind.Item && Item != null)
                EventBus.Instance.Publish(new ItemDragEndedEvent { Item = Item, WasCancelled = wasCancelled });
            else if (Kind == DraggableKind.Bag && Bag != null)
                EventBus.Instance.Publish(new BagDragEndedEvent { Bag = Bag, WasCancelled = wasCancelled });
        }

        private void ReturnToOriginal()
        {
            if (_rt != null) _rt.anchoredPosition = _originalAnchoredPos;
            _currentRotation = _originalRotation;
        }

        private bool TryCommitDrop()
        {
            if (InventoryService.Instance == null) return false;

            if (Kind == DraggableKind.Bag)
            {
                if (Bag == null) return false;
                return InventoryService.Instance.MoveBagAndItems(Bag, _currentSnappedOrigin);
            }
            else
            {
                if (Item == null) return false;
                return InventoryService.Instance.MoveItem(Item, _currentSnappedOrigin, _currentRotation);
            }
        }

        private Vector2Int CursorToSnappedOrigin(Vector2 screenPos)
        {
            if (GridParent == null) return Vector2Int.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(GridParent, screenPos, null, out Vector2 local);
            int x = Mathf.FloorToInt(local.x / CELL_SIZE_PX);
            int y = Mathf.FloorToInt(local.y / CELL_SIZE_PX);
            return new Vector2Int(x, y);
        }

        private void UpdateValidationOverlay()
        {
            if (View == null || InventoryService.Instance == null) return;
            View.ResetCellHighlights();

            List<Vector2Int> effective = ComputeDraggedEffectiveCells();
            var absolute = new List<Vector2Int>(effective.Count);
            foreach (var c in effective) absolute.Add(_currentSnappedOrigin + c);

            bool valid = IsDropValid();
            if (valid) View.HighlightCellsValid(absolute);
            else View.HighlightCellsInvalid(absolute);
        }

        private List<Vector2Int> ComputeDraggedEffectiveCells()
        {
            if (Kind == DraggableKind.Bag)
            {
                if (Bag == null || Bag.Data == null || Bag.Data.Shape == null) return new List<Vector2Int>();
                return new List<Vector2Int>(Bag.Data.Shape.Cells);
            }
            else
            {
                if (Item == null || Item.Data == null || Item.Data.Shape == null) return new List<Vector2Int>();
                return ShapeMath.Rotate(Item.Data.Shape.Cells, _currentRotation);
            }
        }

        private bool IsDropValid()
        {
            if (InventoryService.Instance == null) return false;
            var grid = InventoryService.Instance.Grid;

            if (Kind == DraggableKind.Bag)
            {
                if (Bag == null || Bag.Data == null || Bag.Data.Shape == null) return false;

                foreach (var offset in Bag.Data.Shape.Cells)
                {
                    Vector2Int cell = _currentSnappedOrigin + offset;
                    if (!InventoryGrid.IsInBounds(cell)) return false;
                    var occupant = grid.GetBagAt(cell);
                    if (occupant != null && occupant != Bag) return false;
                }

                Vector2Int delta = _currentSnappedOrigin - Bag.Origin;
                foreach (var item in grid.Items)
                {
                    if (item.HostBag != Bag) continue;
                    Vector2Int proposedItemOrigin = item.Origin + delta;
                    foreach (var iOff in item.EffectiveCells())
                    {
                        Vector2Int ic = proposedItemOrigin + iOff;
                        if (!InventoryGrid.IsInBounds(ic)) return false;
                        bool insideNewBag = false;
                        foreach (var bOff in Bag.Data.Shape.Cells)
                        {
                            if (_currentSnappedOrigin + bOff == ic) { insideNewBag = true; break; }
                        }
                        if (!insideNewBag) return false;
                        var occ = grid.GetItemAt(ic);
                        if (occ != null && occ != item) return false;
                    }
                }
                return true;
            }
            else
            {
                if (Item == null) return false;
                return grid.CanPlaceItem(Item.Data, _currentSnappedOrigin, _currentRotation, ignore: Item);
            }
        }
    }
}
