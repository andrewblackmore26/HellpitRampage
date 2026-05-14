using HellpitRampage.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012.2: ItemData exposes a PlaceholderColor field used by InventoryGridView's per-cell
    /// rendering until real sprite art lands. Tests cover the default value and that distinct
    /// items can hold distinct colors without aliasing.
    /// </summary>
    public class PlaceholderColorTests
    {
        [Test]
        public void PlaceholderColor_DefaultOnFreshItemData_IsMediumGreyOpaque()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();

            Assert.AreEqual(0.5f, item.PlaceholderColor.r, 0.001f, "Default red should be 0.5.");
            Assert.AreEqual(0.5f, item.PlaceholderColor.g, 0.001f, "Default green should be 0.5.");
            Assert.AreEqual(0.5f, item.PlaceholderColor.b, 0.001f, "Default blue should be 0.5.");
            Assert.AreEqual(1.0f, item.PlaceholderColor.a, 0.001f, "Default alpha should be 1.0 (opaque).");
        }

        [Test]
        public void PlaceholderColor_DistinctValuesAcrossItems_DoNotAlias()
        {
            var whetstone = ScriptableObject.CreateInstance<ItemData>();
            whetstone.PlaceholderColor = new Color(0.72f, 0.72f, 0.75f, 1f);

            var sword = ScriptableObject.CreateInstance<ItemData>();
            sword.PlaceholderColor = new Color(0.60f, 0.72f, 0.85f, 1f);

            var crown = ScriptableObject.CreateInstance<ItemData>();
            crown.PlaceholderColor = new Color(0.72f, 0.55f, 0.20f, 1f);

            // Each instance holds its own color — no shared static state.
            Assert.AreNotEqual(whetstone.PlaceholderColor, sword.PlaceholderColor);
            Assert.AreNotEqual(sword.PlaceholderColor, crown.PlaceholderColor);
            Assert.AreNotEqual(crown.PlaceholderColor, whetstone.PlaceholderColor);

            // Spot-check a channel value to make sure the assignment actually round-tripped.
            Assert.AreEqual(0.60f, sword.PlaceholderColor.r, 0.001f);
            Assert.AreEqual(0.20f, crown.PlaceholderColor.b, 0.001f);
        }
    }
}
