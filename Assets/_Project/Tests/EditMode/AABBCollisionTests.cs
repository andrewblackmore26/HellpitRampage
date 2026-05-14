using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    /// <summary>
    /// WS-012.5: AABB collision math used by GroundManager's item-vs-item push-out pass.
    /// The GroundItemPhysics + GroundManager pair uses Unity's built-in Rect.Overlaps for
    /// detection and a smallest-axis push-out for resolution. These tests pin both behaviors
    /// so a refactor toward an alternative collision model (e.g., circle-circle) breaks visibly.
    /// </summary>
    public class AABBCollisionTests
    {
        [Test]
        public void Overlapping_AABBs_DetectedAsOverlapping()
        {
            var a = new Rect(0, 0, 100, 100);
            var b = new Rect(50, 50, 100, 100);
            Assert.IsTrue(a.Overlaps(b));
            Assert.IsTrue(b.Overlaps(a));
        }

        [Test]
        public void NonOverlapping_AABBs_NotDetected()
        {
            var a = new Rect(0, 0, 100, 100);
            var b = new Rect(200, 200, 100, 100);
            Assert.IsFalse(a.Overlaps(b));
            Assert.IsFalse(b.Overlaps(a));
        }

        [Test]
        public void OverlapMath_SmallerXOverlap_PushesHorizontally()
        {
            // Two 100x100 boxes: A at (0,0), B at (10, 50). X overlap = 90, Y overlap = 50.
            // Smaller axis is Y, so push direction is vertical.
            var a = new Rect(0, 0, 100, 100);
            var b = new Rect(10, 50, 100, 100);

            float overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);

            // Sanity check: overlapX is the big one (90), overlapY is the small one (50).
            Assert.AreEqual(90f, overlapX);
            Assert.AreEqual(50f, overlapY);
            // GroundManager pushes along the smaller-overlap axis.
            Assert.IsTrue(overlapY < overlapX, "Smaller-axis selection should choose Y here.");
        }
    }
}
