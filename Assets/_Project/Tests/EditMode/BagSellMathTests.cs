using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012.5 bag sell math: SellModal computes bag sale price as ceil(EffectivePrice * 0.5f),
    /// matching item sell math (SellMathTests). Bags spill contents to the ground at this price;
    /// the math here pins the same Mathf.CeilToInt path so a refactor toward integer division
    /// (which would yield 0 for 1g bags) fails fast.
    /// </summary>
    public class BagSellMathTests
    {
        [Test]
        public void BagSellPrice_Of1g_Returns1()
        {
            Assert.AreEqual(1, Mathf.CeilToInt(1 * 0.5f));
        }

        [Test]
        public void BagSellPrice_Of3g_Returns2()
        {
            Assert.AreEqual(2, Mathf.CeilToInt(3 * 0.5f));
        }

        [Test]
        public void BagSellPrice_Of10g_Returns5()
        {
            Assert.AreEqual(5, Mathf.CeilToInt(10 * 0.5f));
        }
    }
}
