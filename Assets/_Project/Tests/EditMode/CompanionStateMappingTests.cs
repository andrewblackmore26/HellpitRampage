using HellpitRampage.Narrative;
using NUnit.Framework;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-014.B: verifies the round → companion-state mapping used to drive the
    /// placeholder companion appearances.
    /// </summary>
    public class CompanionStateMappingTests
    {
        [Test]
        public void Round5_Returns_Composed()
            => Assert.AreEqual("Composed", CompanionAppearanceScheduler.GetCompanionState(5));

        [Test]
        public void Round14_Returns_Concerned()
            => Assert.AreEqual("Concerned", CompanionAppearanceScheduler.GetCompanionState(14));

        [Test]
        public void Round22_Returns_Glitched()
            => Assert.AreEqual("Glitched", CompanionAppearanceScheduler.GetCompanionState(22));

        [Test]
        public void Round30_Returns_Final()
            => Assert.AreEqual("Final", CompanionAppearanceScheduler.GetCompanionState(30));
    }
}
