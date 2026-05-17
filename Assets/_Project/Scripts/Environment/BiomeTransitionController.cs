using HellpitRampage.Core;
using UnityEngine;

namespace HellpitRampage.Environment
{
    /// <summary>
    /// WS-014.B: placeholder biome system. Tints the camera background per the round's
    /// biome (Outskirts 1-10, Inner Town 11-20, Depths 21-30). Instant colour set, no
    /// animation — real biome art replaces this entirely later. Scene-scoped component;
    /// instantiated in code by <see cref="HellpitRampage.Core.CombatSceneBootstrap"/>.
    /// </summary>
    public class BiomeTransitionController : MonoBehaviour
    {
        [SerializeField] private Color _outskirtsColor = new Color(0.4f, 0.5f, 0.6f);
        [SerializeField] private Color _innerTownColor = new Color(0.5f, 0.4f, 0.4f);
        [SerializeField] private Color _depthsColor = new Color(0.3f, 0.2f, 0.35f);

        private Camera _camera;

        private void OnEnable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Subscribe<RoundStartedEvent>(HandleRoundStarted);
            // Resumed runs republish ShopPhaseStartedEvent (not RoundStartedEvent), so the
            // backdrop would otherwise stay default until the next round advance.
            EventBus.Instance.Subscribe<ShopPhaseStartedEvent>(HandleShopPhaseStarted);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Unsubscribe<RoundStartedEvent>(HandleRoundStarted);
            EventBus.Instance.Unsubscribe<ShopPhaseStartedEvent>(HandleShopPhaseStarted);
        }

        private void HandleRoundStarted(RoundStartedEvent e) => ApplyBiome(e.RoundNumber);

        private void HandleShopPhaseStarted(ShopPhaseStartedEvent e) => ApplyBiome(e.RoundNumber);

        private void ApplyBiome(int round)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            // Force SolidColor clear so the tint is actually visible (a Skybox/Depth
            // clear would ignore backgroundColor).
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = ColorForRound(round);
        }

        /// <summary>Returns the backdrop colour for the given round's biome.</summary>
        public Color ColorForRound(int round)
        {
            if (round <= 10) return _outskirtsColor;
            if (round <= 20) return _innerTownColor;
            return _depthsColor;
        }
    }
}
