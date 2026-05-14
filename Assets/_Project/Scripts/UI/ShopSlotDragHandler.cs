using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    public class ShopSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const int CELL_SIZE_PX = 56;

        [SerializeField] private ShopSlot _slot;
        [SerializeField] private int _slotIndex;
        [SerializeField] private ShopController _shopController;
        [SerializeField] private InventoryGridView _gridView;
        [SerializeField] private RectTransform _gridParent;
        [SerializeField] private Image _ghostPrefab;

        private GameObject _ghostInstance;
        private RectTransform _ghostRT;
        private bool _dragging;
        private Vector2Int _snappedOrigin;
        private ScriptableObject _draggedOffer;
        private Rotation _currentRotation;

        // WS-012.1 fix-pass: scene-wide accessor for the active shop drag so observers
        // (e.g., StarIndicatorOverlay) can preview the dragged offer's effect on synergies
        // before the item is actually purchased and placed. Distinct from DragHandler.Active
        // since shop drags have no real ItemInstance yet — only a ScriptableObject offer.
        public static ShopSlotDragHandler Active { get; private set; }
        public ItemData CurrentItemData => _draggedOffer as ItemData;
        public Vector2Int CurrentSnappedOrigin => _snappedOrigin;
        public Rotation CurrentRotation => _currentRotation;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_slot == null || _slot.IsSold || _slot.CurrentOffer == null) return;
            if (!_slot.CanAfford) return;
            if (_ghostPrefab == null) return;

            _dragging = true;
            _draggedOffer = _slot.CurrentOffer;
            _currentRotation = Rotation.Deg0;
            Active = this;

            if (Tooltip.Instance != null) Tooltip.Instance.Hide();

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) { _dragging = false; return; }

            _ghostInstance = Instantiate(_ghostPrefab.gameObject, canvas.transform);
            _ghostRT = _ghostInstance.GetComponent<RectTransform>();
            _ghostInstance.transform.SetAsLastSibling();
            var img = _ghostInstance.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(0.9f, 0.85f, 0.6f, 0.7f);
                img.raycastTarget = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            if (_gridParent == null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && _ghostRT != null)
            {
                RectTransform canvasRT = canvas.transform as RectTransform;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, null, out Vector2 local);
                _ghostRT.anchoredPosition = local;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_gridParent, eventData.position, null, out Vector2 gridLocal);
            _snappedOrigin = new Vector2Int(
                Mathf.FloorToInt(gridLocal.x / CELL_SIZE_PX),
                Mathf.FloorToInt(gridLocal.y / CELL_SIZE_PX));

            UpdateValidationOverlay();
        }

        private void Update()
        {
            if (!_dragging) return;

            // WS-012.1: R-key OR right-mouse-click rotates the dragged item 90° CW.
            // Items only — bags have no rotation field on the data model (parity with DragHandler).
            bool rotateRequested =
                (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
            if (rotateRequested && _draggedOffer is ItemData) Rotate();

            // Escape cancel.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelDrag();
            }
        }

        private void Rotate()
        {
            _currentRotation = ShapeMath.Next(_currentRotation);
            UpdateValidationOverlay();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            if (Active == this) Active = null;

            if (_gridView != null) _gridView.ResetCellHighlights();
            if (_ghostInstance != null) { Destroy(_ghostInstance); _ghostInstance = null; _ghostRT = null; }

            if (InventoryService.Instance == null || RunManager.Instance == null) return;
            if (_slot == null || _slot.CurrentOffer == null) return;

            int price = _slot.CurrentPrice;
            if (RunManager.Instance.CurrentGold < price) return;

            // 1) Valid grid cell: standard buy-and-place.
            if (IsDropValid())
            {
                if (!RunManager.Instance.SpendGold(price)) return;

                bool placed = false;
                if (_draggedOffer is ItemData itemData)
                    placed = InventoryService.Instance.PlaceItem(itemData, _snappedOrigin, _currentRotation) != null;
                else if (_draggedOffer is BagData bagData)
                    placed = InventoryService.Instance.PlaceBag(bagData, _snappedOrigin) != null;

                if (!placed)
                {
                    RunManager.Instance.AddGold(price); // refund (defensive)
                    return;
                }

                if (_shopController != null) _shopController.TakeOfferFromSlot(_slotIndex);
                return;
            }

            // 2) WS-012.5: drop in backpack column but on an invalid cell → buy and deposit to ground.
            // Items only; bags don't go to ground (they have no holding area).
            if (_draggedOffer is ItemData itemForGround
                && GroundManager.Current != null
                && DropZoneClassifier.IsWithinBackpackXRange(eventData.position))
            {
                if (!RunManager.Instance.SpendGold(price)) return;

                Vector2 groundLocal;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    GroundManager.Current.GroundAreaRT, eventData.position, null, out groundLocal);

                GroundManager.Current.AddItem(itemForGround, _currentRotation, isLocked: false, groundLocal, Vector2.zero);
                if (_shopController != null) _shopController.TakeOfferFromSlot(_slotIndex);
                return;
            }

            // 3) Out-of-range or unsupported case: standard cancel (drag aborts, no gold change).
        }

        private void CancelDrag()
        {
            _dragging = false;
            if (Active == this) Active = null;
            if (_gridView != null) _gridView.ResetCellHighlights();
            if (_ghostInstance != null) { Destroy(_ghostInstance); _ghostInstance = null; _ghostRT = null; }
            // No state change — slot remains available, no gold spent.
        }

        private void UpdateValidationOverlay()
        {
            if (_gridView == null || _draggedOffer == null) return;
            _gridView.ResetCellHighlights();

            var cells = new System.Collections.Generic.List<Vector2Int>();
            bool valid = false;

            if (_draggedOffer is ItemData item)
            {
                if (item.Shape == null) return;
                var rotatedCells = ShapeMath.Rotate(item.Shape.Cells, _currentRotation);
                foreach (var off in rotatedCells) cells.Add(_snappedOrigin + off);
                if (InventoryService.Instance != null)
                    valid = InventoryService.Instance.Grid.CanPlaceItem(item, _snappedOrigin, _currentRotation);
            }
            else if (_draggedOffer is BagData bag)
            {
                if (bag.Shape == null) return;
                foreach (var off in bag.Shape.Cells) cells.Add(_snappedOrigin + off);
                if (InventoryService.Instance != null)
                    valid = InventoryService.Instance.Grid.CanPlaceBag(bag, _snappedOrigin);
            }

            if (valid) _gridView.HighlightCellsValid(cells);
            else _gridView.HighlightCellsInvalid(cells);
        }

        private bool IsDropValid()
        {
            if (_draggedOffer == null || InventoryService.Instance == null) return false;
            if (_draggedOffer is ItemData item)
                return InventoryService.Instance.Grid.CanPlaceItem(item, _snappedOrigin, _currentRotation);
            if (_draggedOffer is BagData bag)
                return InventoryService.Instance.Grid.CanPlaceBag(bag, _snappedOrigin);
            return false;
        }
    }
}
