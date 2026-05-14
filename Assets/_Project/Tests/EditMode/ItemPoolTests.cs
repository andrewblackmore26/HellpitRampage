using HellpitRampage.Inventory;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ItemPool.DrawWeighted"/>. Uses ScriptableObject.CreateInstance so
    /// no scene or asset serialization round-trip is needed.
    /// </summary>
    public class ItemPoolTests
    {
        private static ItemData MakeItem(ItemRarity rarity)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.Rarity = rarity;
            return item;
        }

        [Test]
        public void DrawWeighted_WithEmptyPool_ReturnsEmpty()
        {
            var pool = ScriptableObject.CreateInstance<ItemPool>();
            var draws = pool.DrawWeighted(5, new Random(42));
            Assert.IsNotNull(draws);
            Assert.AreEqual(0, draws.Count);
        }

        [Test]
        public void DrawWeighted_RespectsRequestedCount()
        {
            var pool = ScriptableObject.CreateInstance<ItemPool>();
            pool.Items.Add(MakeItem(ItemRarity.Common));
            pool.Items.Add(MakeItem(ItemRarity.Uncommon));
            pool.Items.Add(MakeItem(ItemRarity.Rare));

            var draws = pool.DrawWeighted(5, new Random(7));
            Assert.AreEqual(5, draws.Count);
        }

        [Test]
        public void DrawWeighted_WithSeededRandom_IsDeterministic()
        {
            var pool = ScriptableObject.CreateInstance<ItemPool>();
            var a = MakeItem(ItemRarity.Common);
            var b = MakeItem(ItemRarity.Uncommon);
            var c = MakeItem(ItemRarity.Rare);
            pool.Items.Add(a);
            pool.Items.Add(b);
            pool.Items.Add(c);

            var first = pool.DrawWeighted(10, new Random(2026));
            var second = pool.DrawWeighted(10, new Random(2026));

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
                Assert.AreSame(first[i], second[i], $"Seeded RNG must produce identical draws at index {i}.");
        }
    }
}
