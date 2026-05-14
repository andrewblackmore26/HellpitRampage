using System.Collections.Generic;
using UnityEngine;

namespace HellpitRampage.Inventory
{
    [CreateAssetMenu(fileName = "NewShape_ItemShape", menuName = "HellpitRampage/Item Shape")]
    public class ItemShape : ScriptableObject
    {
        [Tooltip("Cells occupied by this shape, as offsets from origin (0,0). Order does not matter.")]
        public List<Vector2Int> Cells = new();

        public int BoundingWidth
        {
            get
            {
                if (Cells == null || Cells.Count == 0) return 0;
                int min = int.MaxValue, max = int.MinValue;
                foreach (var c in Cells) { if (c.x < min) min = c.x; if (c.x > max) max = c.x; }
                return max - min + 1;
            }
        }

        public int BoundingHeight
        {
            get
            {
                if (Cells == null || Cells.Count == 0) return 0;
                int min = int.MaxValue, max = int.MinValue;
                foreach (var c in Cells) { if (c.y < min) min = c.y; if (c.y > max) max = c.y; }
                return max - min + 1;
            }
        }
    }
}
