using HellpitRampage.Inventory;
using UnityEngine;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.5: runtime wrapper for an item resting on the spillover floor. Owns the
    /// GameObject visual and an ItemInstance so the sell modal / lock toggle / tooltip
    /// can treat it like any other inventory entry. The Instance carries InstanceID = 0
    /// (sentinel) — ground items aren't tracked by InventoryGrid and don't participate
    /// in synergies, weapons, or recipes. Lock state lives ONLY on the Instance so the
    /// detail-tooltip lock action (which mutates ItemInstance.IsLocked directly) stays
    /// authoritative.
    /// </summary>
    public class GroundItem
    {
        public GameObject Visual;
        public ItemInstance Instance;

        public ItemData Data => Instance != null ? Instance.Data : null;
        public Rotation Rotation => Instance != null ? Instance.Rotation : Rotation.Deg0;
        public bool IsLocked => Instance != null && Instance.IsLocked;
    }

    /// <summary>
    /// WS-012.5: serializable snapshot of a ground item. Captures only persistent state
    /// (data ref + rotation + lock); physics position is intentionally NOT captured —
    /// restoring drops items at top-of-area and physics settles them. WS-013 will consume
    /// this struct to round-trip ground state through the save system.
    /// </summary>
    [System.Serializable]
    public struct GroundItemSnapshot
    {
        public ItemData ItemId;
        public Rotation Rotation;
        public bool IsLocked;
    }
}
