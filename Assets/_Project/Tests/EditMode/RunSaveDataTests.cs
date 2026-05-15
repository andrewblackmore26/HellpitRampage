using HellpitRampage.Core;
using Newtonsoft.Json;
using NUnit.Framework;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-013: pure-DTO serialization tests. Exercise the schema without any file IO or
    /// scene state so a regression in field naming / shape surfaces immediately.
    /// </summary>
    public class RunSaveDataTests
    {
        [Test]
        public void RunSaveData_RoundTrip_PreservesAllFields()
        {
            var original = new RunSaveData
            {
                SaveFormatVersion = 1,
                GameVersion = "0.9.0",
                SavedAtUtc = "2026-05-14T13:45:22Z",
                CurrentRound = 7,
                HeroId = "default_hero",
                Gold = 14,
                PlayerCurrentHp = 87.5f,
                PlayerMaxHp = 120f,
            };
            original.Bags.Add(new BagSaveEntry { BagId = "warden_pouch", OriginX = 0, OriginY = 0, IsLocked = true });
            original.Items.Add(new ItemSaveEntry { ItemId = "bone_knife", OriginX = 1, OriginY = 1, Rotation = 90, IsLocked = false });
            original.Items.Add(new ItemSaveEntry { ItemId = "whetstone", OriginX = 2, OriginY = 1, Rotation = 0, IsLocked = true });
            original.GroundItems.Add(new GroundItemSaveEntry { ItemId = "tarnished_bell", Rotation = 180, IsLocked = false });

            string json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<RunSaveData>(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual(1, restored.SaveFormatVersion);
            Assert.AreEqual("0.9.0", restored.GameVersion);
            Assert.AreEqual("2026-05-14T13:45:22Z", restored.SavedAtUtc);
            Assert.AreEqual(7, restored.CurrentRound);
            Assert.AreEqual("default_hero", restored.HeroId);
            Assert.AreEqual(14, restored.Gold);
            Assert.AreEqual(87.5f, restored.PlayerCurrentHp, 0.0001f);
            Assert.AreEqual(120f, restored.PlayerMaxHp, 0.0001f);

            Assert.AreEqual(1, restored.Bags.Count);
            Assert.AreEqual("warden_pouch", restored.Bags[0].BagId);
            Assert.AreEqual(0, restored.Bags[0].OriginX);
            Assert.AreEqual(0, restored.Bags[0].OriginY);
            Assert.IsTrue(restored.Bags[0].IsLocked);

            Assert.AreEqual(2, restored.Items.Count);
            Assert.AreEqual("bone_knife", restored.Items[0].ItemId);
            Assert.AreEqual(1, restored.Items[0].OriginX);
            Assert.AreEqual(1, restored.Items[0].OriginY);
            Assert.AreEqual(90, restored.Items[0].Rotation);
            Assert.IsFalse(restored.Items[0].IsLocked);
            Assert.AreEqual("whetstone", restored.Items[1].ItemId);
            Assert.IsTrue(restored.Items[1].IsLocked);

            Assert.AreEqual(1, restored.GroundItems.Count);
            Assert.AreEqual("tarnished_bell", restored.GroundItems[0].ItemId);
            Assert.AreEqual(180, restored.GroundItems[0].Rotation);
        }

        [Test]
        public void RunSaveData_EmptyLists_RoundTripAsEmpty()
        {
            var data = new RunSaveData
            {
                SaveFormatVersion = 1,
                CurrentRound = 1,
                HeroId = "default_hero",
            };

            string json = JsonConvert.SerializeObject(data);
            var restored = JsonConvert.DeserializeObject<RunSaveData>(json);

            Assert.IsNotNull(restored.Bags);
            Assert.AreEqual(0, restored.Bags.Count);
            Assert.IsNotNull(restored.Items);
            Assert.AreEqual(0, restored.Items.Count);
            Assert.IsNotNull(restored.GroundItems);
            Assert.AreEqual(0, restored.GroundItems.Count);
        }

        [Test]
        public void RunSaveData_RotationInts_RoundTripUnchanged()
        {
            // Rotation enum values are literal degrees (0/90/180/270). We persist the raw int
            // rather than the enum so renames don't break old saves.
            var data = new RunSaveData { SaveFormatVersion = 1, CurrentRound = 1, HeroId = "default_hero" };
            data.Items.Add(new ItemSaveEntry { ItemId = "mystic_sword", OriginX = 3, OriginY = 3, Rotation = 270, IsLocked = false });

            string json = JsonConvert.SerializeObject(data);
            var restored = JsonConvert.DeserializeObject<RunSaveData>(json);

            Assert.AreEqual(270, restored.Items[0].Rotation);
        }
    }
}
