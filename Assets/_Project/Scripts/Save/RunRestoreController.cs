using System.Collections;
using HellpitRampage.Combat;
using HellpitRampage.Core;
using HellpitRampage.Inventory;
using HellpitRampage.UI;
using UnityEngine;

namespace HellpitRampage.Save
{
    /// <summary>
    /// WS-013: lives in the Game scene. If GameManager.PendingResume is set when the scene loads,
    /// rebuild run state from the save data instead of starting a fresh run. Defers one frame so
    /// every Awake() (singletons, scene objects) has completed before we read or mutate.
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

            // WS-014.A (C-2 fix): a resumed run lands in the shop phase, but the shop UI opens
            // off RoundEndedEvent — ShopOverlayController activates its panel and ShopController
            // populates the 5 slots on RoundEndedEvent, NOT on ShopPhaseStartedEvent. Without
            // this publish the shop never appears and the resumed run is soft-locked.
            // Mirror RunManager.EndCurrentRound's order: RoundEndedEvent first (shop UI +
            // CombatRoundController shop-state), then ShopPhaseStartedEvent for the shop-only
            // consumers below. Side effects on resume are verified harmless: no round-end gold
            // is awarded (only RunManager.EndCurrentRound awards it, and it is not invoked), and
            // GoldFieldSweeper's sweep is a no-op because no gold pickups exist on resume.
            EventBus.Instance?.Publish(new RoundEndedEvent { RoundNumber = data.CurrentRound });

            // Drive shop-only systems: GroundManager activates GroundArea, DragModeService resets
            // to Items mode. Our own auto-save handler has its restore flag cleared, so a save
            // will fire — that's intentional: it re-writes the same content we just loaded and
            // confirms the file is well-formed.
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

            if (RunManager.Instance != null)
                RunManager.Instance.RestoreFromSave(data.CurrentRound, data.Gold, hero);

            // Player Health — find by IsPlayer (no enemies alive in shop phase).
            Health playerHealth = FindPlayerHealth();
            if (playerHealth != null)
                playerHealth.RestoreFromSave(data.PlayerCurrentHp, data.PlayerMaxHp);

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
                        // Shape conflict (content changed since save) → spillover to the ground area.
                        if (GroundManager.Current != null)
                        {
                            GroundManager.Current.AddItem(itemData, rotation, itemEntry.IsLocked, Vector2.zero, Vector2.zero);
                            Debug.LogWarning($"[RunRestoreController] Item '{itemEntry.ItemId}' didn't fit at {origin}; spilled to ground.");
                        }
                        else
                        {
                            Debug.LogWarning($"[RunRestoreController] Item '{itemEntry.ItemId}' didn't fit at {origin}; no GroundManager available, item lost.");
                        }
                    }
                }
            }

            // Ground items — convert save entries back to snapshot type and hand off.
            if (GroundManager.Current != null)
            {
                var snapshot = new System.Collections.Generic.List<GroundItemSnapshot>();
                foreach (var g in data.GroundItems)
                {
                    var itemData = DataRegistry.Instance.GetItem(g.ItemId);
                    if (itemData == null) continue;
                    snapshot.Add(new GroundItemSnapshot
                    {
                        ItemId = itemData,
                        Rotation = (Rotation)g.Rotation,
                        IsLocked = g.IsLocked,
                    });
                }
                GroundManager.Current.RestoreGroundState(snapshot);
            }
        }

        private static Health FindPlayerHealth()
        {
            // L-004: parameterless overload.
            var all = Object.FindObjectsByType<Health>();
            foreach (var h in all)
                if (h != null && h.IsPlayer) return h;
            return null;
        }
    }
}
