#if UNITY_EDITOR
using System.Collections.Generic;
using HellpitRampage.Combat;
using HellpitRampage.Core;
using HellpitRampage.Inventory;
using UnityEditor;
using UnityEngine;

namespace HellpitRampage.EditorTools
{
    /// <summary>
    /// WS-013: scans every ItemData/BagData/HeroData/EnemyData asset for empty or duplicate
    /// stable Ids. Run via "Tools/Hellpit Rampage/Validate Data IDs" before shipping content
    /// — silently broken Ids surface as DataRegistry lookup failures at load time.
    /// </summary>
    public static class DataIdValidator
    {
        [MenuItem("Tools/Hellpit Rampage/Validate Data IDs")]
        public static void Validate()
        {
            int errors = 0;
            errors += ValidateType<ItemData>("ItemData", a => a.Id);
            errors += ValidateType<BagData>("BagData", a => a.Id);
            errors += ValidateType<HeroData>("HeroData", a => a.Id);
            errors += ValidateType<EnemyData>("EnemyData", a => a.Id);

            if (errors == 0) Debug.Log("[DataIdValidator] Validation passed. 0 errors.");
            else Debug.LogError($"[DataIdValidator] Validation failed. {errors} error(s). See log entries above.");
        }

        private static int ValidateType<T>(string typeName, System.Func<T, string> idAccessor) where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeName}");
            var seen = new Dictionary<string, string>();
            int errors = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null) continue;

                string id = idAccessor(asset);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError($"[DataIdValidator] {typeName} at '{path}' has empty Id.", asset);
                    errors++;
                    continue;
                }
                if (seen.TryGetValue(id, out var existingPath))
                {
                    Debug.LogError($"[DataIdValidator] {typeName} duplicate Id '{id}' between '{path}' and '{existingPath}'.", asset);
                    errors++;
                }
                else
                {
                    seen.Add(id, path);
                }
            }
            return errors;
        }
    }
}
#endif
