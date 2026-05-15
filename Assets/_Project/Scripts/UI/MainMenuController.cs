using HellpitRampage.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button _startRunButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private SettingsMenuController _settingsMenu;
        // WS-013: visible only when a valid run save exists.
        [SerializeField] private Button _resumeRunButton;

        private void OnEnable()
        {
            if (_startRunButton != null)
            {
                _startRunButton.onClick.AddListener(OnStartRunClicked);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.AddListener(OnSettingsClicked);
            }

            if (_resumeRunButton != null)
            {
                _resumeRunButton.onClick.AddListener(OnResumeRunClicked);
            }

            RefreshResumeButtonVisibility();
        }

        private void OnDisable()
        {
            if (_startRunButton != null)
            {
                _startRunButton.onClick.RemoveListener(OnStartRunClicked);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            }

            if (_resumeRunButton != null)
            {
                _resumeRunButton.onClick.RemoveListener(OnResumeRunClicked);
            }
        }

        private void RefreshResumeButtonVisibility()
        {
            if (_resumeRunButton == null) return;
            bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasRunSave();
            _resumeRunButton.gameObject.SetActive(hasSave);
        }

        private void OnStartRunClicked()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("MainMenuController: GameManager.Instance is null. Did the Boot scene initialize?");
                return;
            }

            // WS-013: starting a fresh run clears any prior in-progress save so the player can
            // never accidentally resume into stale state mid-clean-run.
            if (SaveManager.Instance != null) SaveManager.Instance.DeleteRunSave();

            GameManager.Instance.TransitionTo(GameManager.GameState.InRun);
        }

        private void OnSettingsClicked()
        {
            if (_settingsMenu == null)
            {
                Debug.LogError("MainMenuController: _settingsMenu is not assigned.");
                return;
            }
            _settingsMenu.Open(onClose: null);
        }

        private void OnResumeRunClicked()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("MainMenuController: GameManager.Instance is null. Did the Boot scene initialize?");
                return;
            }
            GameManager.Instance.StartResumeFromSave();
        }
    }
}
