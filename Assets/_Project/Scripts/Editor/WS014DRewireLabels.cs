using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HellpitRampage.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HellpitRampage.EditorTools
{
    /// <summary>
    /// WS-014.D: completes the C-1 fix. Two steps per scene:
    /// <list type="number">
    /// <item>Run <see cref="MigrateTextToTMPro.MigrateActiveScene"/> — converts every legacy
    /// <c>UnityEngine.UI.Text</c> to <see cref="TextMeshProUGUI"/>.</item>
    /// <item>Re-wire the controller label <see cref="SerializeField"/>s. The migration only
    /// re-points references that still pointed <i>at</i> a Text; WS-012.4's script-type change
    /// (Text → TMP fields) had already nulled these, and WS-015's scene re-save persisted them
    /// as <c>{fileID: 0}</c> — so the migration cannot recover them. Each field is wired to
    /// the TMP on a known anchor GameObject; only ever assigns a field that is currently null.
    /// </list>
    /// Run headless via <c>-executeMethod HellpitRampage.EditorTools.WS014DRewireLabels.Run</c>.
    /// </summary>
    public static class WS014DRewireLabels
    {
        public static void Run()
        {
            var sb = new StringBuilder("WS-014.D — migrate scene Text to TMP + re-wire label references\n");

            ProcessScene("Assets/_Project/Scenes/Combat.unity", sb, WireCombat);
            ProcessScene("Assets/_Project/Scenes/Shop.unity", sb, WireShop);
            ProcessScene("Assets/_Project/Scenes/MainMenu.unity", sb, null); // migrate only — labels are runtime-resolved

            string outPath = Path.GetFullPath("Temp/ws015/rewire.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[WS014D] Done. Report: {outPath}");
        }

        private static void ProcessScene(string path, StringBuilder sb, Action<Scene, StringBuilder> rewire)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.AppendLine($"=== {scene.name} ===");
            MigrateTextToTMPro.MigrateActiveScene(); // legacy Text -> TMP
            rewire?.Invoke(scene, sb);               // wire the fields the migration left at {fileID: 0}
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WireCombat(Scene s, StringBuilder sb)
        {
            WireFromAnchor<RunEndOverlayController>(s, "_headerLabel", "YouDiedText", sb);
            WireFromAnchor<RoundTimerUI>(s, "_label", "RoundTimerLabel", sb);
        }

        private static void WireShop(Scene s, StringBuilder sb)
        {
            WireFromAnchor<ShopOverlayController>(s, "_headerLabel", "ShopOverlayHeader", sb);
            WireFromAnchor<GoldDisplayController>(s, "_label", "GoldDisplay", sb);
            WireFromAnchor<ShopController>(s, "_rerollLabel", "RerollButton", sb);
            WireFromAnchor<ModeToggleButton>(s, "_label", "ModeToggleButton", sb);
            WireFromAnchor<SellModal>(s, "_label", "SellModal", sb);
            foreach (ShopSlot slot in FindAll<ShopSlot>(s))
            {
                WireField(slot, "_nameLabel", FindChildTmp(slot.gameObject, "NameLabel"), sb);
                WireField(slot, "_priceLabel", FindChildTmp(slot.gameObject, "PriceLabel"), sb);
            }
        }

        // Wires <paramref name="field"/> on the scene's single <typeparamref name="T"/> to the
        // TMP found within the GameObject named <paramref name="anchorName"/>.
        private static void WireFromAnchor<T>(Scene scene, string field, string anchorName, StringBuilder sb)
            where T : Component
        {
            T comp = FindFirst<T>(scene);
            if (comp == null) { sb.AppendLine($"  SKIP {typeof(T).Name}.{field}: component not found"); return; }
            GameObject anchor = FindGameObject(scene, anchorName);
            if (anchor == null) { sb.AppendLine($"  SKIP {typeof(T).Name}.{field}: anchor '{anchorName}' not found"); return; }
            TextMeshProUGUI tmp = anchor.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            WireField(comp, field, tmp, sb);
        }

        private static void WireField(Component comp, string field, TextMeshProUGUI target, StringBuilder sb)
        {
            string label = comp != null ? comp.GetType().Name : "?";
            var so = new SerializedObject(comp);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { sb.AppendLine($"  SKIP {label}.{field}: serialized property not found"); return; }
            if (p.objectReferenceValue != null) { sb.AppendLine($"  OK   {label}.{field}: already wired"); return; }
            if (target == null) { sb.AppendLine($"  FAIL {label}.{field}: no TMP found to wire"); return; }
            p.objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine($"  WIRED {label}.{field} -> {target.name}");
        }

        private static T FindFirst<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T c = root.GetComponentInChildren<T>(includeInactive: true);
                if (c != null) return c;
            }
            return null;
        }

        private static List<T> FindAll<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<T>(includeInactive: true));
            return list;
        }

        private static GameObject FindGameObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform t = FindRecursive(root.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static Transform FindRecursive(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform child in t)
            {
                Transform found = FindRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static TextMeshProUGUI FindChildTmp(GameObject root, string childName)
        {
            Transform t = FindRecursive(root.transform, childName);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }
    }
}
