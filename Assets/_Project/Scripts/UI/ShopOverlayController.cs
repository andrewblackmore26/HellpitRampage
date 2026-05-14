using HellpitRampage.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    public class ShopOverlayController : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _headerLabel;
        [SerializeField] private Button _startNextRoundButton;

        private void OnEnable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<RoundEndedEvent>(HandleRoundEnded);
                EventBus.Instance.Subscribe<RoundStartedEvent>(HandleRoundStarted);
                EventBus.Instance.Subscribe<RunEndedEvent>(HandleRunEnded);
            }
            if (_startNextRoundButton != null)
                _startNextRoundButton.onClick.AddListener(HandleStartNextClicked);

            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<RoundEndedEvent>(HandleRoundEnded);
                EventBus.Instance.Unsubscribe<RoundStartedEvent>(HandleRoundStarted);
                EventBus.Instance.Unsubscribe<RunEndedEvent>(HandleRunEnded);
            }
            if (_startNextRoundButton != null)
                _startNextRoundButton.onClick.RemoveListener(HandleStartNextClicked);
        }

        private void HandleRoundEnded(RoundEndedEvent evt)
        {
            // Suppress shop on the final round — the run-end overlay takes over instead.
            if (RunManager.Instance != null && RunManager.Instance.CurrentRound >= RunManager.Instance.TotalRounds)
                return;

            if (_panel != null) _panel.SetActive(true);
            if (_headerLabel != null) _headerLabel.text = $"Round {evt.RoundNumber} Complete";
        }

        private void HandleRoundStarted(RoundStartedEvent _)
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void HandleRunEnded(RunEndedEvent _)
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void HandleStartNextClicked()
        {
            if (RunManager.Instance != null) RunManager.Instance.AdvanceToNextRound();
        }
    }
}
