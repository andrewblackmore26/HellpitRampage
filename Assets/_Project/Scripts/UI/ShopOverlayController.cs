using HellpitRampage.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-015: with the shop now a dedicated scene, this controller no longer shows or
    /// hides an overlay panel — it just owns the shop header label and the Next Round
    /// button. (Name kept for continuity; "overlay" no longer applies literally.)
    /// </summary>
    public class ShopOverlayController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _headerLabel;
        [SerializeField] private Button _startNextRoundButton;

        private void OnEnable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Subscribe<ShopPhaseStartedEvent>(HandleShopPhaseStarted);
            if (_startNextRoundButton != null)
                _startNextRoundButton.onClick.AddListener(HandleStartNextClicked);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Unsubscribe<ShopPhaseStartedEvent>(HandleShopPhaseStarted);
            if (_startNextRoundButton != null)
                _startNextRoundButton.onClick.RemoveListener(HandleStartNextClicked);
        }

        private void HandleShopPhaseStarted(ShopPhaseStartedEvent evt)
        {
            if (_headerLabel != null) _headerLabel.text = $"Round {evt.RoundNumber} Complete";
        }

        private void HandleStartNextClicked()
        {
            if (RunManager.Instance != null) RunManager.Instance.AdvanceToNextRound();
        }
    }
}
