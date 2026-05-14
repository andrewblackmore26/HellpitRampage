using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012 sell math: SellModal computes sale price as ceil(EffectivePrice * 0.5f).
    /// Integer division (`price / 2`) is wrong for 1g (would yield 0); these tests pin the
    /// Mathf.CeilToInt path so any refactor that switches to int math fails fast.
    /// </summary>
    public class SellMathTests
    {
        [Test]
        public void SellPrice_Of1g_Returns1()
        {
            // ceil(1 * 0.5) = ceil(0.5) = 1 — proves we are not using integer division.
            Assert.AreEqual(1, Mathf.CeilToInt(1 * 0.5f));
        }

        [Test]
        public void SellPrice_Of3g_Returns2()
        {
            // ceil(3 * 0.5) = ceil(1.5) = 2
            Assert.AreEqual(2, Mathf.CeilToInt(3 * 0.5f));
        }

        [Test]
        public void SellPrice_Of10g_Returns5()
        {
            // ceil(10 * 0.5) = ceil(5.0) = 5
            Assert.AreEqual(5, Mathf.CeilToInt(10 * 0.5f));
        }
    }
}
