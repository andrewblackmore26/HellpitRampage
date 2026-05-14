using UnityEngine;

namespace HellpitRampage.Inventory
{
    /// <summary>
    /// One activation edge on an item. The star points outward from <see cref="Cell"/> in
    /// <see cref="Direction"/>. A contributor placed in the adjacent cell in that direction
    /// activates whichever ConditionalEffects on the recipient match the contributor's tags.
    /// </summary>
    [System.Serializable]
    public class StarredEdge
    {
        public Vector2Int Cell;
        public EdgeDirection Direction;
    }
}
