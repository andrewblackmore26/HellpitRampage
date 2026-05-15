using System;
using System.Collections.Generic;
using HellpitRampage.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// Settings menu controller. Builds the entire UI tree in Awake (L-014:
    /// code-built UI must happen in Awake, never OnEnable, or hot-reload duplicates
    /// children). OnEnable only re-syncs UI from SettingsManager state and wires
    /// listeners; OnDisable unwires.
    /// Attach to a scene-level GameObject under a Canvas. Caller invokes Open(onClose).
    /// </summary>
    public class SettingsMenuController : MonoBehaviour
    {
        private enum Tab { Audio, Display, Accessibility, Controls }

        private const float ConfirmRevertSeconds = 10f;

        // Root + tab plumbing
        private GameObject _root;
        private Dictionary<Tab, Button> _tabButtons = new Dictionary<Tab, Button>();
        private Dictionary<Tab, GameObject> _tabPanels = new Dictionary<Tab, GameObject>();
        private Tab _currentTab = Tab.Audio;

        // Audio tab refs
        private Slider _masterSlider, _musicSlider, _sfxSlider, _voiceSlider;
        private TextMeshProUGUI _masterValue, _musicValue, _sfxValue, _voiceValue;
        private Toggle _muteMaster, _muteMusic, _muteSfx, _muteVoice;

        // Display tab refs (cycler pattern: ◄ value ► — avoids TMP_Dropdown's
        // complex template wiring while preserving functional acceptance).
        private TextMeshProUGUI _resolutionLabel;
        private int _resolutionIndex;
        private List<Resolution> _availableResolutions = new List<Resolution>();
        private TextMeshProUGUI _fullscreenLabel;
        private int _fullscreenIndex;
        private static readonly string[] FullscreenLabels = { "Borderless", "Exclusive", "Windowed" };
        private static readonly FullScreenMode[] FullscreenModes =
        {
            FullScreenMode.FullScreenWindow,
            FullScreenMode.ExclusiveFullScreen,
            FullScreenMode.Windowed,
        };
        private Toggle _vsyncToggle;
        private TextMeshProUGUI _framerateLabel;
        private int _framerateIndex;
        private static readonly string[] FramerateLabels = { "Uncapped", "60", "144" };
        private static readonly int[] FramerateValues = { -1, 60, 144 };
        private Button _applyButton;
        private TextMeshProUGUI _applyHint;

        // Accessibility tab
        private TextMeshProUGUI _shakeLabel;
        private int _shakeIndex;
        private static readonly string[] ShakeLabels = { "Off", "Low", "Normal" };
        private Toggle _reduceMotionToggle;
        private Toggle _highContrastToggle;

        // Confirm-revert overlay
        private GameObject _confirmOverlay;
        private TextMeshProUGUI _confirmCountdownText;
        private Button _confirmKeepButton;

        // Open/close callback
        private Action _onClose;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            BuildUITree();
            _root.SetActive(false);
        }

        private void OnEnable()
        {
            if (_root == null) return; // Awake hasn't run (e.g., disabled by default)
            SyncFromSettings();
            WireListeners();
        }

        private void OnDisable()
        {
            if (_root == null) return;
            UnwireListeners();
        }

        public void Open(Action onClose)
        {
            _onClose = onClose;
            IsOpen = true;
            _root.SetActive(true);
            ShowTab(_currentTab);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _root.SetActive(false);
            Action cb = _onClose;
            _onClose = null;
            cb?.Invoke();
        }

        // --- UI sync ---

        private void SyncFromSettings()
        {
            var s = SettingsManager.Instance?.State;
            if (s == null) return;

            _masterSlider.SetValueWithoutNotify(s.MasterVolume);
            _masterValue.text = $"{Mathf.RoundToInt(s.MasterVolume * 100)}";
            _muteMaster.SetIsOnWithoutNotify(s.MuteMaster);

            _musicSlider.SetValueWithoutNotify(s.MusicVolume);
            _musicValue.text = $"{Mathf.RoundToInt(s.MusicVolume * 100)}";
            _muteMusic.SetIsOnWithoutNotify(s.MuteMusic);

            _sfxSlider.SetValueWithoutNotify(s.SfxVolume);
            _sfxValue.text = $"{Mathf.RoundToInt(s.SfxVolume * 100)}";
            _muteSfx.SetIsOnWithoutNotify(s.MuteSfx);

            _voiceSlider.SetValueWithoutNotify(s.VoiceVolume);
            _voiceValue.text = $"{Mathf.RoundToInt(s.VoiceVolume * 100)}";
            _muteVoice.SetIsOnWithoutNotify(s.MuteVoice);

            PopulateResolutions();
            _resolutionIndex = FindResolutionIndex(s.ResolutionWidth, s.ResolutionHeight);
            RefreshResolutionLabel();

            _fullscreenIndex = Mathf.Clamp(s.FullScreenMode, 0, FullscreenLabels.Length - 1);
            _fullscreenLabel.text = FullscreenLabels[_fullscreenIndex];

            _vsyncToggle.SetIsOnWithoutNotify(s.VSync);

            _framerateIndex = FindFramerateIndex(s.TargetFramerate);
            _framerateLabel.text = FramerateLabels[_framerateIndex];

            _shakeIndex = Mathf.Clamp(s.ScreenShakeIntensity, 0, ShakeLabels.Length - 1);
            _shakeLabel.text = ShakeLabels[_shakeIndex];
            _reduceMotionToggle.SetIsOnWithoutNotify(s.ReduceMotion);
            _highContrastToggle.SetIsOnWithoutNotify(s.HighContrast);

            _applyButton.interactable = false;
            _applyHint.text = "Resolution / fullscreen changes need Apply.";
            _confirmOverlay.SetActive(false);
        }

        // --- Listener wiring ---

        private void WireListeners()
        {
            _masterSlider.onValueChanged.AddListener(OnMasterChanged);
            _musicSlider.onValueChanged.AddListener(OnMusicChanged);
            _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            _voiceSlider.onValueChanged.AddListener(OnVoiceChanged);
            _muteMaster.onValueChanged.AddListener(b => SettingsManager.Instance?.SetMuteMaster(b));
            _muteMusic.onValueChanged.AddListener(b => SettingsManager.Instance?.SetMuteMusic(b));
            _muteSfx.onValueChanged.AddListener(b => SettingsManager.Instance?.SetMuteSfx(b));
            _muteVoice.onValueChanged.AddListener(b => SettingsManager.Instance?.SetMuteVoice(b));

            _vsyncToggle.onValueChanged.AddListener(b => SettingsManager.Instance?.SetVSync(b));
            _applyButton.onClick.AddListener(OnApplyClicked);

            _reduceMotionToggle.onValueChanged.AddListener(b => SettingsManager.Instance?.SetReduceMotion(b));
            _highContrastToggle.onValueChanged.AddListener(b => SettingsManager.Instance?.SetHighContrast(b));
        }

        private void UnwireListeners()
        {
            _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            _musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            _sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            _voiceSlider.onValueChanged.RemoveListener(OnVoiceChanged);
            _muteMaster.onValueChanged.RemoveAllListeners();
            _muteMusic.onValueChanged.RemoveAllListeners();
            _muteSfx.onValueChanged.RemoveAllListeners();
            _muteVoice.onValueChanged.RemoveAllListeners();
            _vsyncToggle.onValueChanged.RemoveAllListeners();
            _applyButton.onClick.RemoveAllListeners();
            _reduceMotionToggle.onValueChanged.RemoveAllListeners();
            _highContrastToggle.onValueChanged.RemoveAllListeners();
        }

        // --- Slider handlers ---

        private void OnMasterChanged(float v)
        {
            SettingsManager.Instance?.SetMasterVolume(v);
            _masterValue.text = $"{Mathf.RoundToInt(v * 100)}";
        }

        private void OnMusicChanged(float v)
        {
            SettingsManager.Instance?.SetMusicVolume(v);
            _musicValue.text = $"{Mathf.RoundToInt(v * 100)}";
        }

        private void OnSfxChanged(float v)
        {
            SettingsManager.Instance?.SetSfxVolume(v);
            _sfxValue.text = $"{Mathf.RoundToInt(v * 100)}";
        }

        private void OnVoiceChanged(float v)
        {
            SettingsManager.Instance?.SetVoiceVolume(v);
            _voiceValue.text = $"{Mathf.RoundToInt(v * 100)}";
        }

        // --- Display tab cyclers ---

        private void CycleResolution(int delta)
        {
            if (_availableResolutions.Count == 0) return;
            _resolutionIndex = (_resolutionIndex + delta + _availableResolutions.Count) % _availableResolutions.Count;
            RefreshResolutionLabel();
            MarkDisplayDirty();
        }

        private void CycleFullscreen(int delta)
        {
            _fullscreenIndex = (_fullscreenIndex + delta + FullscreenLabels.Length) % FullscreenLabels.Length;
            _fullscreenLabel.text = FullscreenLabels[_fullscreenIndex];
            MarkDisplayDirty();
        }

        private void CycleFramerate(int delta)
        {
            _framerateIndex = (_framerateIndex + delta + FramerateLabels.Length) % FramerateLabels.Length;
            _framerateLabel.text = FramerateLabels[_framerateIndex];
            SettingsManager.Instance?.SetTargetFramerate(FramerateValues[_framerateIndex]);
        }

        private void CycleShake(int delta)
        {
            _shakeIndex = (_shakeIndex + delta + ShakeLabels.Length) % ShakeLabels.Length;
            _shakeLabel.text = ShakeLabels[_shakeIndex];
            SettingsManager.Instance?.SetScreenShakeIntensity(_shakeIndex);
        }

        private void MarkDisplayDirty()
        {
            _applyButton.interactable = true;
            _applyHint.text = "Apply to commit resolution / fullscreen.";
        }

        private void RefreshResolutionLabel()
        {
            if (_availableResolutions.Count == 0)
            {
                _resolutionLabel.text = $"{Screen.width} × {Screen.height}";
                return;
            }
            var r = _availableResolutions[_resolutionIndex];
            _resolutionLabel.text = $"{r.width} × {r.height}";
        }

        private void PopulateResolutions()
        {
            _availableResolutions.Clear();
            var seen = new HashSet<long>();
            foreach (var r in Screen.resolutions)
            {
                long key = ((long)r.width << 32) | (uint)r.height;
                if (seen.Add(key))
                {
                    _availableResolutions.Add(r);
                }
            }
            if (_availableResolutions.Count == 0)
            {
                _availableResolutions.Add(new Resolution { width = Screen.width, height = Screen.height });
            }
        }

        private int FindResolutionIndex(int width, int height)
        {
            for (int i = 0; i < _availableResolutions.Count; i++)
            {
                if (_availableResolutions[i].width == width && _availableResolutions[i].height == height)
                    return i;
            }
            return 0;
        }

        private int FindFramerateIndex(int fps)
        {
            for (int i = 0; i < FramerateValues.Length; i++)
            {
                if (FramerateValues[i] == fps) return i;
            }
            return 0;
        }

        // --- Apply / confirm-revert ---

        private void OnApplyClicked()
        {
            if (_availableResolutions.Count == 0) return;
            var r = _availableResolutions[_resolutionIndex];
            var mode = FullscreenModes[_fullscreenIndex];

            _applyButton.interactable = false;
            _confirmOverlay.SetActive(true);

            SettingsManager.Instance?.SetResolutionWithConfirm(
                r.width,
                r.height,
                mode,
                ConfirmRevertSeconds,
                onTick: secs => _confirmCountdownText.text = $"Reverting in {Mathf.CeilToInt(secs)}s",
                onCommit: () =>
                {
                    _confirmOverlay.SetActive(false);
                    _applyHint.text = "Applied.";
                },
                onRevert: () =>
                {
                    _confirmOverlay.SetActive(false);
                    _applyHint.text = "Reverted — change was not confirmed.";
                    SyncFromSettings();
                });
        }

        // --- Tab switching ---

        private void ShowTab(Tab tab)
        {
            _currentTab = tab;
            foreach (var kv in _tabPanels)
            {
                kv.Value.SetActive(kv.Key == tab);
            }
            foreach (var kv in _tabButtons)
            {
                var colors = kv.Value.colors;
                colors.normalColor = kv.Key == tab ? new Color(0.30f, 0.42f, 0.65f) : new Color(0.20f, 0.22f, 0.28f);
                kv.Value.colors = colors;
            }
        }

        // ====================================================================
        // UI TREE CONSTRUCTION (Awake only — L-014)
        // ====================================================================

        private void BuildUITree()
        {
            _root = NewChild(transform, "Root");
            var rootRT = AddRect(_root);
            StretchFull(rootRT);
            AddImage(_root, new Color(0f, 0f, 0f, 0.7f));

            var panel = NewChild(_root.transform, "Panel");
            var panelRT = AddRect(panel);
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(720, 540);
            panelRT.anchoredPosition = Vector2.zero;
            AddImage(panel, new Color(0.10f, 0.12f, 0.16f, 0.98f));
            AddVerticalLayout(panel, 12, new RectOffset(20, 20, 20, 20));

            // Header
            var header = NewChild(panel.transform, "Header");
            AddLayoutElement(header, preferredHeight: 36, flexibleWidth: 1);
            var headerText = AddText(header, "Settings", 22, FontStyles.Bold);
            headerText.alignment = TextAlignmentOptions.Center;

            // Tab bar
            var tabBar = NewChild(panel.transform, "TabBar");
            AddLayoutElement(tabBar, preferredHeight: 36, flexibleWidth: 1);
            AddHorizontalLayout(tabBar, 6, new RectOffset(0, 0, 0, 0));
            _tabButtons[Tab.Audio] = AddTabButton(tabBar, "Audio", () => ShowTab(Tab.Audio));
            _tabButtons[Tab.Display] = AddTabButton(tabBar, "Display", () => ShowTab(Tab.Display));
            _tabButtons[Tab.Accessibility] = AddTabButton(tabBar, "Accessibility", () => ShowTab(Tab.Accessibility));
            _tabButtons[Tab.Controls] = AddTabButton(tabBar, "Controls", () => ShowTab(Tab.Controls));

            // Content area
            var content = NewChild(panel.transform, "Content");
            AddLayoutElement(content, flexibleHeight: 1, flexibleWidth: 1);
            // Each tab panel is the same size; we just toggle Active.
            AddImage(content, new Color(0.06f, 0.07f, 0.09f, 0.6f));

            _tabPanels[Tab.Audio] = BuildAudioTab(content.transform);
            _tabPanels[Tab.Display] = BuildDisplayTab(content.transform);
            _tabPanels[Tab.Accessibility] = BuildAccessibilityTab(content.transform);
            _tabPanels[Tab.Controls] = BuildControlsTab(content.transform);

            // Footer
            var footer = NewChild(panel.transform, "Footer");
            AddLayoutElement(footer, preferredHeight: 40, flexibleWidth: 1);
            AddHorizontalLayout(footer, 8, new RectOffset(0, 0, 0, 0));
            AddPrimaryButton(footer, "Back", () => Close());

            // Confirm-revert overlay (sibling of root content; on top)
            _confirmOverlay = NewChild(_root.transform, "ConfirmOverlay");
            var coRT = AddRect(_confirmOverlay);
            StretchFull(coRT);
            AddImage(_confirmOverlay, new Color(0f, 0f, 0f, 0.85f));
            AddVerticalLayout(_confirmOverlay, 12, new RectOffset(20, 20, 20, 20), childAlignment: TextAnchor.MiddleCenter);
            _confirmCountdownText = AddText(_confirmOverlay, "Reverting in 10s", 20, FontStyles.Bold);
            _confirmCountdownText.alignment = TextAlignmentOptions.Center;
            _confirmKeepButton = AddPrimaryButton(_confirmOverlay, "Keep", () =>
            {
                SettingsManager.Instance?.ConfirmPendingResolution();
            });
            _confirmOverlay.SetActive(false);

            ShowTab(Tab.Audio);
        }

        private GameObject BuildAudioTab(Transform parent)
        {
            var panel = NewChild(parent, "AudioPanel");
            var rt = AddRect(panel);
            StretchFull(rt);
            AddVerticalLayout(panel, 10, new RectOffset(14, 14, 14, 14));
            BuildSliderRow(panel.transform, "Master", out _masterSlider, out _masterValue, out _muteMaster);
            BuildSliderRow(panel.transform, "Music", out _musicSlider, out _musicValue, out _muteMusic);
            BuildSliderRow(panel.transform, "SFX", out _sfxSlider, out _sfxValue, out _muteSfx);
            BuildSliderRow(panel.transform, "Voice", out _voiceSlider, out _voiceValue, out _muteVoice);
            return panel;
        }

        private GameObject BuildDisplayTab(Transform parent)
        {
            var panel = NewChild(parent, "DisplayPanel");
            var rt = AddRect(panel);
            StretchFull(rt);
            AddVerticalLayout(panel, 10, new RectOffset(14, 14, 14, 14));

            BuildCyclerRow(panel.transform, "Resolution", out _resolutionLabel, delta => CycleResolution(delta));
            BuildCyclerRow(panel.transform, "Fullscreen", out _fullscreenLabel, delta => CycleFullscreen(delta));

            var vsyncRow = MakeRow(panel.transform, "VSync");
            AddText(vsyncRow, "VSync", 16);
            _vsyncToggle = BuildToggle(vsyncRow.transform);

            BuildCyclerRow(panel.transform, "Framerate", out _framerateLabel, delta => CycleFramerate(delta));

            var applyRow = MakeRow(panel.transform, "ApplyRow");
            _applyHint = AddText(applyRow, "Resolution / fullscreen changes need Apply.", 14);
            _applyHint.color = new Color(0.7f, 0.7f, 0.75f);
            _applyButton = AddPrimaryButton(applyRow, "Apply", () => { });
            _applyButton.interactable = false;

            return panel;
        }

        private GameObject BuildAccessibilityTab(Transform parent)
        {
            var panel = NewChild(parent, "AccessibilityPanel");
            var rt = AddRect(panel);
            StretchFull(rt);
            AddVerticalLayout(panel, 10, new RectOffset(14, 14, 14, 14));

            BuildCyclerRow(panel.transform, "Screen Shake", out _shakeLabel, delta => CycleShake(delta));

            var reduceRow = MakeRow(panel.transform, "ReduceMotionRow");
            AddText(reduceRow, "Reduce Motion", 16);
            _reduceMotionToggle = BuildToggle(reduceRow.transform);

            var contrastRow = MakeRow(panel.transform, "HighContrastRow");
            AddText(contrastRow, "High Contrast UI", 16);
            _highContrastToggle = BuildToggle(contrastRow.transform);

            return panel;
        }

        private GameObject BuildControlsTab(Transform parent)
        {
            var panel = NewChild(parent, "ControlsPanel");
            var rt = AddRect(panel);
            StretchFull(rt);
            AddVerticalLayout(panel, 6, new RectOffset(14, 14, 14, 14));

            const string bindings =
                "Movement:      WASD  /  Left Stick\n" +
                "Pause:         Esc   /  Start\n" +
                "Confirm:       Enter /  South Button (A / Cross)\n" +
                "Cancel / Back: Esc   /  East Button (B / Circle)\n" +
                "\n" +
                "Note: shop drag-and-drop, rotation, and sell modal require a mouse.\n" +
                "Controller supports menu navigation and combat movement only.";
            var bindingsText = AddText(panel, bindings, 14);
            bindingsText.alignment = TextAlignmentOptions.TopLeft;
            return panel;
        }

        // --- Reusable row builders ---

        private GameObject MakeRow(Transform parent, string name)
        {
            var row = NewChild(parent, name);
            AddLayoutElement(row, preferredHeight: 32, flexibleWidth: 1);
            AddHorizontalLayout(row, 8, new RectOffset(0, 0, 0, 0), childAlignment: TextAnchor.MiddleLeft);
            return row;
        }

        private void BuildSliderRow(Transform parent, string label, out Slider slider, out TextMeshProUGUI valueText, out Toggle mute)
        {
            var row = MakeRow(parent, $"{label}Row");
            var labelText = AddText(row, label, 16);
            AddLayoutElement(labelText.gameObject, preferredWidth: 90);

            slider = BuildSlider(row.transform);
            valueText = AddText(row, "0", 14);
            AddLayoutElement(valueText.gameObject, preferredWidth: 36);
            valueText.alignment = TextAlignmentOptions.Right;

            var muteLabel = AddText(row, "Mute", 12);
            AddLayoutElement(muteLabel.gameObject, preferredWidth: 36);
            mute = BuildToggle(row.transform);
        }

        private void BuildCyclerRow(Transform parent, string label, out TextMeshProUGUI valueLabel, Action<int> onCycle)
        {
            var row = MakeRow(parent, $"{label}Row");
            var labelText = AddText(row, label, 16);
            AddLayoutElement(labelText.gameObject, preferredWidth: 140);

            var left = AddSecondaryButton(row, "<", () => onCycle(-1));
            AddLayoutElement(left.gameObject, preferredWidth: 32, preferredHeight: 28);

            valueLabel = AddText(row, "—", 16);
            valueLabel.alignment = TextAlignmentOptions.Center;
            AddLayoutElement(valueLabel.gameObject, preferredWidth: 200, flexibleWidth: 1);

            var right = AddSecondaryButton(row, ">", () => onCycle(1));
            AddLayoutElement(right.gameObject, preferredWidth: 32, preferredHeight: 28);
        }

        // --- Low-level UI primitives ---

        private static GameObject NewChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        private static RectTransform AddRect(GameObject go) => go.GetComponent<RectTransform>();

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

        private static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing, RectOffset padding, TextAnchor childAlignment = TextAnchor.UpperCenter)
        {
            var vl = go.AddComponent<VerticalLayoutGroup>();
            vl.spacing = spacing;
            vl.padding = padding;
            vl.childAlignment = childAlignment;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            return vl;
        }

        private static HorizontalLayoutGroup AddHorizontalLayout(GameObject go, float spacing, RectOffset padding, TextAnchor childAlignment = TextAnchor.MiddleCenter)
        {
            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing;
            hl.padding = padding;
            hl.childAlignment = childAlignment;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            return hl;
        }

        private static LayoutElement AddLayoutElement(GameObject go, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1, float flexibleHeight = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferredWidth > 0) le.preferredWidth = preferredWidth;
            if (preferredHeight > 0) le.preferredHeight = preferredHeight;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
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
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private static Button AddTabButton(GameObject parent, string label, Action onClick)
        {
            var go = NewChild(parent.transform, $"Tab_{label}");
            AddLayoutElement(go, preferredHeight: 32, flexibleWidth: 1);
            AddImage(go, new Color(0.20f, 0.22f, 0.28f));
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.20f, 0.22f, 0.28f);
            colors.highlightedColor = new Color(0.30f, 0.34f, 0.42f);
            colors.pressedColor = new Color(0.15f, 0.17f, 0.22f);
            colors.selectedColor = new Color(0.30f, 0.34f, 0.42f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());

            var lbl = AddText(go, label, 16);
            lbl.alignment = TextAlignmentOptions.Center;
            var lblRT = lbl.GetComponent<RectTransform>();
            StretchFull(lblRT);
            return btn;
        }

        private static Button AddPrimaryButton(GameObject parent, string label, Action onClick)
        {
            var go = NewChild(parent.transform, $"Btn_{label}");
            AddLayoutElement(go, preferredWidth: 100, preferredHeight: 32);
            AddImage(go, new Color(0.30f, 0.42f, 0.65f));
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var lbl = AddText(go, label, 16, FontStyles.Bold);
            lbl.alignment = TextAlignmentOptions.Center;
            var lblRT = lbl.GetComponent<RectTransform>();
            StretchFull(lblRT);
            return btn;
        }

        private static Button AddSecondaryButton(GameObject parent, string label, Action onClick)
        {
            var go = NewChild(parent.transform, $"Btn_{label}");
            AddImage(go, new Color(0.22f, 0.25f, 0.32f));
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var lbl = AddText(go, label, 16, FontStyles.Bold);
            lbl.alignment = TextAlignmentOptions.Center;
            var lblRT = lbl.GetComponent<RectTransform>();
            StretchFull(lblRT);
            return btn;
        }

        private static Slider BuildSlider(Transform parent)
        {
            var go = NewChild(parent, "Slider");
            AddLayoutElement(go, preferredHeight: 20, flexibleWidth: 1);

            // Background
            var bg = NewChild(go.transform, "Background");
            var bgRT = AddRect(bg);
            bgRT.anchorMin = new Vector2(0f, 0.4f);
            bgRT.anchorMax = new Vector2(1f, 0.6f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            AddImage(bg, new Color(0.15f, 0.17f, 0.22f));

            // Fill area
            var fillArea = NewChild(go.transform, "FillArea");
            var fillAreaRT = AddRect(fillArea);
            fillAreaRT.anchorMin = new Vector2(0f, 0.4f);
            fillAreaRT.anchorMax = new Vector2(1f, 0.6f);
            fillAreaRT.offsetMin = new Vector2(8, 0);
            fillAreaRT.offsetMax = new Vector2(-8, 0);
            var fill = NewChild(fillArea.transform, "Fill");
            var fillRT = AddRect(fill);
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            AddImage(fill, new Color(0.30f, 0.55f, 0.85f));

            // Handle area
            var handleArea = NewChild(go.transform, "HandleSlideArea");
            var handleAreaRT = AddRect(handleArea);
            handleAreaRT.anchorMin = new Vector2(0f, 0f);
            handleAreaRT.anchorMax = new Vector2(1f, 1f);
            handleAreaRT.offsetMin = new Vector2(8, 0);
            handleAreaRT.offsetMax = new Vector2(-8, 0);
            var handle = NewChild(handleArea.transform, "Handle");
            var handleRT = AddRect(handle);
            handleRT.sizeDelta = new Vector2(14, 20);
            AddImage(handle, new Color(0.85f, 0.88f, 0.95f));

            var slider = go.AddComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;
            return slider;
        }

        private static Toggle BuildToggle(Transform parent)
        {
            var go = NewChild(parent, "Toggle");
            AddLayoutElement(go, preferredWidth: 24, preferredHeight: 24);
            AddImage(go, new Color(0.15f, 0.17f, 0.22f));

            var check = NewChild(go.transform, "Checkmark");
            var checkRT = AddRect(check);
            checkRT.anchorMin = new Vector2(0.15f, 0.15f);
            checkRT.anchorMax = new Vector2(0.85f, 0.85f);
            checkRT.offsetMin = Vector2.zero;
            checkRT.offsetMax = Vector2.zero;
            var checkImg = AddImage(check, new Color(0.30f, 0.55f, 0.85f));

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = go.GetComponent<Image>();
            toggle.graphic = checkImg;
            toggle.isOn = false;
            return toggle;
        }
    }
}
