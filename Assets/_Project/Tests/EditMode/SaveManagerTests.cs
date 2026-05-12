using System.IO;
using System.Text.RegularExpressions;
using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HellpitRampage.Tests
{
    public class SaveManagerTests
    {
        private GameObject _go;
        private SaveManager _saveManager;
        private string _testSavePath;

        [SetUp]
        public void SetUp()
        {
            _testSavePath = Path.Combine(Application.temporaryCachePath, "save_test.json");
            if (File.Exists(_testSavePath))
            {
                File.Delete(_testSavePath);
            }

            _go = new GameObject("SaveManagerTestHost");
            _saveManager = _go.AddComponent<SaveManager>();
            _saveManager._savePathOverride = _testSavePath;
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_testSavePath))
            {
                File.Delete(_testSavePath);
            }

            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
            _go = null;
            _saveManager = null;
        }

        [Test]
        public void Save_ThenLoad_ReturnsEquivalentData()
        {
            SaveData data = new SaveData { SchemaVersion = 7 };
            _saveManager.Save(data);

            LogAssert.Expect(LogType.Warning, new Regex("Save schema version mismatch: 7"));
            SaveData loaded = _saveManager.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(7, loaded.SchemaVersion);
        }

        [Test]
        public void Load_WhenFileMissing_ReturnsFreshSaveData()
        {
            Assert.IsFalse(File.Exists(_testSavePath), "Pre-condition: test save file must not exist.");

            SaveData loaded = _saveManager.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.SchemaVersion);
        }

        [Test]
        public void SaveFileExists_BeforeAndAfterSave()
        {
            Assert.IsFalse(_saveManager.SaveFileExists(), "Save file should not exist before Save.");

            _saveManager.Save(new SaveData());

            Assert.IsTrue(_saveManager.SaveFileExists(), "Save file should exist after Save.");
        }
    }
}
