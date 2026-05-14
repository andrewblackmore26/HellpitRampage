using HellpitRampage.UI;
using NUnit.Framework;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// Pure static-method test for <see cref="ShopController.CurrentRerollCost"/>. No EventBus or
    /// scene host required.
    /// </summary>
    public class ShopRerollCostTests
    {
        [Test]
        public void CurrentRerollCost_FirstFive_Returns1()
        {
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(1, ShopController.CurrentRerollCost(i), $"Reroll {i} should cost 1.");
        }

        [Test]
        public void CurrentRerollCost_NextFive_Returns2()
        {
            for (int i = 5; i < 10; i++)
                Assert.AreEqual(2, ShopController.CurrentRerollCost(i), $"Reroll {i} should cost 2.");
        }

        [Test]
        public void CurrentRerollCost_TenOrMore_Returns3()
        {
            Assert.AreEqual(3, ShopController.CurrentRerollCost(10));
            Assert.AreEqual(3, ShopController.CurrentRerollCost(25));
            Assert.AreEqual(3, ShopController.CurrentRerollCost(100));
        }
    }
}
