using HellpitRampage.Core;
using TMPro;
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
            if (!hasSave) return;

            // WS-014.B: surface what the save resumes into, folded into the button label
            // so no extra UI element needs wiring.
            var label = _resumeRunButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;

            string text = "Continue Run";
            RunSaveData data = SaveManager.Instance.LoadRun();
            if (data != null)
            {
                string heroName = "Unknown";
                HeroData hero = DataRegistry.Instance != null
                    ? DataRegistry.Instance.GetHero(data.HeroId)
                    : null;
                if (hero != null) heroName = hero.DisplayName;
                text = $"Continue Run\nRound {data.CurrentRound} · {data.Gold}g · {heroName}";
            }
            label.text = text;
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
