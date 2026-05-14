using System.Collections.Generic;
using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// v3: stars hover IN the target cell (the cell that, if occupied by a matching-tag neighbor,
    /// would activate one of the starred item's conditional effects). Dim when no valid neighbor
    /// is present; bright gold when active. Larger size (32×32) than v2's edge-midpoint stars.
    /// </summary>
    public class StarIndicatorOverlay : MonoBehaviour
    {
        private const int CELL_SIZE_PX = 56;
        private const int STAR_SIZE_PX = 32;

        [Header("Wiring")]
        [SerializeField] private RectTransform _gridParent;
        [Tooltip("Optional placeholder sprite. If null, the star renders as a tinted square (Unity default fallback).")]
        [SerializeField] private Sprite _starSprite;

        [Header("Style")]
        [SerializeField] private Color _idleColor = new(1f, 1f, 1f, 0.4f);
        [SerializeField] private Color _activeColor = new(0.95f, 0.85f, 0.35f, 1f);

        private readonly List<GameObject> _starInstances = new();

        private void OnEnable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<BagPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<BagMovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<ItemMovedEvent>(HandleAnyChange);
                EventBus.Instance.Subscribe<SynergyChangedEvent>(HandleAnyChange);
            }
            Rebuild();
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<BagPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<BagRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<BagMovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<ItemPlacedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<ItemRemovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<ItemMovedEvent>(HandleAnyChange);
                EventBus.Instance.Unsubscribe<SynergyChangedEvent>(HandleAnyChange);
            }
            ClearStars();
        }

        private void HandleAnyChange<T>(T _) where T : IGameEvent => Rebuild();

        private void Rebuild()
        {
            ClearStars();
            if (InventoryService.Instance == null || _gridParent == null) return;
            var grid = InventoryService.Instance.Grid;

            foreach (var item in grid.Items)
            {
                if (item?.Data?.StarredEdges == null) continue;
                if (item.Data.StarredEdges.Count == 0) continue;

                foreach (var star in item.EffectiveStarredEdges())
                {
                    Vector2Int absStarCell = item.Origin + star.Cell;
                    Vector2Int targetCell = absStarCell + DirectionOffset(star.Direction);

                    if (!grid.IsCellInBounds(targetCell)) continue;
                    // Don't render stars pointing into the starred item's own shape.
                    if (item.OccupiesCell(targetCell)) continue;

                    SpawnStar(item, star, targetCell);
                }
            }
        }

        private void ClearStars()
        {
            foreach (var go in _starInstances) if (go != null) Destroy(go);
            _starInstances.Clear();
        }

        private void SpawnStar(ItemInstance owner, StarredEdge star, Vector2Int targetCell)
        {
            GameObject go = new GameObject($"Star_{owner.InstanceID}_{targetCell.x}_{targetCell.y}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_gridParent, false);
            var rt = (RectTransform)go.transform;
            var img = go.GetComponent<Image>();
            if (_starSprite != null) img.sprite = _starSprite;
            img.raycastTarget = false;

            bool active = SynergyService.Instance != null
                && SynergyService.Instance.IsStarActive(owner.InstanceID, star.Cell, star.Direction);
            img.color = active ? _activeColor : _idleColor;

            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(STAR_SIZE_PX, STAR_SIZE_PX);
            // Center of the target cell in grid-local pixels.
            float cx = (targetCell.x + 0.5f) * CELL_SIZE_PX;
            float cy = (targetCell.y + 0.5f) * CELL_SIZE_PX;
            rt.anchoredPosition = new Vector2(cx, cy);

            // L-009: overlays in mixed-content parents must render last so cell backgrounds don't cover them.
            go.transform.SetAsLastSibling();

            _starInstances.Add(go);
        }

        private static Vector2Int DirectionOffset(EdgeDirection dir) => dir switch
        {
            EdgeDirection.Up => new Vector2Int(0, 1),
            EdgeDirection.Down => new Vector2Int(0, -1),
            EdgeDirection.Left => new Vector2Int(-1, 0),
            EdgeDirection.Right => new Vector2Int(1, 0),
            _ => Vector2Int.zero
        };
    }
}
