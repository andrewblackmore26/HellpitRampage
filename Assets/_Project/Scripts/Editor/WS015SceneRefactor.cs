using System.Collections.Generic;
using System.IO;
using System.Text;
using HellpitRampage.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HellpitRampage.EditorTools
{
    /// <summary>
    /// WS-015 part 5: one-shot scene surgery. Run headless with
    /// <c>-executeMethod HellpitRampage.EditorTools.WS015SceneRefactor.Run</c>.
    /// <para/>
    /// Creates Shop.unity, moves the shop-UI subtree out of Combat.unity, relocates the
    /// persistent singletons (InventoryService, SynergyService, HeroStartingLoadout) plus a
    /// new SceneRouter into Boot.unity, duplicates the pause/settings menus into Shop, and
    /// registers Shop in Build Settings.
    /// <para/>
    /// Not idempotent. If a run fails partway, restore the scenes
    /// (<c>git checkout -- Assets/_Project/Scenes ProjectSettings/EditorBuildSettings.asset</c>),
    /// delete Shop.unity, fix the cause, and re-run.
    /// </summary>
    public static class WS015SceneRefactor
    {
        private const string CombatPath = "Assets/_Project/Scenes/Combat.unity";
        private const string BootPath = "Assets/_Project/Scenes/Boot.unity";
        private const string ShopPath = "Assets/_Project/Scenes/Shop.unity";

        public static void Run()
        {
            Debug.Log("[WS015] Scene surgery starting.");

            Scene combat = EditorSceneManager.OpenScene(CombatPath, OpenSceneMode.Single);
            Scene boot = EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Additive);
            Scene shop = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            // ---- A. Boot: relocate the persistent singletons + add SceneRouter ----
            GameObject managers = Require(boot, "Managers");
            MoveRootUnder(Require(combat, "InventoryService"), boot, managers.transform);
            MoveRootUnder(Require(combat, "SynergyService"), boot, managers.transform);
            MoveRootUnder(Require(combat, "HeroStartingLoadout"), boot, managers.transform);

            var routerGO = new GameObject("SceneRouter");
            routerGO.AddComponent<SceneRouter>();
            SceneManager.MoveGameObjectToScene(routerGO, boot);
            routerGO.transform.SetParent(managers.transform, false);
            Debug.Log("[WS015] Boot: InventoryService/SynergyService/HeroStartingLoadout relocated; SceneRouter added.");

            // ---- B. Shop: build the scene shell ----
            var cam = new GameObject("Main Camera");
            var camComp = cam.AddComponent<Camera>();
            camComp.orthographic = true;
            camComp.orthographicSize = 5f;
            camComp.clearFlags = CameraClearFlags.SolidColor;
            camComp.backgroundColor = new Color(0.05f, 0.04f, 0.06f, 1f);
            cam.AddComponent<AudioListener>();
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.tag = "MainCamera";
            SceneManager.MoveGameObjectToScene(cam, shop);

            // EventSystem — duplicate Combat's so the InputSystemUIInputModule config carries.
            GameObject shopEventSystem = Object.Instantiate(Require(combat, "EventSystem"));
            shopEventSystem.name = "EventSystem";
            SceneManager.MoveGameObjectToScene(shopEventSystem, shop);

            // Canvas — duplicate Combat's, strip its children → a bare Canvas with the exact
            // Canvas / CanvasScaler / GraphicRaycaster configuration.
            GameObject shopCanvas = Object.Instantiate(Require(combat, "Canvas"));
            shopCanvas.name = "Canvas";
            StripChildren(shopCanvas);
            SceneManager.MoveGameObjectToScene(shopCanvas, shop);

            var sceneRoot = new GameObject("SceneRoot");
            SceneManager.MoveGameObjectToScene(sceneRoot, shop);

            var shopBootstrap = new GameObject("ShopSceneBootstrap");
            shopBootstrap.AddComponent<ShopSceneBootstrap>();
            SceneManager.MoveGameObjectToScene(shopBootstrap, shop);
            shopBootstrap.transform.SetParent(sceneRoot.transform, false);
            Debug.Log("[WS015] Shop: camera, canvas, event system, scene root, bootstrap created.");

            // ---- C. Move the shop UI Combat -> Shop ----
            GameObject shopPanel = Require(combat, "ShopOverlayPanel");
            MoveChildUnder(shopPanel, shop, shopCanvas.transform);
            // Authored inactive (the old overlay was toggled on by an event). The shop is its
            // own scene now, so the panel must start visible.
            shopPanel.SetActive(true);

            MoveChildUnder(Require(combat, "TooltipController"), shop, shopCanvas.transform);

            MoveRootUnder(Require(combat, "ShopController"), shop, sceneRoot.transform);
            MoveRootUnder(Require(combat, "ShopOverlayController"), shop, sceneRoot.transform);
            MoveRootUnder(Require(combat, "DragModeService"), shop, sceneRoot.transform);
            MoveRootUnder(Require(combat, "RunRestoreController"), shop, sceneRoot.transform);
            Debug.Log("[WS015] Shop: shop-UI subtree + shop services moved in.");

            // ---- D. Duplicate the Pause + Settings menus into Shop ----
            GameObject shopSettings = Object.Instantiate(Require(combat, "SettingsMenu"));
            shopSettings.name = "SettingsMenu";
            SceneManager.MoveGameObjectToScene(shopSettings, shop);
            shopSettings.transform.SetParent(shopCanvas.transform, false);

            GameObject shopPause = Object.Instantiate(Require(combat, "PauseMenu"));
            shopPause.name = "PauseMenu";
            SceneManager.MoveGameObjectToScene(shopPause, shop);
            shopPause.transform.SetParent(shopCanvas.transform, false);

            WirePauseMenuSettings(shopPause, shopSettings);
            Debug.Log("[WS015] Shop: pause + settings menus duplicated and wired.");

            // ---- E. Rename the Combat bootstrap GameObject ----
            GameObject bootstrapGO = FindInScene(combat, "GameSceneBootstrap");
            if (bootstrapGO != null)
            {
                bootstrapGO.name = "CombatSceneBootstrap";
                Debug.Log("[WS015] Combat: GameSceneBootstrap GameObject renamed to CombatSceneBootstrap.");
            }
            else
            {
                Debug.LogWarning("[WS015] Combat: no 'GameSceneBootstrap' GameObject found to rename.");
            }

            // ---- F. Save scenes + register Shop in Build Settings ----
            EditorSceneManager.MarkSceneDirty(combat);
            EditorSceneManager.MarkSceneDirty(boot);
            EditorSceneManager.MarkSceneDirty(shop);
            EditorSceneManager.SaveScene(shop, ShopPath);
            EditorSceneManager.SaveScene(combat);
            EditorSceneManager.SaveScene(boot);
            AddToBuildSettings(ShopPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ReportScene(boot);
            ReportScene(combat);
            ReportScene(shop);
            Debug.Log("[WS015] Scene surgery COMPLETE.");
        }

        // ---- Helpers ----

        private static GameObject Require(Scene scene, string name)
        {
            GameObject go = FindInScene(scene, name);
            if (go == null)
            {
                string msg = $"[WS015] FATAL: GameObject '{name}' not found in scene '{scene.name}'. Aborting.";
                Debug.LogError(msg);
                throw new System.InvalidOperationException(msg);
            }
            return go;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject found = FindRecursive(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindRecursive(Transform t, string name)
        {
            if (t.name == name) return t.gameObject;
            foreach (Transform child in t)
            {
                GameObject found = FindRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // Moves a root GameObject to another scene and re-parents it under <paramref name="parent"/>.
        private static void MoveRootUnder(GameObject go, Scene target, Transform parent)
        {
            SceneManager.MoveGameObjectToScene(go, target);
            go.transform.SetParent(parent, false);
        }

        // Moves a nested GameObject to another scene: detach to a root first (MoveGameObjectToScene
        // only accepts roots), move, then re-parent. SetParent(..., false) keeps local RectTransform
        // values so the layout is identical under the destination Canvas.
        private static void MoveChildUnder(GameObject go, Scene target, Transform parent)
        {
            go.transform.SetParent(null, false);
            SceneManager.MoveGameObjectToScene(go, target);
            go.transform.SetParent(parent, false);
        }

        private static void StripChildren(GameObject go)
        {
            var children = new List<GameObject>();
            foreach (Transform child in go.transform) children.Add(child.gameObject);
            foreach (GameObject child in children) Object.DestroyImmediate(child);
        }

        private static void WirePauseMenuSettings(GameObject pauseGO, GameObject settingsGO)
        {
            Component pauseCtrl = pauseGO.GetComponent("PauseMenuController");
            Component settingsCtrl = settingsGO.GetComponent("SettingsMenuController");
            if (pauseCtrl == null || settingsCtrl == null)
            {
                Debug.LogError("[WS015] Pause/Settings controller component missing — cannot wire _settingsMenu.");
                return;
            }
            var so = new SerializedObject(pauseCtrl);
            SerializedProperty prop = so.FindProperty("_settingsMenu");
            if (prop == null)
            {
                Debug.LogError("[WS015] PauseMenuController._settingsMenu serialized property not found.");
                return;
            }
            prop.objectReferenceValue = settingsCtrl;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene s in scenes)
                if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[WS015] Build Settings: registered {path}.");
        }

        private static void ReportScene(Scene scene)
        {
            int missing = 0;
            var roots = scene.GetRootGameObjects();
            var names = new List<string>();
            foreach (GameObject root in roots)
            {
                names.Add(root.name);
                missing += CountMissingScripts(root.transform);
            }
            Debug.Log($"[WS015] Scene '{scene.name}' roots: {string.Join(", ", names)} | missing scripts: {missing}");
        }

        private static int CountMissingScripts(Transform t)
        {
            int count = 0;
            foreach (Component c in t.GetComponents<Component>())
                if (c == null) count++;
            foreach (Transform child in t)
                count += CountMissingScripts(child);
            return count;
        }

        /// <summary>
        /// WS-015 part 5 validation. Run via
        /// <c>-executeMethod HellpitRampage.EditorTools.WS015SceneRefactor.Validate</c>.
        /// Opens Boot/Combat/Shop, scans for missing scripts and null serialized object
        /// references, and writes a report to Temp/ws015/validation.txt (Debug.Log output
        /// is unreliable under -executeMethod, so the report goes to a file).
        /// </summary>
        public static void Validate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("WS-015 scene validation report");
            ValidateScene(BootPath, sb);
            ValidateScene(CombatPath, sb);
            ValidateScene(ShopPath, sb);
            string outPath = Path.GetFullPath("Temp/ws015/validation.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[WS015] Validation report written to {outPath}");
        }

        private static void ValidateScene(string scenePath, StringBuilder sb)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int missing = 0, monos = 0, nullRefs = 0;
            var lines = new List<string>();
            var rootNames = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                rootNames.Add(root.name);
                WalkValidate(root.transform, lines, ref missing, ref monos, ref nullRefs);
            }
            sb.AppendLine();
            sb.AppendLine($"=== {scene.name} ===");
            sb.AppendLine($"roots: {string.Join(", ", rootNames)}");
            sb.AppendLine($"totals: missingScripts={missing}, monoBehaviours={monos}, nullObjectRefs={nullRefs}");
            foreach (string line in lines) sb.AppendLine(line);
        }

        private static void WalkValidate(Transform t, List<string> lines, ref int missing, ref int monos, ref int nullRefs)
        {
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null)
                {
                    missing++;
                    lines.Add($"  MISSING SCRIPT on '{HierarchyPath(t)}'");
                    continue;
                }
                if (c is MonoBehaviour mb)
                {
                    monos++;
                    var so = new SerializedObject(mb);
                    SerializedProperty p = so.GetIterator();
                    while (p.NextVisible(true))
                    {
                        if (p.propertyType == SerializedPropertyType.ObjectReference
                            && p.name.StartsWith("_")
                            && p.objectReferenceValue == null)
                        {
                            nullRefs++;
                            lines.Add($"  null ref: {mb.GetType().Name}.{p.name} on '{HierarchyPath(t)}'");
                        }
                    }
                }
            }
            foreach (Transform child in t)
                WalkValidate(child, lines, ref missing, ref monos, ref nullRefs);
        }

        private static string HierarchyPath(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
