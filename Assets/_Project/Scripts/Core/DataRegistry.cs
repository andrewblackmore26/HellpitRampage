using System.Collections.Generic;
using HellpitRampage.Combat;
using HellpitRampage.Inventory;
using UnityEngine;

namespace HellpitRampage.Core
{
    /// <summary>
    /// WS-013: singleton that indexes every data SO by its stable string Id. The save system
    /// stores Ids (not Unity object references) so they survive content shuffling, then asks
    /// the registry to resolve them back to assets on load. Initialized at Boot from a hand-
    /// authored DataManifest (no Resources folder convention).
    /// </summary>
    public class DataRegistry : MonoBehaviour
    {
        public static DataRegistry Instance { get; private set; }

        [SerializeField] private DataManifest _manifest;

        private readonly Dictionary<string, ItemData> _items = new();
        private readonly Dictionary<string, BagData> _bags = new();
        private readonly Dictionary<string, HeroData> _heroes = new();
        private readonly Dictionary<string, EnemyData> _enemies = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // L-001: persist the root, not the child. Singleton sits under `Managers`.
            // Guarded: DontDestroyOnLoad is play-mode-only and throws in EditMode tests.
            if (Application.isPlaying) DontDestroyOnLoad(transform.root.gameObject);

            IndexManifest();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void IndexManifest()
        {
            _items.Clear();
            _bags.Clear();
            _heroes.Clear();
            _enemies.Clear();

            if (_manifest == null)
            {
                Debug.LogError("[DataRegistry] No DataManifest assigned. Save/load will not be able to resolve any data IDs.");
                return;
            }

            Index(_manifest.Items, _items, so => so.Id, "ItemData");
            Index(_manifest.Bags, _bags, so => so.Id, "BagData");
            Index(_manifest.Heroes, _heroes, so => so.Id, "HeroData");
            Index(_manifest.Enemies, _enemies, so => so.Id, "EnemyData");

            Debug.Log($"[DataRegistry] Indexed {_items.Count} items, {_bags.Count} bags, {_heroes.Count} heroes, {_enemies.Count} enemies.");
        }

        private static void Index<T>(IList<T> assets, Dictionary<string, T> dict,
                                     System.Func<T, string> idAccessor, string typeName)
            where T : ScriptableObject
        {
            if (assets == null) return;
            foreach (var asset in assets)
            {
                if (asset == null) continue;
                string id = idAccessor(asset);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[DataRegistry] {typeName} '{asset.name}' has empty Id; skipped.");
                    continue;
                }
                if (dict.ContainsKey(id))
                {
                    Debug.LogError($"[DataRegistry] Duplicate {typeName} Id '{id}' (existing '{dict[id].name}', new '{asset.name}'); kept first.");
                    continue;
                }
                dict[id] = asset;
            }
        }

        public ItemData  GetItem(string id)  { _items.TryGetValue(id ?? string.Empty, out var v);   if (v == null) Debug.LogWarning($"[DataRegistry] Unknown ItemData id '{id}'.");  return v; }
        public BagData   GetBag(string id)   { _bags.TryGetValue(id ?? string.Empty, out var v);    if (v == null) Debug.LogWarning($"[DataRegistry] Unknown BagData id '{id}'.");   return v; }
        public HeroData  GetHero(string id)  { _heroes.TryGetValue(id ?? string.Empty, out var v);  if (v == null) Debug.LogWarning($"[DataRegistry] Unknown HeroData id '{id}'.");  return v; }
        public EnemyData GetEnemy(string id) { _enemies.TryGetValue(id ?? string.Empty, out var v); if (v == null) Debug.LogWarning($"[DataRegistry] Unknown EnemyData id '{id}'."); return v; }
    }
}
