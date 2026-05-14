using System.Collections.Generic;
using HellpitRampage.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace HellpitRampage.Tests
{
    public class ShapeMathTests
    {
        private static HashSet<Vector2Int> ToSet(IEnumerable<Vector2Int> cells)
        {
            var set = new HashSet<Vector2Int>();
            foreach (var c in cells) set.Add(c);
            return set;
        }

        [Test]
        public void Rotate_Deg0_ReturnsIdenticalCells()
        {
            var input = new List<Vector2Int> { new(0, 0), new(1, 0) };
            var result = ShapeMath.Rotate(input, Rotation.Deg0);

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(ToSet(result).SetEquals(new HashSet<Vector2Int> { new(0, 0), new(1, 0) }));
        }

        [Test]
        public void Rotate_1x1_IsInvariantUnderAllRotations()
        {
            var input = new List<Vector2Int> { new(0, 0) };

            foreach (var r in new[] { Rotation.Deg90, Rotation.Deg180, Rotation.Deg270 })
            {
                var result = ShapeMath.Rotate(input, r);
                Assert.AreEqual(1, result.Count, $"Rotation {r} should keep 1x1 a single cell.");
                Assert.AreEqual(new Vector2Int(0, 0), result[0], $"Rotation {r} should normalize 1x1 to (0,0).");
            }
        }

        [Test]
        public void Rotate_Deg90_Of1x2Vertical_GivesHorizontal()
        {
            var input = new List<Vector2Int> { new(0, 0), new(0, 1) };
            var result = ShapeMath.Rotate(input, Rotation.Deg90);

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(
                ToSet(result).SetEquals(new HashSet<Vector2Int> { new(0, 0), new(1, 0) }),
                "Rotating vertical 1x2 by 90 deg CW should produce a horizontal 2x1 shape, normalized to (0,0)+(1,0).");
        }

        [Test]
        public void Rotate_Deg180_Of2x1Horizontal_GivesHorizontal()
        {
            var input = new List<Vector2Int> { new(0, 0), new(1, 0) };
            var result = ShapeMath.Rotate(input, Rotation.Deg180);

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(
                ToSet(result).SetEquals(new HashSet<Vector2Int> { new(0, 0), new(1, 0) }),
                "Rotating horizontal 2x1 by 180 deg should normalize back to (0,0)+(1,0).");
        }

        [Test]
        public void Rotate_LShape_Deg90_NormalizesCorrectly()
        {
            // L:  (0,0), (1,0), (0,1) — a 2x2 corner missing (1,1).
            // 90 CW: (x,y) -> (y, -x):
            //   (0,0)  -> (0, 0)
            //   (1,0)  -> (0,-1)
            //   (0,1)  -> (1, 0)
            // After normalize (minX=0, minY=-1 -> shift y by +1):
            //   (0,1), (0,0), (1,1)
            var input = new List<Vector2Int> { new(0, 0), new(1, 0), new(0, 1) };
            var result = ShapeMath.Rotate(input, Rotation.Deg90);

            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(
                ToSet(result).SetEquals(new HashSet<Vector2Int> { new(0, 0), new(0, 1), new(1, 1) }),
                "Rotating L by 90 CW should normalize to {(0,0),(0,1),(1,1)}.");
        }

        [Test]
        public void Next_CyclesCorrectly()
        {
            Assert.AreEqual(Rotation.Deg90, ShapeMath.Next(Rotation.Deg0));
            Assert.AreEqual(Rotation.Deg180, ShapeMath.Next(Rotation.Deg90));
            Assert.AreEqual(Rotation.Deg270, ShapeMath.Next(Rotation.Deg180));
            Assert.AreEqual(Rotation.Deg0, ShapeMath.Next(Rotation.Deg270));
        }
    }
}
