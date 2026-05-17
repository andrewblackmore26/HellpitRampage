using UnityEngine;

namespace HellpitRampage.Core
{
    /// <summary>
    /// WS-015: composition root for the Shop scene. Once the scene is live it publishes
    /// <see cref="ShopPhaseStartedEvent"/> — which drives the auto-save, activates the
    /// GroundArea, and resets the drag mode. On a resume the <c>RunRestoreController</c>
    /// owns that publish instead (it must finish rebuilding run state first), so this
    /// bootstrap defers to it whenever a resume is pending (L-020).
    /// </summary>
    public class ShopSceneBootstrap : MonoBehaviour
    {
        private void Start()
        {
            // Resume path: RunRestoreController rebuilds state one frame later and publishes
            // ShopPhaseStartedEvent itself once done. Publishing here too would fire it
            // against half-restored state.
            if (GameManager.Instance != null && GameManager.Instance.PendingResume != null) return;

            int round = RunManager.Instance != null ? RunManager.Instance.CurrentRound : 0;
            EventBus.Instance?.Publish(new ShopPhaseStartedEvent { RoundNumber = round });
        }
    }
}
