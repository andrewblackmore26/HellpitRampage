using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.5: drag handler attached to each spawned ground item visual. Drag semantics:
    /// - Drop on sell modal → sell (SellModal.OnDrop handles it; we just notice the item is gone).
    /// - Drop on a valid grid cell → move to grid; remove from ground.
    /// - Drop in backpack X-range, invalid grid cell → return to ground at drop position with fling velocity.
    /// - Drop outside backpack X-range → snap back to drag origin.
    /// Publishes <see cref="ItemDragBeganEvent"/> / <see cref="ItemDragEndedEvent"/> so SellModal
    /// reacts uniformly with grid-item drags.
    /// </summary>
    public class GroundDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const int CELL_SIZE_PX = 56;

        private GroundItem _groundItem;
        private RectTransform _rt;
        private GroundItemPhysics _physics;
        private CanvasGroup _canvasGroup;
        private Vector2 _dragOrigin;
        private bool _dragging;

        public void Initialize(GroundItem groundItem)
        {
            _groundItem = groundItem;
            _rt = transform as RectTransform;
            _physics = GetComponent<GroundItemPhysics>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_groundItem == null || _rt == null) return;
            if (DragModeService.Current != null && DragModeService.Current.CurrentMode != DragMode.Items) return;

            _dragging = true;
            _dragOrigin = _rt.anchoredPosition;

            if (_physics != null) _physics.IsHeld = true;
            _canvasGroup.alpha = 0.7f;
            _canvasGroup.blocksRaycasts = false;
            transform.SetAsLastSibling();

            if (EventBus.Instance != null && _groundItem.Instance != null)
                EventBus.Instance.Publish(new ItemDragBeganEvent { Item = _groundItem.Instance });
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _rt == null) return;
            // Follow the cursor in ground-area-local space (the parent RectTransform).
            RectTransform parent = _rt.parent as RectTransform;
            if (parent == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, null, out Vector2 local);
            _rt.anchoredPosition = local;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;

            // Defensive: if SellModal.OnDrop already removed this ground item, we have nothing
            // to revert. Detect by checking whether the manager still owns us.
            bool soldOut = _groundItem != null && GroundManager.Current != null
                           && !GroundManager.Current.ContainsItem(_groundItem);
            if (soldOut)
            {
                PublishEnded(wasCancelled: false);
                return;
            }

            if (_physics != null) _physics.IsHeld = false;

            bool inRange = DropZoneClassifier.IsWithinBackpackXRange(eventData.position);
            if (inRange)
            {
                if (TryPlaceInGrid(eventData.position))
                {
                    if (GroundManager.Current != null) GroundManager.Current.RemoveItem(_groundItem);
                    PublishEnded(wasCancelled: false);
                    return;
                }

                // Re-throw onto the ground with fling velocity. The item is still at the
                // pointer's local position from OnDrag; just hand off to physics.
                if (_physics != null)
                {
                    _physics.Velocity = ComputeFlingVelocity(eventData);
                    _physics.Wake();
                }
                PublishEnded(wasCancelled: false);
                return;
            }

            // Outside backpack column — snap back to the drag origin.
            if (_rt != null) _rt.anchoredPosition = _dragOrigin;
            if (_physics != null)
            {
                _physics.Velocity = Vector2.zero;
                _physics.Wake();
            }
            PublishEnded(wasCancelled: true);
        }

        private void PublishEnded(bool wasCancelled)
        {
            if (EventBus.Instance == null || _groundItem == null || _groundItem.Instance == null) return;
            EventBus.Instance.Publish(new ItemDragEndedEvent { Item = _groundItem.Instance, WasCancelled = wasCancelled });
        }

        private bool TryPlaceInGrid(Vector2 screenPos)
        {
            if (_groundItem == null || _groundItem.Data == null) return false;
            if (InventoryService.Instance == null) return false;
            RectTransform grid = BackpackBoundsProvider.Current;
            if (grid == null) return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(grid, screenPos, null, out Vector2 local);
            Vector2Int cell = new Vector2Int(
                Mathf.FloorToInt(local.x / CELL_SIZE_PX),
                Mathf.FloorToInt(local.y / CELL_SIZE_PX));

            if (!InventoryService.Instance.Grid.CanPlaceItem(_groundItem.Data, cell, _groundItem.Rotation)) return false;

            var placed = InventoryService.Instance.PlaceItem(_groundItem.Data, cell, _groundItem.Rotation);
            if (placed == null) return false;
            placed.IsLocked = _groundItem.IsLocked;
            return true;
        }

        private Vector2 ComputeFlingVelocity(PointerEventData eventData)
        {
            // Convert per-frame delta to per-second velocity. eventData.delta is screen-space pixels
            // since last drag dispatch; multiply by 60 (~target fps) for a reasonable fling.
            Vector2 vel = eventData.delta * 60f;
            return Vector2.ClampMagnitude(vel, 1500f);
        }
    }
}
