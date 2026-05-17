using HellpitRampage.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// Run-end overlay. Subscribes to <see cref="RunEndedEvent"/> and shows a death or
    /// victory screen. WS-014.B extends it: death adds a "Try Again" button (fresh run),
    /// victory adds a reveal-placeholder subtitle. The Try-Again button and subtitle are
    /// code-built in Awake (L-014) so no extra Inspector wiring is required.
    /// </summary>
    public class RunEndOverlayController : MonoBehaviour
    {
        [SerializeField] private GameObject _overlayPanel;
        [SerializeField] private TextMeshProUGUI _headerLabel;
        [SerializeField] private Button _returnButton;

        // Code-built (WS-014.B). Non-serialized — null after a domain reload, hence the
        // OnEnable guard; that accepts a Play restart for hot reload, per L-014.
        private Button _tryAgainButton;
        private TextMeshProUGUI _subtitleText;

        private void Awake()
        {
            BuildExtensions();
        }

        private void OnEnable()
        {
            if (_tryAgainButton == null) return; // Awake hasn't run (hot reload) — restart Play.

            if (EventBus.Instance != null)
                EventBus.Instance.Subscribe<RunEndedEvent>(HandleRunEnded);

            if (_returnButton != null)
                _returnButton.onClick.AddListener(HandleReturnClicked);
            _tryAgainButton.onClick.AddListener(HandleTryAgainClicked);

            if (_overlayPanel != null) _overlayPanel.SetActive(false);
        }

        private void OnDisable()
        {
            if (_tryAgainButton == null) return;

            if (EventBus.Instance != null)
                EventBus.Instance.Unsubscribe<RunEndedEvent>(HandleRunEnded);

            if (_returnButton != null)
                _returnButton.onClick.RemoveListener(HandleReturnClicked);
            _tryAgainButton.onClick.RemoveListener(HandleTryAgainClicked);
        }

        private void HandleRunEnded(RunEndedEvent evt)
        {
            if (_overlayPanel != null) _overlayPanel.SetActive(true);

            if (evt.Victory)
            {
                if (_headerLabel != null) _headerLabel.text = "VICTORY";
                _tryAgainButton.gameObject.SetActive(false);
                _subtitleText.gameObject.SetActive(true);
                _subtitleText.text = "[Round 30 Companion Reveal would play here — see GDD §7]";
            }
            else
            {
                int finalRound = RunManager.Instance != null ? RunManager.Instance.CurrentRound : 0;
                if (_headerLabel != null) _headerLabel.text = $"RUN ENDED — Round {finalRound}";
                _tryAgainButton.gameObject.SetActive(true);
                _subtitleText.gameObject.SetActive(false);
            }

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

        private void HandleTryAgainClicked()
        {
            Time.timeScale = 1f;
            // SaveManager already deletes the run save on RunEndedEvent; this is a defensive
            // idempotent repeat so a retry can never resume into stale state.
            SaveManager.Instance?.DeleteRunSave();
            if (GameManager.Instance != null)
                GameManager.Instance.TransitionTo(GameManager.GameState.InRun);
        }

        // ====================================================================
        // UI EXTENSIONS (Awake only — L-014). Built into the existing scene panel
        // and positioned relative to the scene-authored header / return button.
        // ====================================================================

        private void BuildExtensions()
        {
            Transform panel = _overlayPanel != null ? _overlayPanel.transform : transform;

            // Relabel the scene-authored return button to "Main Menu".
            if (_returnButton != null)
            {
                var returnLabel = _returnButton.GetComponentInChildren<TextMeshProUGUI>();
                if (returnLabel != null) returnLabel.text = "Main Menu";
            }

            // Victory subtitle — placed just below the header.
            _subtitleText = AddText(panel, string.Empty, 20f, FontStyles.Italic);
            var subRT = _subtitleText.rectTransform;
            if (_headerLabel != null)
            {
                var hdrRT = _headerLabel.rectTransform;
                subRT.anchorMin = hdrRT.anchorMin;
                subRT.anchorMax = hdrRT.anchorMax;
                subRT.pivot = hdrRT.pivot;
                subRT.sizeDelta = new Vector2(Mathf.Max(hdrRT.sizeDelta.x, 600f), 120f);
                subRT.anchoredPosition = hdrRT.anchoredPosition + new Vector2(0f, -(hdrRT.sizeDelta.y * 0.5f + 80f));
            }
            else
            {
                subRT.anchorMin = subRT.anchorMax = subRT.pivot = new Vector2(0.5f, 0.5f);
                subRT.sizeDelta = new Vector2(600f, 120f);
            }
            _subtitleText.alignment = TextAlignmentOptions.Center;
            _subtitleText.gameObject.SetActive(false);

            // Try-Again button — placed just above the return button.
            _tryAgainButton = BuildButton(panel, "Try Again");
            var taRT = _tryAgainButton.GetComponent<RectTransform>();
            if (_returnButton != null)
            {
                var retRT = _returnButton.GetComponent<RectTransform>();
                taRT.anchorMin = retRT.anchorMin;
                taRT.anchorMax = retRT.anchorMax;
                taRT.pivot = retRT.pivot;
                taRT.sizeDelta = retRT.sizeDelta;
                taRT.anchoredPosition = retRT.anchoredPosition + new Vector2(0f, retRT.sizeDelta.y + 14f);
            }
            else
            {
                taRT.anchorMin = taRT.anchorMax = taRT.pivot = new Vector2(0.5f, 0.5f);
                taRT.sizeDelta = new Vector2(240f, 48f);
            }
            _tryAgainButton.gameObject.SetActive(false);
        }

        private static Button BuildButton(Transform parent, string label)
        {
            var go = new GameObject($"Btn_{label}", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, worldPositionStays: false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.30f, 0.42f, 0.65f);
            var btn = go.AddComponent<Button>();

            var lbl = AddText(go.transform, label, 18f, FontStyles.Bold);
            var lblRT = lbl.rectTransform;
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero;
            lblRT.offsetMax = Vector2.zero;
            lbl.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        private static TextMeshProUGUI AddText(Transform parent, string text, float fontSize, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, worldPositionStays: false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }
    }
}
