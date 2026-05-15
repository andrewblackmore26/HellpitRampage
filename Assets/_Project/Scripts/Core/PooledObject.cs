using UnityEngine;

namespace HellpitRampage.Core
{
    /// <summary>
    /// Marker component automatically attached to pooled instances.
    /// Stores a reference back to the source prefab so PoolManager.Release knows which pool to return to.
    /// Do not add this component manually — PoolManager attaches it during instance creation.
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        [HideInInspector] public GameObject SourcePrefab;

        // True while this instance is sitting in its pool (released), false while it is checked
        // out. PoolManager owns this flag — it is the pool-level source of truth for membership,
        // independent of any consumer's own despawn guard. Makes PoolManager.Release idempotent
        // so an out-of-band double-release (scene/domain reload scrambling consumer state) is
        // ignored with a warning instead of throwing on ObjectPool's collectionCheck.
        [HideInInspector] public bool IsPooled;
    }
}
