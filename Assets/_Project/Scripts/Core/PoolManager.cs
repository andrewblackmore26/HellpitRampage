using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace HellpitRampage.Core
{
    /// <summary>
    /// Singleton wrapper around <see cref="ObjectPool{T}"/>. One pool per source prefab,
    /// keyed by prefab reference. Instances are parented under PoolManager so they
    /// persist alongside the rest of the Managers hierarchy across scene loads.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // DontDestroyOnLoad only works on root GameObjects. The singleton sits under a
            // `Managers` parent in Boot.unity for hierarchy organization, so persist the root.
            DontDestroyOnLoad(transform.root.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                _pools.Clear();
                Instance = null;
            }
        }

        public GameObject Get(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("PoolManager.Get: prefab is null.");
                return null;
            }

            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = CreatePool(prefab);
            }

            return pool.Get();
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
            {
                Debug.LogError("PoolManager.Release: instance is null.");
                return;
            }

            PooledObject marker = instance.GetComponent<PooledObject>();
            if (marker == null || marker.SourcePrefab == null)
            {
                Debug.LogError($"PoolManager.Release: instance '{instance.name}' has no PooledObject/SourcePrefab. Destroying directly.");
                Destroy(instance);
                return;
            }

            if (!_pools.TryGetValue(marker.SourcePrefab, out var pool))
            {
                Debug.LogError($"PoolManager.Release: no pool found for source prefab '{marker.SourcePrefab.name}'. Destroying instance.");
                Destroy(instance);
                return;
            }

            pool.Release(instance);
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null)
            {
                Debug.LogError("PoolManager.Prewarm: prefab is null.");
                return;
            }
            if (count <= 0) return;

            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = CreatePool(prefab);
            }

            // Round-trip Get/Release so the pool's internal stack is seeded.
            var temp = new GameObject[count];
            for (int i = 0; i < count; i++) temp[i] = pool.Get();
            for (int i = 0; i < count; i++) pool.Release(temp[i]);
        }

        private ObjectPool<GameObject> CreatePool(GameObject prefab)
        {
            bool collectionCheck = false;
#if UNITY_EDITOR
            collectionCheck = true;
#endif
            var pool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    GameObject obj = Instantiate(prefab, transform);
                    PooledObject marker = obj.GetComponent<PooledObject>();
                    if (marker == null) marker = obj.AddComponent<PooledObject>();
                    marker.SourcePrefab = prefab;
                    return obj;
                },
                actionOnGet: obj => obj.SetActive(true),
                actionOnRelease: obj => obj.SetActive(false),
                actionOnDestroy: obj => Destroy(obj),
                collectionCheck: collectionCheck,
                defaultCapacity: 10,
                maxSize: 200);

            _pools[prefab] = pool;
            return pool;
        }
    }
}
