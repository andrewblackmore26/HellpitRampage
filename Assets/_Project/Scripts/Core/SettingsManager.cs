using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace HellpitRampage.Core
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        public const float SilenceDb = -80f;
        public const float MinLinearForLog = 0.0001f;

        [SerializeField] private AudioMixer _mainMixer;

        public SettingsState State { get; private set; }

        private bool _initialized;
        private Coroutine _confirmRevertCo;
        private bool _pendingConfirmed;
        private int _pendingPrevWidth;
        private int _pendingPrevHeight;
        private FullScreenMode _pendingPrevMode;
        private Action<float> _confirmRevertProgress;
        private Action _confirmRevertOnRevert;
        private Action _confirmRevertOnCommit;

        public bool HasPendingResolutionConfirm => _confirmRevertCo != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // Guarded: DontDestroyOnLoad is play-mode-only and throws when this singleton
            // is instantiated in an EditMode test.
            if (Application.isPlaying) DontDestroyOnLoad(transform.root.gameObject);
            EnsureInitialized();
            ApplyAll();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            SettingsSaveData data = null;
            if (SaveManager.Instance != null)
            {
                data = SaveManager.Instance.LoadSettings();
            }

            State = data != null ? SettingsState.FromSaveData(data) : new SettingsState();
            _initialized = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyVolumes();
        }

        // --- Public setters ---

        public void SetMasterVolume(float v)
        {
            EnsureInitialized();
            State.MasterVolume = Mathf.Clamp01(v);
            ApplyVolume("Volume_Master", State.MasterVolume, State.MuteMaster);
            Persist();
            Publish(SettingKind.MasterVolume);
        }

        public void SetMusicVolume(float v)
        {
            EnsureInitialized();
            State.MusicVolume = Mathf.Clamp01(v);
            ApplyVolume("Volume_Music", State.MusicVolume, State.MuteMusic);
            Persist();
            Publish(SettingKind.MusicVolume);
        }

        public void SetSfxVolume(float v)
        {
            EnsureInitialized();
            State.SfxVolume = Mathf.Clamp01(v);
            ApplyVolume("Volume_SFX", State.SfxVolume, State.MuteSfx);
            Persist();
            Publish(SettingKind.SfxVolume);
        }

        public void SetVoiceVolume(float v)
        {
            EnsureInitialized();
            State.VoiceVolume = Mathf.Clamp01(v);
            ApplyVolume("Volume_Voice", State.VoiceVolume, State.MuteVoice);
            Persist();
            Publish(SettingKind.VoiceVolume);
        }

        public void SetMuteMaster(bool muted)
        {
            EnsureInitialized();
            State.MuteMaster = muted;
            ApplyVolume("Volume_Master", State.MasterVolume, State.MuteMaster);
            Persist();
            Publish(SettingKind.MuteMaster);
        }

        public void SetMuteMusic(bool muted)
        {
            EnsureInitialized();
            State.MuteMusic = muted;
            ApplyVolume("Volume_Music", State.MusicVolume, State.MuteMusic);
            Persist();
            Publish(SettingKind.MuteMusic);
        }

        public void SetMuteSfx(bool muted)
        {
            EnsureInitialized();
            State.MuteSfx = muted;
            ApplyVolume("Volume_SFX", State.SfxVolume, State.MuteSfx);
            Persist();
            Publish(SettingKind.MuteSfx);
        }

        public void SetMuteVoice(bool muted)
        {
            EnsureInitialized();
            State.MuteVoice = muted;
            ApplyVolume("Volume_Voice", State.VoiceVolume, State.MuteVoice);
            Persist();
            Publish(SettingKind.MuteVoice);
        }

        public void SetVSync(bool on)
        {
            EnsureInitialized();
            State.VSync = on;
            QualitySettings.vSyncCount = on ? 1 : 0;
            Persist();
            Publish(SettingKind.VSync);
        }

        public void SetTargetFramerate(int fps)
        {
            EnsureInitialized();
            State.TargetFramerate = fps;
            Application.targetFrameRate = fps;
            Persist();
            Publish(SettingKind.Framerate);
        }

        public void SetScreenShakeIntensity(int level)
        {
            EnsureInitialized();
            State.ScreenShakeIntensity = Mathf.Clamp(level, 0, 2);
            Persist();
            Publish(SettingKind.ScreenShake);
        }

        public void SetReduceMotion(bool on)
        {
            EnsureInitialized();
            State.ReduceMotion = on;
            Persist();
            Publish(SettingKind.ReduceMotion);
        }

        public void SetHighContrast(bool on)
        {
            EnsureInitialized();
            State.HighContrast = on;
            Persist();
            Publish(SettingKind.HighContrast);
        }

        /// <summary>
        /// Apply resolution + fullscreen change with a confirm-revert window.
        /// Coroutine runs on this persistent singleton so it survives the menu closing.
        /// `onTick(remainingSeconds)` is fired once per second for UI countdown.
        /// </summary>
        public void SetResolutionWithConfirm(
            int width,
            int height,
            FullScreenMode mode,
            float confirmWindowSeconds,
            Action<float> onTick,
            Action onCommit,
            Action onRevert)
        {
            EnsureInitialized();

            // Cancel any in-flight revert (treat as implicit commit of the previous attempt).
            if (_confirmRevertCo != null)
            {
                StopCoroutine(_confirmRevertCo);
                _confirmRevertCo = null;
            }

            _pendingPrevWidth = Screen.width;
            _pendingPrevHeight = Screen.height;
            _pendingPrevMode = Screen.fullScreenMode;

            State.ResolutionWidth = width;
            State.ResolutionHeight = height;
            State.FullScreenMode = (int)mode;
            Screen.SetResolution(width, height, mode);

            _pendingConfirmed = false;
            _confirmRevertProgress = onTick;
            _confirmRevertOnRevert = onRevert;
            _confirmRevertOnCommit = onCommit;
            _confirmRevertCo = StartCoroutine(ConfirmRevertCountdown(confirmWindowSeconds));
        }

        public void ConfirmPendingResolution()
        {
            if (_confirmRevertCo == null) return;
            _pendingConfirmed = true;
        }

        private IEnumerator ConfirmRevertCountdown(float seconds)
        {
            float remaining = seconds;
            while (remaining > 0f && !_pendingConfirmed)
            {
                _confirmRevertProgress?.Invoke(remaining);
                yield return new WaitForSecondsRealtime(1f);
                remaining -= 1f;
            }

            if (_pendingConfirmed)
            {
                Persist();
                _confirmRevertOnCommit?.Invoke();
            }
            else
            {
                // Revert.
                State.ResolutionWidth = _pendingPrevWidth;
                State.ResolutionHeight = _pendingPrevHeight;
                State.FullScreenMode = (int)_pendingPrevMode;
                Screen.SetResolution(_pendingPrevWidth, _pendingPrevHeight, _pendingPrevMode);
                Persist();
                _confirmRevertOnRevert?.Invoke();
            }

            _confirmRevertCo = null;
            _confirmRevertProgress = null;
            _confirmRevertOnRevert = null;
            _confirmRevertOnCommit = null;
        }

        // --- Internals ---

        private void ApplyAll()
        {
            ApplyVolumes();
            QualitySettings.vSyncCount = State.VSync ? 1 : 0;
            Application.targetFrameRate = State.TargetFramerate;
        }

        private void ApplyVolumes()
        {
            ApplyVolume("Volume_Master", State.MasterVolume, State.MuteMaster);
            ApplyVolume("Volume_Music", State.MusicVolume, State.MuteMusic);
            ApplyVolume("Volume_SFX", State.SfxVolume, State.MuteSfx);
            ApplyVolume("Volume_Voice", State.VoiceVolume, State.MuteVoice);
        }

        private void ApplyVolume(string parameter, float linear, bool muted)
        {
            if (_mainMixer == null) return;
            float db = LinearToDecibels(muted ? 0f : linear);
            _mainMixer.SetFloat(parameter, db);
        }

        public static float LinearToDecibels(float linear)
        {
            if (linear <= MinLinearForLog) return SilenceDb;
            return Mathf.Log10(linear) * 20f;
        }

        private void Persist()
        {
            if (SaveManager.Instance == null) return;
            SaveManager.Instance.SaveSettings(State.ToSaveData());
        }

        private void Publish(SettingKind kind)
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Publish(new SettingsChangedEvent { Kind = kind });
        }
    }
}
