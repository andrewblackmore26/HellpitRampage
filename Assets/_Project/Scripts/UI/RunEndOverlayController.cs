using HellpitRampage.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    public class RunEndOverlayController : MonoBehaviour
    {
        [SerializeField] private GameObject _overlayPanel;
        [SerializeField] private Text _headerLabel;
        [SerializeField] private Button _returnButton;

        private void OnEnable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Subscribe<RunEndedEvent>(HandleRunEnded);

            if (_returnButton != null)
                _returnButton.onClick.AddListener(HandleReturnClicked);

            if (_overlayPanel != null) _overlayPanel.SetActive(false);
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Unsubscribe<RunEndedEvent>(HandleRunEnded);

            if (_returnButton != null)
                _returnButton.onClick.RemoveListener(HandleReturnClicked);
        }

        private void HandleRunEnded(RunEndedEvent evt)
        {
            if (_overlayPanel != null) _overlayPanel.SetActive(true);
            if (_headerLabel != null)
                _headerLabel.text = evt.Victory ? "Run Complete!" : "You Died";

            // TODO(WS-pause-or-slowmo): replace Time.timeScale with TimeManager.SetPaused(true).
            Time.timeScale = 0f;
        }

        private void HandleReturnClicked()
        {
            // Reset timescale before scene change so the next scene loads unpaused.
            Time.timeScale = 1f;
            if (GameManager.Instance != null)
                GameManager.Instance.TransitionTo(GameManager.GameState.MainMenu);
        }
    }
}
