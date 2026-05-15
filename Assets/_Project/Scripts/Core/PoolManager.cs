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

            GameObject instance = pool.Get();
            // The instance is now checked out — clear the pool-membership flag so a later
            // Release is accepted exactly once.
            if (instance != null)
            {
                PooledObject marker = instance.GetComponent<PooledObject>();
                if (marker != null) marker.IsPooled = false;
            }
            return instance;
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

            // Pool-level idempotency. A consumer's own despawn guard (e.g. Projectile._isDespawned)
            // is reset by OnEnable, so it cannot catch a double-release where the instance was
            // re-activated out of band between the two releases (scene/domain reload). This flag
            // is the pool's own source of truth — skip the redundant pool.Release so ObjectPool's
            // collectionCheck (kept ON, per L-005) only ever fires on a genuinely new bug.
            if (marker.IsPooled)
            {
                Debug.LogWarning($"PoolManager.Release: instance '{instance.name}' is already in its pool; ignoring redundant release.");
                return;
            }
            marker.IsPooled = true;

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

            // Round-trip Get/Release so the pool's internal stack is seeded. Keep each marker's
            // IsPooled flag truthful so the very first real Release after prewarm isn't seen as
            // a redundant release.
            var temp = new GameObject[count];
            for (int i = 0; i < count; i++) temp[i] = pool.Get();
            for (int i = 0; i < count; i++)
            {
                pool.Release(temp[i]);
                if (temp[i] != null)
                {
                    PooledObject marker = temp[i].GetComponent<PooledObject>();
                    if (marker != null) marker.IsPooled = true;
                }
            }
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
