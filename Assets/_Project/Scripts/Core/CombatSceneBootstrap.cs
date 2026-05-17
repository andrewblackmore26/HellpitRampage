using HellpitRampage.Combat;
using HellpitRampage.Environment;
using HellpitRampage.Narrative;
using UnityEngine;

namespace HellpitRampage.Core
{
    /// <summary>
    /// WS-015: composition root for the Combat scene. Instantiates the scene-scoped
    /// narrative + environment controllers in code (no Inspector wiring — L-021), ensures
    /// a run is in progress, seeds this scene's player with the carried-over HP, then
    /// publishes <see cref="RoundStartedEvent"/> so combat-scene subscribers react now
    /// that the scene is live (they cannot receive it before the scene exists).
    /// </summary>
    public class CombatSceneBootstrap : MonoBehaviour
    {
        private void Start()
        {
            if (RunManager.Instance == null)
            {
                Debug.LogError("CombatSceneBootstrap: RunManager.Instance is null. Boot scene must have RunManager under Managers.");
                return;
            }

            // Created unconditionally — they must exist for every round so the
            // companion beat plays and the biome backdrop is set.
            CreateSceneController<CompanionAppearanceScheduler>("CompanionAppearanceScheduler");
            CreateSceneController<BiomeTransitionController>("BiomeTransitionController");

            // A fresh run (cold start from the menu, or Try Again after a run ended) reaches
            // the Combat scene with no round in progress — phase is Idle or RunEnd. A
            // continued round arrives already in the Combat phase (AdvanceToNextRound set it
            // before triggering the scene load), so StartNewRun must not fire then.
            if (RunManager.Instance.CurrentPhase != RunManager.RunPhase.Combat)
                RunManager.Instance.StartNewRun();

            ApplyCarriedHpToPlayer();

            // WS-015: the Combat scene is now live — publish RoundStartedEvent so in-scene
            // subscribers (CombatRoundController, CompanionAppearanceScheduler) react here.
            EventBus.Instance?.Publish(new RoundStartedEvent { RoundNumber = RunManager.Instance.CurrentRound });
        }

        /// <summary>
        /// Seeds this scene's freshly-spawned player Health from RunManager's canonical HP.
        /// WS-015: HP carries across the Combat&lt;-&gt;Shop scene transitions, and the player
        /// GameObject is rebuilt on every Combat scene load.
        /// </summary>
        private static void ApplyCarriedHpToPlayer()
        {
            var run = RunManager.Instance;
            if (run == null || run.MaxHp <= 0f) return;
            // L-004: parameterless FindObjectsByType overload.
            foreach (var health in Object.FindObjectsByType<Health>())
            {
                if (health != null && health.IsPlayer)
                {
                    health.RestoreFromSave(run.CurrentHp, run.MaxHp);
                    return;
                }
            }
        }

        private static void CreateSceneController<T>(string name) where T : Component
        {
            new GameObject(name).AddComponent<T>();
        }
    }
}
