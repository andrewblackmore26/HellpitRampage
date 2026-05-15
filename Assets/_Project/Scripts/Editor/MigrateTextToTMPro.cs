using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HellpitRampage.EditorTools
{
    /// <summary>
    /// WS-012.4: one-shot migration of every legacy <see cref="Text"/> component in the active
    /// scene to <see cref="TextMeshProUGUI"/>. Preserves text content, font size, color,
    /// alignment, font style, raycast target, wrapping, and re-points any
    /// <see cref="SerializeField"/> reference that pointed at the old Text. Run once per scene
    /// (Game.unity, MainMenu.unity); marks the scene dirty so File &gt; Save Scene persists.
    ///
    /// Requires TMP Essentials to be imported (Window &gt; TextMeshPro &gt; Import TMP Essential
    /// Resources). Aborts with a clear log if the default font asset isn't available.
    /// </summary>
    public static class MigrateTextToTMPro
    {
        [MenuItem("Tools/WS-012.4/Migrate Text → TMP (Active Scene)")]
        public static void MigrateActiveScene()
        {
            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont == null)
            {
                Debug.LogError("[MigrateTextToTMPro] TMP_Settings.defaultFontAsset is null. " +
                               "Import TMP Essentials first: Window > TextMeshPro > Import TMP Essential Resources.");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MigrateTextToTMPro] No active scene.");
                return;
            }

            List<Text> legacyTexts = CollectInScene<Text>(scene);
            if (legacyTexts.Count == 0)
            {
                Debug.Log($"[MigrateTextToTMPro] No legacy Text components found in '{scene.name}'. Nothing to do.");
                return;
            }

            int migrated = 0;
            int rewiredRefs = 0;
            int unresolvedRefs = 0;

            foreach (Text legacy in legacyTexts)
            {
                if (legacy == null) continue;

                GameObject go = legacy.gameObject;
                var snapshot = Snapshot.From(legacy);

                List<(Component owner, string propertyPath)> incomingRefs = CollectIncomingReferences(legacy);

                Undo.DestroyObjectImmediate(legacy);

                TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(go);
                ApplySnapshot(tmp, snapshot, defaultFont);

                foreach (var (owner, path) in incomingRefs)
                {
                    if (owner == null) { unresolvedRefs++; continue; }
                    var so = new SerializedObject(owner);
                    var prop = so.FindProperty(path);
                    if (prop == null) { unresolvedRefs++; continue; }
                    prop.objectReferenceValue = tmp;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    rewiredRefs++;
                }

                migrated++;
                Debug.Log($"[MigrateTextToTMPro] {go.name}: Text → TMP (refs rewired: {incomingRefs.Count})", go);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MigrateTextToTMPro] Done. Scene '{scene.name}': migrated {migrated} Text components, " +
                      $"rewired {rewiredRefs} references (unresolved: {unresolvedRefs}). " +
                      "Save the scene to persist (File > Save).");
        }

        private struct Snapshot
        {
            public string Text;
            public int FontSize;
            public Color Color;
            public TextAnchor Alignment;
            public FontStyle FontStyle;
            public bool RaycastTarget;
            public bool Maskable;
            public HorizontalWrapMode HorizontalOverflow;
            public VerticalWrapMode VerticalOverflow;
            public float LineSpacing;
            public bool SupportRichText;

            public static Snapshot From(Text t) => new()
            {
                Text = t.text,
                FontSize = t.fontSize,
                Color = t.color,
                Alignment = t.alignment,
                FontStyle = t.fontStyle,
                RaycastTarget = t.raycastTarget,
                Maskable = t.maskable,
                HorizontalOverflow = t.horizontalOverflow,
                VerticalOverflow = t.verticalOverflow,
                LineSpacing = t.lineSpacing,
                SupportRichText = t.supportRichText,
            };
        }

        private static void ApplySnapshot(TextMeshProUGUI tmp, Snapshot s, TMP_FontAsset font)
        {
            tmp.font = font;
            tmp.text = s.Text;
            tmp.fontSize = s.FontSize;
            tmp.color = s.Color;
            tmp.alignment = MapAlignment(s.Alignment);
            tmp.fontStyle = MapFontStyle(s.FontStyle);
            tmp.raycastTarget = s.RaycastTarget;
            tmp.maskable = s.Maskable;
            tmp.richText = s.SupportRichText;
            tmp.lineSpacing = s.LineSpacing == 1f ? 0f : (s.LineSpacing - 1f) * 100f; // legacy=1.0 baseline; TMP uses % delta
            tmp.textWrappingMode = s.HorizontalOverflow == HorizontalWrapMode.Wrap
                ? TextWrappingModes.Normal
                : TextWrappingModes.NoWrap;
            tmp.overflowMode = s.VerticalOverflow == VerticalWrapMode.Overflow
                ? TextOverflowModes.Overflow
                : TextOverflowModes.Truncate;
        }

        private static TextAlignmentOptions MapAlignment(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft    => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter  => TextAlignmentOptions.Top,
            TextAnchor.UpperRight   => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft   => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight  => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft    => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter  => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight   => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center,
        };

        private static FontStyles MapFontStyle(FontStyle s) => s switch
        {
            FontStyle.Normal        => FontStyles.Normal,
            FontStyle.Bold          => FontStyles.Bold,
            FontStyle.Italic        => FontStyles.Italic,
            FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
            _ => FontStyles.Normal,
        };

        private static List<(Component owner, string propertyPath)> CollectIncomingReferences(Text target)
        {
            Scene scene = target.gameObject.scene;
            List<MonoBehaviour> all = CollectInScene<MonoBehaviour>(scene);
            var refs = new List<(Component, string)>();
            foreach (MonoBehaviour mb in all)
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                SerializedProperty iter = so.GetIterator();
                while (iter.NextVisible(true))
                {
                    if (iter.propertyType == SerializedPropertyType.ObjectReference
                        && iter.objectReferenceValue == target)
                    {
                        refs.Add((mb, iter.propertyPath));
                    }
                }
            }
            return refs;
        }

        // L-004 / memory rule: avoid `FindObjectsByType` overloads that take `FindObjectsSortMode`
        // (deprecated on this Unity build). Traverse root GameObjects and use
        // `GetComponentsInChildren<T>(true)` which catches inactive objects without warnings.
        private static List<T> CollectInScene<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(includeInactive: true));
            }
            return results;
        }
    }
}
