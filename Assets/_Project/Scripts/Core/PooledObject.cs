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
    }
}
