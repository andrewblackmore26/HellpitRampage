using UnityEngine;

namespace HellpitRampage.Core
{
    public class GameSceneBootstrap : MonoBehaviour
    {
        private void Start()
        {
            if (RunManager.Instance == null)
            {
                Debug.LogError("GameSceneBootstrap: RunManager.Instance is null. Boot scene must have RunManager under Managers.");
                return;
            }

            // WS-013: on a resume, RunRestoreController rebuilds run state (one frame later).
            // Starting a fresh run here would spawn round-1 combat on top of the restored shop
            // and leave the round timer running forever. PendingResume is still set this frame
            // (RunRestoreController clears it next frame), so bail and let the restore own startup.
            if (GameManager.Instance != null && GameManager.Instance.PendingResume != null) return;

            RunManager.Instance.StartNewRun();
        }
    }
}
