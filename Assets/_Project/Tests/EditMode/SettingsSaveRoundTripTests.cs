using System.IO;
using System.Text.RegularExpressions;
using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HellpitRampage.Tests
{
    public class SettingsSaveRoundTripTests
    {
        private GameObject _go;
        private SaveManager _saveManager;
        private string _testSettingsPath;

        [SetUp]
        public void SetUp()
        {
            _testSettingsPath = Path.Combine(Application.temporaryCachePath, "settings_test.json");
            if (File.Exists(_testSettingsPath))
            {
                File.Delete(_testSettingsPath);
            }

            _go = new GameObject("SaveManagerTestHost");
            _saveManager = _go.AddComponent<SaveManager>();
            _saveManager._settingsPathOverride = _testSettingsPath;
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_testSettingsPath))
            {
                File.Delete(_testSettingsPath);
            }

            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
            _go = null;
            _saveManager = null;
        }

        [Test]
        public void SaveSettings_ThenLoadSettings_PreservesAllFields()
        {
            var written = new SettingsSaveData
            {
                MasterVolume = 0.42f,
                MusicVolume = 0.33f,
                SfxVolume = 0.77f,
                VoiceVolume = 0.55f,
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

            _saveManager.SaveSettings(written);
            var read = _saveManager.LoadSettings();

            Assert.IsNotNull(read);
            Assert.AreEqual(0.42f, read.MasterVolume, 0.0001f);
            Assert.AreEqual(0.33f, read.MusicVolume, 0.0001f);
            Assert.AreEqual(0.77f, read.SfxVolume, 0.0001f);
            Assert.AreEqual(0.55f, read.VoiceVolume, 0.0001f);
            Assert.IsTrue(read.MuteMaster);
            Assert.IsFalse(read.MuteMusic);
            Assert.IsTrue(read.MuteSfx);
            Assert.IsFalse(read.MuteVoice);
            Assert.AreEqual(1280, read.ResolutionWidth);
            Assert.AreEqual(720, read.ResolutionHeight);
            Assert.AreEqual(2, read.FullScreenMode);
            Assert.IsFalse(read.VSync);
            Assert.AreEqual(60, read.TargetFramerate);
            Assert.AreEqual(1, read.ScreenShakeIntensity);
            Assert.IsTrue(read.ReduceMotion);
            Assert.IsTrue(read.HighContrast);
        }

        [Test]
        public void LoadSettings_WhenFileMissing_ReturnsDefaults()
        {
            Assert.IsFalse(File.Exists(_testSettingsPath), "Pre-condition: test settings file must not exist.");

            var loaded = _saveManager.LoadSettings();

            Assert.IsNotNull(loaded);
            // Defaults should match SettingsSaveData's initializers.
            var defaults = new SettingsSaveData();
            Assert.AreEqual(defaults.MasterVolume, loaded.MasterVolume);
            Assert.AreEqual(defaults.ResolutionWidth, loaded.ResolutionWidth);
            Assert.AreEqual(defaults.VSync, loaded.VSync);
            Assert.AreEqual(defaults.ScreenShakeIntensity, loaded.ScreenShakeIntensity);
        }

        [Test]
        public void LoadSettings_WhenFileCorrupt_ReturnsDefaultsAndLogsError()
        {
            File.WriteAllText(_testSettingsPath, "{ this is not valid json :::");

            LogAssert.Expect(LogType.Error, new Regex("failed to load settings"));
            var loaded = _saveManager.LoadSettings();

            Assert.IsNotNull(loaded);
            var defaults = new SettingsSaveData();
            Assert.AreEqual(defaults.MasterVolume, loaded.MasterVolume);
            Assert.IsTrue(File.Exists(_testSettingsPath), "Corrupt file should be left in place for debugging.");
        }

        [Test]
        public void SettingsFileExists_BeforeAndAfterSave()
        {
            Assert.IsFalse(_saveManager.SettingsFileExists(), "Settings file should not exist before save.");

            _saveManager.SaveSettings(new SettingsSaveData());

            Assert.IsTrue(_saveManager.SettingsFileExists(), "Settings file should exist after save.");
        }
    }
}
