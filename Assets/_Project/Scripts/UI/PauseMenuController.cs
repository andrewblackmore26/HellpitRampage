using System;
using HellpitRampage.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// In-game pause menu. Listens to the Pause action (Esc / Gamepad Start),
    /// freezes time, and offers Resume / Settings / Quit-to-Menu.
    /// UI is built in Awake (L-014) — never in OnEnable, which would duplicate
    /// children on hot-reload.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private SettingsMenuController _settingsMenu;

        private GameObject _root;
        private Button _resumeButton;
        private Button _settingsButton;
        private Button _quitButton;

        private PlayerInputActions _input;
        private InputAction _pauseAction;
        private bool _isPaused;
        private bool _isRunEnded;
        private bool _settingsActive;

        private void Awake()
        {
            BuildUI();
            _root.SetActive(false);
            EnsurePauseAction();
        }

        private void OnEnable()
        {
            if (_root == null) return; // Awake hasn't run

            EnsurePauseAction();
            if (_pauseAction != null)
            {
                _pauseAction.performed += OnPausePerformed;
                _pauseAction.Enable();
            }

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<RunEndedEvent>(OnRunEnded);
            }

            _resumeButton.onClick.AddListener(OnResumeClicked);
            _settingsButton.onClick.AddListener(OnSettingsClicked);
            _quitButton.onClick.AddListener(OnQuitToMenuClicked);
        }

        private void OnDisable()
        {
            if (_root == null) return;

            if (_pauseAction != null)
            {
                _pauseAction.performed -= OnPausePerformed;
                _pauseAction.Disable();
            }

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<RunEndedEvent>(OnRunEnded);
            }

            _resumeButton.onClick.RemoveListener(OnResumeClicked);
            _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            _quitButton.onClick.RemoveListener(OnQuitToMenuClicked);
        }

        private void OnDestroy()
        {
            // PlayerInputActions.Dispose() destroys the asset built via FromJson.
            _input?.Dispose();
        }

        private void EnsurePauseAction()
        {
            if (_pauseAction != null) return;
            // Resolve the Pause action from the code-defined PlayerInputActions wrapper
            // (the same pattern PlayerController uses). No scene SerializeField to wire,
            // so it can never be left null — see L-021. Non-serialized fields reset to
            // null on hot-reload, so Awake AND OnEnable both call this.
            if (_input == null) _input = new PlayerInputActions();
            _pauseAction = _input.Player.Pause;
        }

        private void OnRunEnded(RunEndedEvent _)
        {
            _isRunEnded = true;
            // If we were paused when run ended, unfreeze our claim on timescale
            // (RunEndOverlay sets its own timescale = 0; we shouldn't fight it).
            if (_isPaused)
            {
                _isPaused = false;
                _root.SetActive(false);
            }
        }

        private void OnPausePerformed(InputAction.CallbackContext ctx)
        {
            // Don't intercept pause while the run-end overlay owns the pause state.
            if (_isRunEnded) return;
            // If settings menu is currently the visible overlay, route Pause to close it.
            if (_settingsActive)
            {
                _settingsMenu.Close();
                return;
            }
            TogglePause();
        }

        private void TogglePause()
        {
            if (_isPaused) Resume();
            else Pause();
        }

        private void Pause()
        {
            if (_isPaused) return;
            _isPaused = true;
            Time.timeScale = 0f;
            _root.SetActive(true);
        }

        private void Resume()
        {
            if (!_isPaused) return;
            _isPaused = false;
            Time.timeScale = 1f;
            _root.SetActive(false);
        }

        private void OnResumeClicked() => Resume();

        private void OnSettingsClicked()
        {
            if (_settingsMenu == null)
            {
                Debug.LogError("PauseMenuController: _settingsMenu not assigned.");
                return;
            }
            _root.SetActive(false);
            _settingsActive = true;
            _settingsMenu.Open(onClose: () =>
            {
                _settingsActive = false;
                if (_isPaused) _root.SetActive(true);
            });
        }

        private void OnQuitToMenuClicked()
        {
            Time.timeScale = 1f;
            _isPaused = false;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TransitionTo(GameManager.GameState.MainMenu);
            }
        }

        // ====================================================================
        // UI TREE CONSTRUCTION (Awake only — L-014)
        // ====================================================================

        private void BuildUI()
        {
            _root = NewChild(transform, "Root");
            var rootRT = _root.GetComponent<RectTransform>();
            StretchFull(rootRT);
            AddImage(_root, new Color(0f, 0f, 0f, 0.7f));

            var panel = NewChild(_root.transform, "Panel");
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(420, 320);
            panelRT.anchoredPosition = Vector2.zero;
            AddImage(panel, new Color(0.10f, 0.12f, 0.16f, 0.98f));
            AddVerticalLayout(panel, 14, new RectOffset(28, 28, 28, 28));

            var title = AddText(panel, "Paused", 28, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            AddLayoutElement(title.gameObject, preferredHeight: 48);

            _resumeButton = AddPrimaryButton(panel, "Resume", null);
            AddLayoutElement(_resumeButton.gameObject, preferredHeight: 44);

            _settingsButton = AddPrimaryButton(panel, "Settings", null);
            AddLayoutElement(_settingsButton.gameObject, preferredHeight: 44);

            _quitButton = AddPrimaryButton(panel, "Quit to Menu", null);
            AddLayoutElement(_quitButton.gameObject, preferredHeight: 44);
        }

        // --- Low-level UI primitives (mirror SettingsMenuController) ---

        private static GameObject NewChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image AddImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing, RectOffset padding)
        {
            var vl = go.AddComponent<VerticalLayoutGroup>();
            vl.spacing = spacing;
            vl.padding = padding;
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            return vl;
        }

        private static LayoutElement AddLayoutElement(GameObject go, float preferredWidth = -1, float preferredHeight = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferredWidth > 0) le.preferredWidth = preferredWidth;
            if (preferredHeight > 0) le.preferredHeight = preferredHeight;
            return le;
        }

        private static TextMeshProUGUI AddText(GameObject parent, string text, float fontSize, FontStyles style = FontStyles.Normal)
        {
            var go = NewChild(parent.transform, "Text");
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.fontStyle = style;
            return tmp;
        }

        private static Button AddPrimaryButton(GameObject parent, string label, Action onClick)
        {
            var go = NewChild(parent.transform, $"Btn_{label}");
            AddImage(go, new Color(0.30f, 0.42f, 0.65f));
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var lbl = AddText(go, label, 18, FontStyles.Bold);
            lbl.alignment = TextAlignmentOptions.Center;
            var lblRT = lbl.GetComponent<RectTransform>();
            StretchFull(lblRT);
            return btn;
        }
    }
}
