using HellpitRampage.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button _startRunButton;

        private void OnEnable()
        {
            if (_startRunButton != null)
            {
                _startRunButton.onClick.AddListener(OnStartRunClicked);
            }
        }

        private void OnDisable()
        {
            if (_startRunButton != null)
            {
                _startRunButton.onClick.RemoveListener(OnStartRunClicked);
            }
        }

        private void OnStartRunClicked()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("MainMenuController: GameManager.Instance is null. Did the Boot scene initialize?");
                return;
            }

            GameManager.Instance.TransitionTo(GameManager.GameState.InRun);
        }
    }
}
