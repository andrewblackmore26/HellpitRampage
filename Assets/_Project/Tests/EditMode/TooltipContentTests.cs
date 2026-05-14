using HellpitRampage.Inventory;
using HellpitRampage.UI;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// Unit tests for <see cref="TooltipContent.FromItem"/>. Builds ItemData via CreateInstance
    /// so no scene/asset round-trip is required.
    /// </summary>
    public class TooltipContentTests
    {
        [Test]
        public void FromItem_WeaponItem_PopulatesAllFields()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.ItemName = "Bone Knife";
            item.Description = "A weathered bone shard.";
            item.Rarity = ItemRarity.Common;
            item.Damage = 1f;
            item.Cooldown = 0.5f;
            item.Range = 8f;
            item.ProjectileSpeed = 12f;
            // ProjectilePrefab non-null marks this as a weapon for tooltip purposes. We can't easily
            // create a runtime GameObject here without polluting state, but ProjectilePrefab is
            // a GameObject reference — a freshly-created GameObject works.
            var marker = new GameObject("ProjectileMarker");
            item.ProjectilePrefab = marker;

            TooltipContent content = TooltipContent.FromItem(item);

            Assert.AreEqual("Bone Knife", content.Title);
            Assert.AreEqual("Common", content.RarityLabel);
            Assert.AreEqual("A weathered bone shard.", content.Description);
            StringAssert.Contains("DMG: 1", content.StatLines);
            StringAssert.Contains("Cooldown: 0.5s", content.StatLines);
            StringAssert.Contains("Range: 8", content.StatLines);
            StringAssert.Contains("Speed: 12", content.StatLines);

            Object.DestroyImmediate(marker);
        }

        [Test]
        public void FromItem_NonWeaponItem_ShowsEffectPending()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.ItemName = "Bottle of Hush";
            item.Rarity = ItemRarity.Common;
            item.ProjectilePrefab = null; // explicitly non-weapon

            TooltipContent content = TooltipContent.FromItem(item);

            Assert.AreEqual("Bottle of Hush", content.Title);
            Assert.AreEqual("(Effect pending)", content.StatLines);
        }
    }
}
