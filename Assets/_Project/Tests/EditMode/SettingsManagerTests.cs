using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    public class SettingsManagerTests
    {
        [Test]
        public void LinearToDecibels_Zero_ReturnsSilenceFloor()
        {
            float db = SettingsManager.LinearToDecibels(0f);
            Assert.AreEqual(SettingsManager.SilenceDb, db, 0.001f);
        }

        [Test]
        public void LinearToDecibels_NegativeOrTinyValue_ReturnsSilenceFloor()
        {
            // Sub-threshold values must clamp to silence to avoid -infinity / NaN.
            Assert.AreEqual(SettingsManager.SilenceDb, SettingsManager.LinearToDecibels(-0.5f), 0.001f);
            Assert.AreEqual(SettingsManager.SilenceDb, SettingsManager.LinearToDecibels(0.00001f), 0.001f);
        }

        [Test]
        public void LinearToDecibels_One_ReturnsZeroDb()
        {
            float db = SettingsManager.LinearToDecibels(1f);
            Assert.AreEqual(0f, db, 0.001f);
        }

        [Test]
        public void LinearToDecibels_Half_ReturnsApproxMinusSixDb()
        {
            // log10(0.5) * 20 ≈ -6.02
            float db = SettingsManager.LinearToDecibels(0.5f);
            Assert.AreEqual(-6.02f, db, 0.05f);
        }

        [Test]
        public void SettingsState_Defaults_AreReasonable()
        {
            var s = new SettingsState();
            Assert.AreEqual(0.8f, s.MasterVolume);
            Assert.AreEqual(0.7f, s.MusicVolume);
            Assert.AreEqual(0.85f, s.SfxVolume);
            Assert.AreEqual(0.9f, s.VoiceVolume);
            Assert.IsFalse(s.MuteMaster);
            Assert.AreEqual(1920, s.ResolutionWidth);
            Assert.AreEqual(1080, s.ResolutionHeight);
            Assert.AreEqual(0, s.FullScreenMode); // Borderless / FullScreenWindow
            Assert.IsTrue(s.VSync);
            Assert.AreEqual(-1, s.TargetFramerate); // Uncapped
            Assert.AreEqual(2, s.ScreenShakeIntensity); // Normal
            Assert.IsFalse(s.ReduceMotion);
            Assert.IsFalse(s.HighContrast);
        }

        [Test]
        public void SettingsSaveData_Defaults_MatchState()
        {
            // Both DTOs should expose the same defaults — drift between them
            // would silently change persisted vs. runtime initial values.
            var save = new SettingsSaveData();
            var state = new SettingsState();
            Assert.AreEqual(save.MasterVolume, state.MasterVolume);
            Assert.AreEqual(save.ResolutionWidth, state.ResolutionWidth);
            Assert.AreEqual(save.FullScreenMode, state.FullScreenMode);
            Assert.AreEqual(save.VSync, state.VSync);
            Assert.AreEqual(save.ScreenShakeIntensity, state.ScreenShakeIntensity);
        }

        [Test]
        public void SettingsState_ToSaveData_RoundTrip_PreservesAllFields()
        {
            var original = new SettingsState
            {
                MasterVolume = 0.3f,
                MusicVolume = 0.4f,
                SfxVolume = 0.55f,
                VoiceVolume = 0.65f,
                MuteMaster = true,
                MuteMusic = false,
                MuteSfx = true,
                MuteVoice = false,
                ResolutionWidth = 1280,
                ResolutionHeight = 720,
                FullScreenMode = 2,
                VSync = false,
                TargetFramerate = 60,
                ScreenShakeIntensity = 1,
                ReduceMotion = true,
                HighContrast = true,
            };

            var saved = original.ToSaveData();
            var restored = SettingsState.FromSaveData(saved);

            Assert.AreEqual(original.MasterVolume, restored.MasterVolume);
            Assert.AreEqual(original.MusicVolume, restored.MusicVolume);
            Assert.AreEqual(original.SfxVolume, restored.SfxVolume);
            Assert.AreEqual(original.VoiceVolume, restored.VoiceVolume);
            Assert.AreEqual(original.MuteMaster, restored.MuteMaster);
            Assert.AreEqual(original.MuteSfx, restored.MuteSfx);
            Assert.AreEqual(original.ResolutionWidth, restored.ResolutionWidth);
            Assert.AreEqual(original.ResolutionHeight, restored.ResolutionHeight);
            Assert.AreEqual(original.FullScreenMode, restored.FullScreenMode);
            Assert.AreEqual(original.VSync, restored.VSync);
            Assert.AreEqual(original.TargetFramerate, restored.TargetFramerate);
            Assert.AreEqual(original.ScreenShakeIntensity, restored.ScreenShakeIntensity);
            Assert.AreEqual(original.ReduceMotion, restored.ReduceMotion);
            Assert.AreEqual(original.HighContrast, restored.HighContrast);
        }
    }
}
