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

        // ---------------- WS-012.2: multi-cell shape rotations ----------------

        [Test]
        public void Rotate_1x3Horizontal_Deg90_BecomesVertical3x1()
        {
            // 1x3 horizontal: (0,0),(1,0),(2,0).
            // Deg90 (CW): (x,y) -> (y,-x):
            //   (0,0) -> (0,0); (1,0) -> (0,-1); (2,0) -> (0,-2).
            // After normalization (minY=-2 -> shift y by +2): (0,2),(0,1),(0,0).
            var input = new List<Vector2Int> { new(0, 0), new(1, 0), new(2, 0) };
            var result = ShapeMath.Rotate(input, Rotation.Deg90);

            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(
                ToSet(result).SetEquals(new HashSet<Vector2Int> { new(0, 0), new(0, 1), new(0, 2) }),
                "Rotated 1x3 horizontal should be a 1x3 vertical column.");
        }

        [Test]
        public void Rotate_2x2Square_AllRotationsProduceSameCellSet()
        {
            // A 2x2 square is rotation-invariant after normalization.
            var input = new List<Vector2Int> { new(0, 0), new(1, 0), new(0, 1), new(1, 1) };
            var expected = new HashSet<Vector2Int> { new(0, 0), new(1, 0), new(0, 1), new(1, 1) };

            foreach (var r in new[] { Rotation.Deg0, Rotation.Deg90, Rotation.Deg180, Rotation.Deg270 })
            {
                var result = ShapeMath.Rotate(input, r);
                Assert.IsTrue(ToSet(result).SetEquals(expected),
                    $"2x2 square should be invariant under {r}.");
            }
        }

        [Test]
        public void Rotate_3CellL_FourRotationsAreAllDistinctButCorrectlyNormalized()
        {
            // L-shape: (0,0),(1,0),(1,1) — bottom-left + bottom-right + top-right corner.
            var input = new List<Vector2Int> { new(0, 0), new(1, 0), new(1, 1) };

            var rotations = new[]
            {
                ShapeMath.Rotate(input, Rotation.Deg0),
                ShapeMath.Rotate(input, Rotation.Deg90),
                ShapeMath.Rotate(input, Rotation.Deg180),
                ShapeMath.Rotate(input, Rotation.Deg270),
            };

            // Each rotation has exactly 3 cells, fits in a 2x2 bbox, and is normalized to (0,0)-min.
            foreach (var rot in rotations)
            {
                Assert.AreEqual(3, rot.Count, "Every rotation of a 3-cell L must still have 3 cells.");
                int minX = int.MaxValue, minY = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue;
                foreach (var c in rot)
                {
                    if (c.x < minX) minX = c.x; if (c.y < minY) minY = c.y;
                    if (c.x > maxX) maxX = c.x; if (c.y > maxY) maxY = c.y;
                }
                Assert.AreEqual(0, minX, "Rotation must normalize minX to 0.");
                Assert.AreEqual(0, minY, "Rotation must normalize minY to 0.");
                Assert.AreEqual(1, maxX, "L-shape bbox should be 2x2.");
                Assert.AreEqual(1, maxY, "L-shape bbox should be 2x2.");
            }

            // The four rotation cell-sets should be pairwise distinct (the L is not symmetric).
            var setDeg0 = ToSet(rotations[0]);
            var setDeg90 = ToSet(rotations[1]);
            var setDeg180 = ToSet(rotations[2]);
            var setDeg270 = ToSet(rotations[3]);
            Assert.IsFalse(setDeg0.SetEquals(setDeg90), "Deg0 and Deg90 of L should differ.");
            Assert.IsFalse(setDeg90.SetEquals(setDeg180), "Deg90 and Deg180 of L should differ.");
            Assert.IsFalse(setDeg180.SetEquals(setDeg270), "Deg180 and Deg270 of L should differ.");
            Assert.IsFalse(setDeg270.SetEquals(setDeg0), "Deg270 and Deg0 of L should differ.");
        }
    }
}
