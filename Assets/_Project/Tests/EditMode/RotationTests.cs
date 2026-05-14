using HellpitRampage.Inventory;
using NUnit.Framework;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012.1: <see cref="ShapeMath.Next"/> is the rotate-once-CW helper called by both
    /// the R-key and right-click triggers in DragHandler. These tests pin its cycle so a
    /// future refactor that touches rotation can't silently break either input path.
    /// </summary>
    public class RotationTests
    {
        [Test]
        public void Next_FromDeg0_ReturnsDeg90()
        {
            Assert.AreEqual(Rotation.Deg90, ShapeMath.Next(Rotation.Deg0));
        }

        [Test]
        public void Next_FromDeg90_ReturnsDeg180()
        {
            Assert.AreEqual(Rotation.Deg180, ShapeMath.Next(Rotation.Deg90));
        }

        [Test]
        public void Next_FromDeg180_ReturnsDeg270()
        {
            Assert.AreEqual(Rotation.Deg270, ShapeMath.Next(Rotation.Deg180));
        }

        [Test]
        public void Next_FromDeg270_WrapsToDeg0()
        {
            Assert.AreEqual(Rotation.Deg0, ShapeMath.Next(Rotation.Deg270));
        }

        [Test]
        public void Next_FourSteps_FullCycle()
        {
            Rotation r = Rotation.Deg0;
            for (int i = 0; i < 4; i++) r = ShapeMath.Next(r);
            Assert.AreEqual(Rotation.Deg0, r, "Four CW rotations must return to the starting orientation.");
        }
    }
}
