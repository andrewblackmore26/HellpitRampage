using HellpitRampage.Environment;
using HellpitRampage.Narrative;
using UnityEngine;

namespace HellpitRampage.Core
{
    /// <summary>
    /// Game-scene composition root. WS-014.B: instantiates the scene-scoped narrative
    /// and environment controllers in code (no Inspector wiring — L-021), then either
    /// starts a fresh run or yields to RunRestoreController for a resume.
    /// </summary>
    public class GameSceneBootstrap : MonoBehaviour
    {
        private void Start()
        {
            if (RunManager.Instance == null)
            {
                Debug.LogError("GameSceneBootstrap: RunManager.Instance is null. Boot scene must have RunManager under Managers.");
                return;
            }

            // WS-014.B: create these unconditionally — they must exist on a resumed run
            // too, or the first round advanced after a resume would soft-lock waiting on
            // CompanionAppearanceCompleteEvent.
            CreateSceneController<CompanionAppearanceScheduler>("CompanionAppearanceScheduler");
            CreateSceneController<BiomeTransitionController>("BiomeTransitionController");

            // WS-013: on a resume, RunRestoreController rebuilds run state (one frame later).
            // Starting a fresh run here would spawn round-1 combat on top of the restored shop
            // and leave the round timer running forever. PendingResume is still set this frame
            // (RunRestoreController clears it next frame), so bail and let the restore own startup.
            if (GameManager.Instance != null && GameManager.Instance.PendingResume != null) return;

            RunManager.Instance.StartNewRun();
        }

        private static void CreateSceneController<T>(string name) where T : Component
        {
            new GameObject(name).AddComponent<T>();
        }
    }
}
