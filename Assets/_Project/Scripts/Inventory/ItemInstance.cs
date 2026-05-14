using System.Collections.Generic;
using UnityEngine;

namespace HellpitRampage.Inventory
{
    public class ItemInstance
    {
        public int InstanceID;
        public ItemData Data;
        public Vector2Int Origin;
        public BagInstance HostBag;
        public Rotation Rotation;

        public ItemInstance(int id, ItemData data, Vector2Int origin, BagInstance hostBag, Rotation rotation = Rotation.Deg0)
        {
            InstanceID = id;
            Data = data;
            Origin = origin;
            HostBag = hostBag;
            Rotation = rotation;
        }

        public List<Vector2Int> EffectiveCells()
        {
            if (Data == null || Data.Shape == null) return new List<Vector2Int>();
            return ShapeMath.Rotate(Data.Shape.Cells, Rotation);
        }

        /// <summary>
        /// Star anchors with the same rotation + normalization as <see cref="EffectiveCells"/>.
        /// The offset is computed from the shape's cells so star cells line up with the rotated body.
        /// Directions rotate CW per Deg90 (matching ShapeMath's convention).
        /// </summary>
        public List<StarredEdge> EffectiveStarredEdges()
        {
            var result = new List<StarredEdge>();
            if (Data == null || Data.Shape == null) return result;
            if (Data.StarredEdges == null || Data.StarredEdges.Count == 0) return result;

            Vector2Int offset = ShapeMath.ComputeRotationOffset(Data.Shape.Cells, Rotation);
            foreach (var star in Data.StarredEdges)
            {
                if (star == null) continue;
                Vector2Int rotatedCell = ShapeMath.RotateCellRaw(star.Cell, Rotation) - offset;
                EdgeDirection rotatedDir = ShapeMath.RotateDirection(star.Direction, Rotation);
                result.Add(new StarredEdge { Cell = rotatedCell, Direction = rotatedDir });
            }
            return result;
        }

        /// <summary>
        /// True if <paramref name="absoluteCell"/> is one of this item's effective grid cells
        /// (after rotation and origin offset). Used by the star overlay to skip target cells
        /// that point into the starred item's own shape.
        /// </summary>
        public bool OccupiesCell(Vector2Int absoluteCell)
        {
            foreach (var off in EffectiveCells())
                if (Origin + off == absoluteCell) return true;
            return false;
        }

        public override int GetHashCode() => InstanceID;
        public override bool Equals(object obj) => obj is ItemInstance i && i.InstanceID == InstanceID;
    }
}
