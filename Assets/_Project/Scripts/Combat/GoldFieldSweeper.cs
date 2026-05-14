using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Combat
{
    /// <summary>
    /// At round end, sweeps all remaining gold pickups on the field and auto-collects
    /// them to the player. The field is clean at shop-phase entry — no gold persists
    /// between rounds. Required prerequisite for the WS-012.5 spillover system, which
    /// assumes only items can persist on the floor.
    /// </summary>
    public class GoldFieldSweeper : MonoBehaviour
    {
        private void OnEnable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Subscribe<RoundEndedEvent>(HandleRoundEnded);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Unsubscribe<RoundEndedEvent>(HandleRoundEnded);
        }

        private void HandleRoundEnded(RoundEndedEvent _)
        {
            // L-004: no-arg FindObjectsByType, the FindObjectsSortMode overload is deprecated.
            // Defaults to FindObjectsInactive.Exclude — pooled-inactive pickups won't appear.
            GoldPickup[] pickups = Object.FindObjectsByType<GoldPickup>();
            foreach (var pickup in pickups)
            {
                if (pickup == null) continue;
                if (!pickup.gameObject.activeInHierarchy) continue;
                pickup.ForceCollect();
            }
        }
    }
}
