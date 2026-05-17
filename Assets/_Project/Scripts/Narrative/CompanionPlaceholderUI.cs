using System.Collections;
using HellpitRampage.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HellpitRampage.Narrative
{
    /// <summary>
    /// WS-014.B: placeholder companion appearance. Shows a coloured portrait rectangle
    /// plus a subtitle line at round start, holds for a short grace period, then
    /// publishes <see cref="CompanionAppearanceCompleteEvent"/>. Real dialogue, voice
    /// and art replace this later. UI is code-built in Awake (L-014).
    /// </summary>
    public class CompanionPlaceholderUI : MonoBehaviour
    {
        [SerializeField] private float _gracePeriodSeconds = 2f;

        private GameObject _root;
        private Image _portraitRect;
        private TextMeshProUGUI _portraitLabel;
        private TextMeshProUGUI _subtitleText;

        private Coroutine _activeRoutine;

        private void Awake()
        {
            BuildUI();
            _root.SetActive(false);
        }

        /// <summary>
        /// Displays the companion for the given round/state. <paramref name="isGlitch"/>
        /// drives the round-22 corruption effect. Publishes
        /// <see cref="CompanionAppearanceCompleteEvent"/> when the grace period ends or
        /// the player dismisses early.
        /// </summary>
        public void Show(int round, string state, bool isGlitch)
        {
            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(ShowRoutine(round, state, isGlitch));
        }

        private IEnumerator ShowRoutine(int round, string state, bool isGlitch)
        {
            _root.SetActive(true);
            Color stateColor = ColorForState(state);
            _portraitRect.color = stateColor;
            _portraitLabel.text = $"Companion ({state})";
            string normalSubtitle = $"Round {round} dialogue would appear here. State: {state}";
            _subtitleText.text = normalSubtitle;

            float elapsed = 0f;
            float corruptionTimer = 0f;
            while (elapsed < _gracePeriodSeconds)
            {
                // Yield first: the click/key that advanced the round is still flagged
                // "this frame" when Show() is called, so checking dismiss before a
                // frame passes would close the companion instantly. Glitch + dismiss
                // are evaluated from the second frame on.
                yield return null;
                elapsed += Time.unscaledDeltaTime;

                if (isGlitch)
                {
                    _portraitRect.color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f);
                    corruptionTimer += Time.unscaledDeltaTime;
                    if (corruptionTimer >= 0.2f)
                    {
                        _subtitleText.text = Random.value < 0.5f
                            ? CorruptText(normalSubtitle)
                            : normalSubtitle;
                        corruptionTimer = 0f;
                    }
                }

                if (DidPlayerDismiss()) break;
            }

            _root.SetActive(false);
            _activeRoutine = null;
            EventBus.Instance?.Publish(new CompanionAppearanceCompleteEvent());
        }

        private static bool DidPlayerDismiss()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            return false;
        }

        private static Color ColorForState(string state)
        {
            return state switch
            {
                "Composed" => new Color(0.6f, 0.6f, 0.7f),
                "Concerned" => new Color(0.5f, 0.5f, 0.6f),
                "Pleading" => new Color(0.5f, 0.4f, 0.5f),
                "Glitched" => new Color(0.7f, 0.3f, 0.7f),
                "Devil" => new Color(0.7f, 0.2f, 0.2f),
                "Final" => new Color(0.4f, 0.1f, 0.1f),
                _ => Color.grey,
            };
        }

        private static string CorruptText(string s)
        {
            return s
                .Replace("o", "0").Replace("O", "0")
                .Replace("e", "3").Replace("E", "3")
                .Replace("a", "@").Replace("A", "@")
                .Replace("i", "!").Replace("I", "!");
        }

        // ====================================================================
        // UI TREE CONSTRUCTION (Awake only — L-014)
        // ====================================================================

        private void BuildUI()
        {
            _root = NewChild(transform, "Root");
            StretchFull(_root.GetComponent<RectTransform>());

            // Subtitle strip — dark backing for readability over an arbitrary backdrop.
            var subtitleBacking = NewChild(_root.transform, "SubtitleBacking");
            var sbRT = subtitleBacking.GetComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(0.5f, 0f);
            sbRT.anchorMax = new Vector2(0.5f, 0f);
            sbRT.pivot = new Vector2(0.5f, 0f);
            sbRT.sizeDelta = new Vector2(1000f, 90f);
            sbRT.anchoredPosition = new Vector2(0f, 110f);
            AddImage(subtitleBacking, new Color(0f, 0f, 0f, 0.72f));

            _subtitleText = AddText(subtitleBacking, string.Empty, 22f, FontStyles.Normal);
            StretchFull(_subtitleText.GetComponent<RectTransform>());
            _subtitleText.alignment = TextAlignmentOptions.Center;
            _subtitleText.margin = new Vector4(24f, 8f, 24f, 8f);

            // Portrait — colour rectangle bottom-right, sitting above the subtitle strip.
            var portrait = NewChild(_root.transform, "Portrait");
            var pRT = portrait.GetComponent<RectTransform>();
            pRT.anchorMin = new Vector2(1f, 0f);
            pRT.anchorMax = new Vector2(1f, 0f);
            pRT.pivot = new Vector2(1f, 0f);
            pRT.sizeDelta = new Vector2(200f, 200f);
            pRT.anchoredPosition = new Vector2(-40f, 220f);
            _portraitRect = AddImage(portrait, Color.grey);

            _portraitLabel = AddText(portrait, string.Empty, 18f, FontStyles.Bold);
            StretchFull(_portraitLabel.GetComponent<RectTransform>());
            _portraitLabel.alignment = TextAlignmentOptions.Center;
            _portraitLabel.margin = new Vector4(8f, 8f, 8f, 8f);
        }

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
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI AddText(GameObject parent, string text, float fontSize, FontStyles style)
        {
            var go = NewChild(parent.transform, "Text");
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
