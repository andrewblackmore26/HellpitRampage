using System.Collections.Generic;
using UnityEngine;

namespace HellpitRampage.Inventory
{
    public static class ShapeMath
    {
        public static List<Vector2Int> Rotate(IReadOnlyList<Vector2Int> cells, Rotation rotation)
        {
            var result = new List<Vector2Int>(cells.Count);

            foreach (var c in cells)
            {
                Vector2Int rotated = rotation switch
                {
                    Rotation.Deg0   => new Vector2Int(c.x, c.y),
                    Rotation.Deg90  => new Vector2Int(c.y, -c.x),
                    Rotation.Deg180 => new Vector2Int(-c.x, -c.y),
                    Rotation.Deg270 => new Vector2Int(-c.y, c.x),
                    _ => c
                };
                result.Add(rotated);
            }

            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in result)
            {
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
            }
            var offset = new Vector2Int(minX, minY);
            for (int i = 0; i < result.Count; i++) result[i] -= offset;

            return result;
        }

        public static Rotation Next(Rotation r) => r switch
        {
            Rotation.Deg0 => Rotation.Deg90,
            Rotation.Deg90 => Rotation.Deg180,
            Rotation.Deg180 => Rotation.Deg270,
            Rotation.Deg270 => Rotation.Deg0,
            _ => Rotation.Deg0
        };

        // ---------- WS-011.5 helpers for star + direction rotation ----------

        /// <summary>
        /// Raw per-cell rotation matching <see cref="Rotate"/>'s convention. Does NOT apply
        /// the origin-normalization offset. Useful when you need to rotate a single cell
        /// (e.g., a star anchor) while reusing the offset computed for the whole shape.
        /// </summary>
        public static Vector2Int RotateCellRaw(Vector2Int c, Rotation rotation) => rotation switch
        {
            Rotation.Deg0   => c,
            Rotation.Deg90  => new Vector2Int(c.y, -c.x),
            Rotation.Deg180 => new Vector2Int(-c.x, -c.y),
            Rotation.Deg270 => new Vector2Int(-c.y, c.x),
            _ => c
        };

        /// <summary>
        /// Returns the (minX, minY) of the raw-rotated cells of <paramref name="shapeCells"/>.
        /// Subtract this from every raw-rotated cell to match the normalization <see cref="Rotate"/>
        /// applies. Star edges on a multi-cell item must use this offset to stay attached
        /// to the same logical cell after rotation.
        /// </summary>
        public static Vector2Int ComputeRotationOffset(IReadOnlyList<Vector2Int> shapeCells, Rotation rotation)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in shapeCells)
            {
                Vector2Int r = RotateCellRaw(c, rotation);
                if (r.x < minX) minX = r.x;
                if (r.y < minY) minY = r.y;
            }
            return new Vector2Int(minX, minY);
        }

        /// <summary>
        /// Rotates an <see cref="EdgeDirection"/> by the same CW step <see cref="Rotate"/> uses.
        /// Deg90: Up→Right, Right→Down, Down→Left, Left→Up (matches the (x,y)→(y,-x) rotation
        /// applied to a unit direction vector).
        /// </summary>
        public static EdgeDirection RotateDirection(EdgeDirection dir, Rotation rotation)
        {
            int steps = (((int)rotation / 90) % 4 + 4) % 4;
            for (int i = 0; i < steps; i++) dir = StepCW(dir);
            return dir;
        }

        // Spec §4.6 call site naming. Same as RotateDirection.
        public static EdgeDirection RotateEdge(EdgeDirection dir, Rotation rotation) => RotateDirection(dir, rotation);

        private static EdgeDirection StepCW(EdgeDirection d) => d switch
        {
            EdgeDirection.Up => EdgeDirection.Right,
            EdgeDirection.Right => EdgeDirection.Down,
            EdgeDirection.Down => EdgeDirection.Left,
            EdgeDirection.Left => EdgeDirection.Up,
            _ => d
        };
    }
}
