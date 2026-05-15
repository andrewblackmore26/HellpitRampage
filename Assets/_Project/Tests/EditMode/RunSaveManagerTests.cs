using System.IO;
using System.Text.RegularExpressions;
using HellpitRampage.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-013: file-IO-level tests for SaveManager's run save lifecycle. Uses a per-test
    /// temp path so the real persistentDataPath/run_save.json is never touched.
    /// </summary>
    public class RunSaveManagerTests
    {
        private GameObject _go;
        private SaveManager _saveManager;
        private string _testRunSavePath;

        [SetUp]
        public void SetUp()
        {
            _testRunSavePath = Path.Combine(Application.temporaryCachePath, "run_save_test.json");
            if (File.Exists(_testRunSavePath))
            {
                File.Delete(_testRunSavePath);
            }

            _go = new GameObject("SaveManagerTestHost");
            _saveManager = _go.AddComponent<SaveManager>();
            _saveManager._runSavePathOverride = _testRunSavePath;
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_testRunSavePath))
            {
                File.Delete(_testRunSavePath);
            }

            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
            _go = null;
            _saveManager = null;
        }

        [Test]
        public void HasRunSave_WhenFileMissing_ReturnsFalse()
        {
            Assert.IsFalse(_saveManager.HasRunSave());
        }

        [Test]
        public void HasRunSave_AfterValidSave_ReturnsTrue()
        {
            var data = new RunSaveData { SaveFormatVersion = 1, CurrentRound = 3, HeroId = "default_hero" };
            _saveManager.SaveRun(data);

            Assert.IsTrue(_saveManager.HasRunSave());
        }

        [Test]
        public void HasRunSave_WithWrongSchemaVersion_ReturnsFalse()
        {
            var data = new RunSaveData { SaveFormatVersion = 999, CurrentRound = 3, HeroId = "default_hero" };
            File.WriteAllText(_testRunSavePath, JsonConvert.SerializeObject(data));

            Assert.IsFalse(_saveManager.HasRunSave());
        }

        [Test]
        public void HasRunSave_WithCorruptFile_ReturnsFalse()
        {
            File.WriteAllText(_testRunSavePath, "{ not valid json :::");

            Assert.IsFalse(_saveManager.HasRunSave());
        }

        [Test]
        public void DeleteRunSave_RemovesFile()
        {
            File.WriteAllText(_testRunSavePath, "{}");
            Assert.IsTrue(File.Exists(_testRunSavePath));

            _saveManager.DeleteRunSave();

            Assert.IsFalse(File.Exists(_testRunSavePath));
        }

        [Test]
        public void LoadRun_WhenFileMissing_ReturnsNull()
        {
            Assert.IsNull(_saveManager.LoadRun());
        }

        [Test]
        public void LoadRun_WithWrongSchemaVersion_ReturnsNullAndWarns()
        {
            var data = new RunSaveData { SaveFormatVersion = 42, CurrentRound = 5, HeroId = "default_hero" };
            File.WriteAllText(_testRunSavePath, JsonConvert.SerializeObject(data));

            LogAssert.Expect(LogType.Warning, new Regex("schema version 42 does not match"));
            var loaded = _saveManager.LoadRun();

            Assert.IsNull(loaded);
        }

        [Test]
        public void SaveRun_ThenLoadRun_RoundTripsContent()
        {
            var written = new RunSaveData
            {
                SaveFormatVersion = 1,
                GameVersion = "test",
                SavedAtUtc = "2026-05-16T00:00:00Z",
                CurrentRound = 11,
                HeroId = "default_hero",
                Gold = 42,
                PlayerCurrentHp = 80f,
                PlayerMaxHp = 100f,
            };
            written.Bags.Add(new BagSaveEntry { BagId = "warden_pouch", OriginX = 1, OriginY = 1, IsLocked = true });
            written.Items.Add(new ItemSaveEntry { ItemId = "bone_knife", OriginX = 2, OriginY = 2, Rotation = 90, IsLocked = false });
            written.GroundItems.Add(new GroundItemSaveEntry { ItemId = "whetstone", Rotation = 0, IsLocked = true });

            _saveManager.SaveRun(written);
            var read = _saveManager.LoadRun();

            Assert.IsNotNull(read);
            Assert.AreEqual(11, read.CurrentRound);
            Assert.AreEqual("default_hero", read.HeroId);
            Assert.AreEqual(42, read.Gold);
            Assert.AreEqual(80f, read.PlayerCurrentHp, 0.0001f);
            Assert.AreEqual(100f, read.PlayerMaxHp, 0.0001f);
            Assert.AreEqual(1, read.Bags.Count);
            Assert.AreEqual("warden_pouch", read.Bags[0].BagId);
            Assert.IsTrue(read.Bags[0].IsLocked);
            Assert.AreEqual(1, read.Items.Count);
            Assert.AreEqual("bone_knife", read.Items[0].ItemId);
            Assert.AreEqual(90, read.Items[0].Rotation);
            Assert.AreEqual(1, read.GroundItems.Count);
            Assert.AreEqual("whetstone", read.GroundItems[0].ItemId);
            Assert.IsTrue(read.GroundItems[0].IsLocked);
        }

        [Test]
        public void SaveRun_RemovesTempFileOnSuccess()
        {
            var data = new RunSaveData { SaveFormatVersion = 1, CurrentRound = 1, HeroId = "default_hero" };
            _saveManager.SaveRun(data);

            // Atomic write: temp file should not survive a successful rename.
            Assert.IsFalse(File.Exists(_testRunSavePath + ".tmp"));
            Assert.IsTrue(File.Exists(_testRunSavePath));
        }
    }
}
