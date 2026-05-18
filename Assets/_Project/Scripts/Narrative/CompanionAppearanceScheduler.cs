using System.Collections;
using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Narrative
{
    /// <summary>
    /// WS-014.B: on each round start, decides the companion's emotional state and shows
    /// the placeholder appearance. Rounds 23-24 are deliberately silent — the scheduler
    /// still publishes <see cref="CompanionAppearanceCompleteEvent"/> so combat is not
    /// blocked. Scene-scoped; instantiated in code by
    /// <see cref="HellpitRampage.Core.CombatSceneBootstrap"/>. It also builds and owns its
    /// <see cref="CompanionPlaceholderUI"/> child so no Inspector wiring is required.
    /// </summary>
    public class CompanionAppearanceScheduler : MonoBehaviour
    {
        private CompanionPlaceholderUI _placeholderUI;

        private void Awake()
        {
            // Build the placeholder UI under the scene Canvas. If there is no Canvas the
            // UI stays null and HandleRoundStarted falls back to publishing the complete
            // event immediately — combat still proceeds.
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("CompanionPlaceholderUI", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(canvas.transform, worldPositionStays: false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _placeholderUI = go.AddComponent<CompanionPlaceholderUI>();
        }

        private void OnEnable()
        {
            EventBus.Instance?.Subscribe<RoundStartedEvent>(HandleRoundStarted);
        }

        private void OnDisable()
        {
            EventBus.Instance?.Unsubscribe<RoundStartedEvent>(HandleRoundStarted);
        }

        private void HandleRoundStarted(RoundStartedEvent e)
        {
            // Rounds 23-24: companion is absent (or no UI was built) — but still unblock
            // combat. Defer one frame so the orchestrator finishes handling
            // RoundStartedEvent (recording the pending round) before it receives the
            // complete event; publishing synchronously here would race that ordering.
            if (e.RoundNumber == 23 || e.RoundNumber == 24 || _placeholderUI == null)
            {
                StartCoroutine(PublishCompleteNextFrame());
                return;
            }

            // Visible rounds publish the complete event from CompanionPlaceholderUI's
            // coroutine (which always yields at least one frame first), so they are
            // already free of that ordering race.
            _placeholderUI.Show(e.RoundNumber, GetCompanionState(e.RoundNumber), e.RoundNumber == 22);
        }

        private static IEnumerator PublishCompleteNextFrame()
        {
            yield return null;
            EventBus.Instance?.Publish(new CompanionAppearanceCompleteEvent());
        }

        /// <summary>
        /// Maps a round number to the companion's placeholder emotional state
        /// (per the run-arc design: Composed → Concerned → Pleading → Glitched →
        /// Devil → Final).
        /// </summary>
        public static string GetCompanionState(int round)
        {
            if (round <= 9) return "Composed";
            if (round <= 14) return "Concerned";
            if (round <= 21) return "Pleading";
            if (round == 22) return "Glitched";
            if (round <= 29) return "Devil";
            return "Final";
        }
    }
}
