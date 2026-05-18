using System.Collections;
using System.Collections.Generic;
using HellpitRampage.Core;
using HellpitRampage.Inventory;
using HellpitRampage.UI;
using UnityEngine;

namespace HellpitRampage.Save
{
    /// <summary>
    /// WS-015: lives in the Shop scene. A resume always lands here — a run save is only
    /// captured at shop-phase entry. If <see cref="GameManager.PendingResume"/> is set when
    /// the scene loads, rebuild run state from the save (ShopSceneBootstrap defers its own
    /// startup to us). Defers one frame so every Awake/OnEnable has completed first.
    /// </summary>
    public class RunRestoreController : MonoBehaviour
    {
        private void Start()
        {
            StartCoroutine(RestoreNextFrame());
        }

        private IEnumerator RestoreNextFrame()
        {
            yield return null;

            if (GameManager.Instance == null) yield break;
            var data = GameManager.Instance.PendingResume;
            if (data == null) yield break;
            GameManager.Instance.PendingResume = null;

            if (SaveManager.Instance != null) SaveManager.Instance.BeginRestore();
            try
            {
                ApplySave(data);
            }
            finally
            {
                if (SaveManager.Instance != null) SaveManager.Instance.EndRestore();
            }

            EventBus.Instance?.Publish(new RunResumedEvent());

            // WS-015: ShopSceneBootstrap deferred its ShopPhaseStartedEvent to us (PendingResume
            // was set). Publish it now that state is rebuilt — it drives the GroundArea rebuild,
            // the drag-mode reset, the shop slot population, and the auto-save. The auto-save
            // handler's restore flag is now cleared, so a confirming same-content save fires:
            // intentional — it re-writes exactly what we loaded and proves the file is sound.
            EventBus.Instance?.Publish(new ShopPhaseStartedEvent { RoundNumber = data.CurrentRound });
        }

        private static void ApplySave(RunSaveData data)
        {
            if (DataRegistry.Instance == null)
            {
                Debug.LogError("[RunRestoreController] DataRegistry.Instance is null. Cannot resolve content IDs.");
                return;
            }

            var hero = DataRegistry.Instance.GetHero(data.HeroId);

            // WS-015: RunManager owns the canonical HP — RestoreFromSave applies round, gold,
            // hero AND HP. There is no player Health component in the Shop scene to restore.
            if (RunManager.Instance != null)
                RunManager.Instance.RestoreFromSave(data.CurrentRound, data.Gold, hero,
                                                    data.PlayerCurrentHp, data.PlayerMaxHp);

            // Ground items are accumulated here (saved ground items + any item that no longer
            // fits its grid origin) and handed to InventoryService in one batch. GroundManager
            // rebuilds its visuals from this list when ShopPhaseStartedEvent fires (above).
            var groundSnapshot = new List<GroundItemSnapshot>();
            foreach (var g in data.GroundItems)
            {
                var groundData = DataRegistry.Instance.GetItem(g.ItemId);
                if (groundData == null) continue; // GetItem already logged a warning
                groundSnapshot.Add(new GroundItemSnapshot
                {
                    ItemId = groundData,
                    Rotation = (Rotation)g.Rotation,
                    IsLocked = g.IsLocked,
                });
            }

            var inv = InventoryService.Instance;
            if (inv != null)
            {
                inv.ClearAll();

                // Bags must precede items: items need their host bag's cells available.
                foreach (var bagEntry in data.Bags)
                {
                    var bagData = DataRegistry.Instance.GetBag(bagEntry.BagId);
                    if (bagData == null) continue; // GetBag already logged a warning
                    var origin = new Vector2Int(bagEntry.OriginX, bagEntry.OriginY);
                    var bag = inv.PlaceBag(bagData, origin);
                    if (bag != null) bag.IsLocked = bagEntry.IsLocked;
                    else Debug.LogWarning($"[RunRestoreController] Failed to place bag '{bagEntry.BagId}' at {origin}; possibly out of bounds.");
                }

                foreach (var itemEntry in data.Items)
                {
                    var itemData = DataRegistry.Instance.GetItem(itemEntry.ItemId);
                    if (itemData == null) continue;
                    var origin = new Vector2Int(itemEntry.OriginX, itemEntry.OriginY);
                    var rotation = (Rotation)itemEntry.Rotation;
                    var item = inv.PlaceItem(itemData, origin, rotation);
                    if (item != null)
                    {
                        item.IsLocked = itemEntry.IsLocked;
                    }
                    else
                    {
                        // Shape conflict (content changed since save) → spill to the ground.
                        groundSnapshot.Add(new GroundItemSnapshot
                        {
                            ItemId = itemData,
                            Rotation = rotation,
                            IsLocked = itemEntry.IsLocked,
                        });
                        Debug.LogWarning($"[RunRestoreController] Item '{itemEntry.ItemId}' didn't fit at {origin}; spilled to ground.");
                    }
                }
            }

            // One batch hand-off. GroundManager mirrors this back when it rebuilds its visuals.
            if (inv != null) inv.SyncGroundItems(groundSnapshot);
        }
    }
}
