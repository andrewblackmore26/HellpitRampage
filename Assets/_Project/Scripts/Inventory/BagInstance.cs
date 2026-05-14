using UnityEngine;

namespace HellpitRampage.Inventory
{
    public class BagInstance
    {
        public int InstanceID;
        public BagData Data;
        public Vector2Int Origin;

        // WS-012: per-instance lock. Forward-looking — bag selling lands in WS-012.5,
        // but the flag is implemented now so day-one starting bags can be protected.
        public bool IsLocked;

        public BagInstance(int id, BagData data, Vector2Int origin)
        {
            InstanceID = id;
            Data = data;
            Origin = origin;
        }

        public override int GetHashCode() => InstanceID;
        public override bool Equals(object obj) => obj is BagInstance b && b.InstanceID == InstanceID;
    }
}
