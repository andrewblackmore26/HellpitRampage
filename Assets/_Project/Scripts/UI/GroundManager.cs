using System.Collections.Generic;
using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.5: scene-scoped manager for items resting on the spillover floor. Spawns
    /// per-item GameObjects under <c>_groundAreaRT</c>, runs the item-vs-item collision
    /// pass in FixedUpdate, sorts Z-order by anchored Y so piles read correctly, and
    /// exposes Snapshot/Restore for a future save system (WS-013).
    /// <para/>
    /// Lifecycle: GroundArea activates on <see cref="ShopPhaseStartedEvent"/>, deactivates
    /// on <see cref="RoundStartedEvent"/>, clears on <see cref="RunStartedEvent"/>.
    /// </summary>
    public class GroundManager : MonoBehaviour, IDropHandler
    {
        public static GroundManager Current { get; private set; }

        [Header("Wiring")]
        [SerializeField] private RectTransform _groundAreaRT;
        [SerializeField] private GameObject _groundItemPrefab;
        // WS-012.5: 16x16 overlay drawn on locked ground items. Same sprite InventoryGridView
        // uses on grid items, so locked grid + locked ground share visual language.
        [SerializeField] private Sprite _lockIconSprite;

        private readonly List<GroundItem> _items = new();
        private float _itemAlpha = 1f;

        public RectTransform GroundAreaRT => _groundAreaRT;
        public IReadOnlyList<GroundItem> Items => _items;

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void OnEnable()
        {
            // L-007: hot-reload safety — re-assign Current if Awake didn't run.
            Current = this;

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<RunStartedEvent>(HandleRunStarted);
                EventBus.Instance.Subscribe<ShopPhaseStartedEvent>(HandleShopStarted);
                EventBus.Instance.Subscribe<RoundStartedEvent>(HandleRoundStarted);
                EventBus.Instance.Subscribe<DragModeChangedEvent>(HandleDragModeChanged);
                EventBus.Instance.Subscribe<ItemLockChangedEvent>(HandleItemLockChanged);
            }
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<RunStartedEvent>(HandleRunStarted);
                EventBus.Instance.Unsubscribe<ShopPhaseStartedEvent>(HandleShopStarted);
                EventBus.Instance.Unsubscribe<RoundStartedEvent>(HandleRoundStarted);
                EventBus.Instance.Unsubscribe<DragModeChangedEvent>(HandleDragModeChanged);
                EventBus.Instance.Unsubscribe<ItemLockChangedEvent>(HandleItemLockChanged);
            }
        }

        // WS-012.3: when the unified tooltip's lock action toggles the ItemInstance, refresh
        // our visual overlay. Grid items get refreshed by InventoryGridView's RefreshAll on
        // the same event; ground items live in our managed list so we touch only the affected
        // visual.
        private void HandleItemLockChanged(ItemLockChangedEvent e)
        {
            if (e.Item == null) return;
            var gi = FindByInstance(e.Item);
            if (gi == null || gi.Visual == null) return;
            RefreshLockOverlay(gi);
        }

        private void HandleRunStarted(RunStartedEvent _) => ClearAll();

        private void HandleShopStarted(ShopPhaseStartedEvent _)
        {
            if (_groundAreaRT != null) _groundAreaRT.gameObject.SetActive(true);
        }

        private void HandleRoundStarted(RoundStartedEvent _)
        {
            if (_groundAreaRT != null) _groundAreaRT.gameObject.SetActive(false);
        }

        private void HandleDragModeChanged(DragModeChangedEvent e)
        {
            bool itemsMode = e.NewMode == DragMode.Items;
            _itemAlpha = itemsMode ? 1f : 0.4f;
            foreach (var gi in _items)
            {
                if (gi.Visual == null) continue;
                ApplyVisualMode(gi.Visual, itemsMode);
            }
        }

        private void ApplyVisualMode(GameObject visual, bool itemsMode)
        {
            var img = visual.GetComponent<Image>();
            if (img != null)
            {
                Color c = img.color;
                c.a = _itemAlpha;
                img.color = c;
            }
            var dh = visual.GetComponent<GroundDragHandler>();
            if (dh != null) dh.enabled = itemsMode;
        }

        // -------- Mutators --------

        public GroundItem AddItem(ItemData data, Rotation rotation, bool isLocked, Vector2 spawnPosLocal, Vector2 initialVelocity)
        {
            if (_groundItemPrefab == null || _groundAreaRT == null || data == null) return null;

            GameObject go = Instantiate(_groundItemPrefab, _groundAreaRT);
            go.name = $"GroundItem_{data.ItemName}";

            // Position
            var rt = go.transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition = spawnPosLocal;
                // Make sure the prefab's pivot is centered for our AABB math.
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            // Visual
            var img = go.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = data.Icon;
                img.color = data.Icon != null
                    ? new Color(1f, 1f, 1f, _itemAlpha)
                    : new Color(data.PlaceholderColor.r, data.PlaceholderColor.g, data.PlaceholderColor.b, _itemAlpha);
                img.raycastTarget = true;
            }

            // Physics
            var physics = go.GetComponent<GroundItemPhysics>();
            if (physics != null) physics.Initialize(initialVelocity, _groundAreaRT);

            // Build the wrapper. InstanceID = 0 sentinel — ground items aren't grid-tracked.
            // Lock + data + rotation are all read from Instance via property accessors.
            var groundItem = new GroundItem
            {
                Visual = go,
                Instance = new ItemInstance(0, data, Vector2Int.zero, null, rotation) { IsLocked = isLocked }
            };

            // Drag handler
            var dh = go.GetComponent<GroundDragHandler>();
            if (dh != null) dh.Initialize(groundItem);

            // WS-012.3: unified tooltip. Hover shows preview; left-click pins with lock/book.
            // Same component grid items use — keeps semantics uniform across grid + ground.
            var inspect = go.GetComponent<InspectableItem>();
            if (inspect != null)
            {
                inspect.ItemKind = InspectableItem.Kind.OwnedItem;
                inspect.Item = groundItem.Instance;
            }

            // Lock icon overlay if the item spawns locked (e.g., locked item spilled from a sold bag).
            RefreshLockOverlay(groundItem);

            // Honor current drag mode at spawn time so spawning while in Bags mode greys out.
            bool itemsMode = DragModeService.Current == null || DragModeService.Current.CurrentMode == DragMode.Items;
            ApplyVisualMode(go, itemsMode);

            _items.Add(groundItem);

            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new GroundItemAddedEvent { Item = groundItem.Instance });

            return groundItem;
        }

        public bool RemoveItem(GroundItem gi)
        {
            if (gi == null) return false;
            if (!_items.Remove(gi)) return false;
            if (gi.Visual != null) Destroy(gi.Visual);
            if (EventBus.Instance != null)
                EventBus.Instance.Publish(new GroundItemRemovedEvent { Item = gi.Instance });
            return true;
        }

        public GroundItem FindByInstance(ItemInstance instance)
        {
            if (instance == null) return null;
            foreach (var gi in _items)
                if (gi.Instance == instance) return gi;
            return null;
        }

        // WS-012.5: parity with InventoryService.ContainsItem. Used by GroundDragHandler's
        // "did the sell modal remove me mid-drag" defensive check (L-012 trap).
        public bool ContainsItem(GroundItem gi)
        {
            if (gi == null) return false;
            foreach (var existing in _items)
                if (existing == gi) return true;
            return false;
        }

        // WS-012.5: add/remove the 16x16 lock icon overlay on a ground item visual so it
        // matches the grid items' lock affordance. Looks for an existing child named
        // "LockIcon" and creates or destroys it as needed based on Instance.IsLocked.
        private void RefreshLockOverlay(GroundItem gi)
        {
            if (gi == null || gi.Visual == null) return;
            Transform existing = gi.Visual.transform.Find("LockIcon");

            bool shouldShow = gi.Instance != null && gi.Instance.IsLocked;
            if (shouldShow)
            {
                if (existing != null) return; // already attached
                AttachLockIcon(gi.Visual.transform);
            }
            else if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }

        private void AttachLockIcon(Transform parent)
        {
            if (_lockIconSprite == null)
            {
                Debug.LogWarning("[GroundManager] _lockIconSprite is not assigned; locked ground items will render without an icon.");
                return;
            }
            var lockGO = new GameObject("LockIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lockGO.transform.SetParent(parent, false);
            var lockImg = lockGO.GetComponent<Image>();
            lockImg.sprite = _lockIconSprite;
            lockImg.raycastTarget = false;
            var lockRT = (RectTransform)lockGO.transform;
            lockRT.anchorMin = new Vector2(0f, 1f);
            lockRT.anchorMax = new Vector2(0f, 1f);
            lockRT.pivot = new Vector2(0f, 1f);
            lockRT.sizeDelta = new Vector2(16f, 16f);
            lockRT.anchoredPosition = new Vector2(4f, -4f);
        }

        public void ClearAll()
        {
            foreach (var gi in _items)
                if (gi.Visual != null) Destroy(gi.Visual);
            _items.Clear();
        }

        // -------- Snapshot for future save support (WS-013) --------

        public List<GroundItemSnapshot> SnapshotGroundState()
        {
            var snap = new List<GroundItemSnapshot>(_items.Count);
            foreach (var gi in _items)
                snap.Add(new GroundItemSnapshot { ItemId = gi.Data, Rotation = gi.Rotation, IsLocked = gi.IsLocked });
            return snap;
        }

        public void RestoreGroundState(List<GroundItemSnapshot> snapshot)
        {
            ClearAll();
            if (snapshot == null || _groundAreaRT == null) return;

            float halfW = _groundAreaRT.rect.width * 0.5f;
            float halfH = _groundAreaRT.rect.height * 0.5f;
            foreach (var snap in snapshot)
            {
                if (snap.ItemId == null) continue;
                float spawnX = Random.Range(-halfW * 0.8f, halfW * 0.8f);
                float spawnY = halfH - 20f;
                AddItem(snap.ItemId, snap.Rotation, snap.IsLocked, new Vector2(spawnX, spawnY), new Vector2(0f, -200f));
            }
        }

        // -------- IDropHandler --------

        public void OnDrop(PointerEventData eventData)
        {
            // The dragged handler's OnEndDrag classifies the drop position and chooses
            // grid vs ground. This handler exists so the GroundArea claims the drop event
            // and the EventSystem doesn't bubble to a less-relevant target.
        }

        // -------- Item-vs-item collision pass + Z-order --------

        private void FixedUpdate()
        {
            if (_items.Count == 0) return;

            // 3-pass AABB push-out. Most piles converge in 1-2 passes; cap at 3 to keep
            // per-frame work bounded.
            for (int pass = 0; pass < 3; pass++)
            {
                bool anyOverlap = false;
                for (int i = 0; i < _items.Count; i++)
                {
                    var a = _items[i];
                    if (a.Visual == null) continue;
                    var pa = a.Visual.GetComponent<GroundItemPhysics>();
                    if (pa == null || pa.IsHeld) continue;

                    for (int j = i + 1; j < _items.Count; j++)
                    {
                        var b = _items[j];
                        if (b.Visual == null) continue;
                        var pb = b.Visual.GetComponent<GroundItemPhysics>();
                        if (pb == null || pb.IsHeld) continue;

                        Rect ra = pa.GetLocalAABB();
                        Rect rb = pb.GetLocalAABB();
                        if (!ra.Overlaps(rb)) continue;

                        anyOverlap = true;

                        float overlapX = Mathf.Min(ra.xMax, rb.xMax) - Mathf.Max(ra.xMin, rb.xMin);
                        float overlapY = Mathf.Min(ra.yMax, rb.yMax) - Mathf.Max(ra.yMin, rb.yMin);

                        var rtA = (RectTransform)pa.transform;
                        var rtB = (RectTransform)pb.transform;
                        if (overlapX < overlapY)
                        {
                            float push = overlapX * 0.5f;
                            bool aLeft = ra.center.x < rb.center.x;
                            rtA.anchoredPosition += new Vector2(aLeft ? -push : push, 0);
                            rtB.anchoredPosition += new Vector2(aLeft ? push : -push, 0);
                            pa.Velocity.x *= -0.3f; pb.Velocity.x *= -0.3f;
                        }
                        else
                        {
                            float push = overlapY * 0.5f;
                            bool aBelow = ra.center.y < rb.center.y;
                            rtA.anchoredPosition += new Vector2(0, aBelow ? -push : push);
                            rtB.anchoredPosition += new Vector2(0, aBelow ? push : -push);
                            pa.Velocity.y *= -0.3f; pb.Velocity.y *= -0.3f;
                        }
                        pa.Wake(); pb.Wake();
                    }
                }
                if (!anyOverlap) break;
            }

            // Z-order: lower-y items render on top so the pile reads correctly.
            // Sort by descending Y so that low Y items get higher sibling indices.
            _items.Sort(CompareByY);
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Visual != null) _items[i].Visual.transform.SetSiblingIndex(i);
        }

        private static int CompareByY(GroundItem a, GroundItem b)
        {
            if (a.Visual == null || b.Visual == null) return 0;
            float ay = ((RectTransform)a.Visual.transform).anchoredPosition.y;
            float by = ((RectTransform)b.Visual.transform).anchoredPosition.y;
            return by.CompareTo(ay);
        }
    }
}
