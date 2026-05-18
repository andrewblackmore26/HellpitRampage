using System;
using System.IO;
using HellpitRampage.Inventory;
using Newtonsoft.Json;
using UnityEngine;

namespace HellpitRampage.Core
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        // WS-012.6: meta + profile placeholder (single combined file from launch — kept as-is).
        private const string SAVE_FILENAME = "save.json";
        // WS-012.6: settings.
        private const string SETTINGS_FILENAME = "settings.json";
        // WS-013: between-rounds run state. Created on shop-phase entry, deleted on run end.
        private const string RUN_SAVE_FILENAME = "run_save.json";

        // WS-013: schema version checked on every load; mismatch → treated as no save.
        public const int CurrentRunSchemaVersion = 1;

        internal string _savePathOverride;
        internal string _settingsPathOverride;
        internal string _runSavePathOverride;

        public string SavePath
        {
            get
            {
                if (!string.IsNullOrEmpty(_savePathOverride))
                {
                    return _savePathOverride;
                }
                return Path.Combine(Application.persistentDataPath, SAVE_FILENAME);
            }
        }

        public string SettingsPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_settingsPathOverride))
                {
                    return _settingsPathOverride;
                }
                return Path.Combine(Application.persistentDataPath, SETTINGS_FILENAME);
            }
        }

        public string RunSavePath
        {
            get
            {
                if (!string.IsNullOrEmpty(_runSavePathOverride))
                {
                    return _runSavePathOverride;
                }
                return Path.Combine(Application.persistentDataPath, RUN_SAVE_FILENAME);
            }
        }

        // WS-013: suppresses the auto-save handler while RunRestoreController is rebuilding
        // state — the ShopPhaseStartedEvent that restore emits would otherwise trigger an
        // immediate same-content rewrite.
        private bool _restoringFromSave;

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
            // Guarded: DontDestroyOnLoad is play-mode-only and throws when this singleton
            // is instantiated in an EditMode test.
            if (Application.isPlaying) DontDestroyOnLoad(transform.root.gameObject);
        }

        private void OnEnable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<ShopPhaseStartedEvent>(HandleShopPhaseStarted);
                EventBus.Instance.Subscribe<RunEndedEvent>(HandleRunEnded);
            }
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<ShopPhaseStartedEvent>(HandleShopPhaseStarted);
                EventBus.Instance.Unsubscribe<RunEndedEvent>(HandleRunEnded);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // -------------------- Legacy (meta + profile) save --------------------

        public void Save(SaveData data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveManager: failed to save to '{SavePath}': {ex}");
            }
        }

        public SaveData Load()
        {
            if (!File.Exists(SavePath))
            {
                return new SaveData();
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                SaveData data = JsonConvert.DeserializeObject<SaveData>(json);
                if (data == null)
                {
                    Debug.LogError($"SaveManager: deserialization returned null from '{SavePath}'. Returning fresh SaveData.");
                    return new SaveData();
                }
                return MigrateIfNeeded(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveManager: failed to load from '{SavePath}': {ex}. Returning fresh SaveData; broken file left in place for debugging.");
                return new SaveData();
            }
        }

        public bool SaveFileExists()
        {
            return File.Exists(SavePath);
        }

        public void SaveSettings(SettingsSaveData data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveManager: failed to save settings to '{SettingsPath}': {ex}");
            }
        }

        public SettingsSaveData LoadSettings()
        {
            if (!File.Exists(SettingsPath))
            {
                return new SettingsSaveData();
            }

            try
            {
                string json = File.ReadAllText(SettingsPath);
                SettingsSaveData data = JsonConvert.DeserializeObject<SettingsSaveData>(json);
                if (data == null)
                {
                    Debug.LogError($"SaveManager: settings deserialization returned null from '{SettingsPath}'. Returning defaults.");
                    return new SettingsSaveData();
                }
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveManager: failed to load settings from '{SettingsPath}': {ex}. Returning defaults; broken file left in place for debugging.");
                return new SettingsSaveData();
            }
        }

        public bool SettingsFileExists()
        {
            return File.Exists(SettingsPath);
        }

        private SaveData MigrateIfNeeded(SaveData data)
        {
            if (data.SchemaVersion != 1)
            {
                Debug.LogWarning($"Save schema version mismatch: {data.SchemaVersion}. No migration available yet.");
            }
            return data;
        }

        // -------------------- WS-013: run save lifecycle --------------------

        /// <summary>
        /// Cheap-but-correct check used by the Main Menu to decide whether to show the
        /// "Resume Run" button. Verifies the file exists, parses, and clears the schema
        /// gate. Full validation (hero ID resolution) happens on the actual load.
        /// </summary>
        public bool HasRunSave()
        {
            if (!File.Exists(RunSavePath)) return false;
            try
            {
                string raw = File.ReadAllText(RunSavePath);
                var data = JsonConvert.DeserializeObject<RunSaveData>(raw);
                if (data == null) return false;
                if (data.SaveFormatVersion != CurrentRunSchemaVersion) return false;
                if (data.CurrentRound <= 0) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public RunSaveData LoadRun()
        {
            try
            {
                if (!File.Exists(RunSavePath)) return null;
                string raw = File.ReadAllText(RunSavePath);
                var data = JsonConvert.DeserializeObject<RunSaveData>(raw);
                if (data == null)
                {
                    Debug.LogWarning($"SaveManager: run save at '{RunSavePath}' parsed to null.");
                    return null;
                }
                if (data.SaveFormatVersion != CurrentRunSchemaVersion)
                {
                    Debug.LogWarning($"SaveManager: run save schema version {data.SaveFormatVersion} does not match current {CurrentRunSchemaVersion}; treating as no save.");
                    return null;
                }
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveManager: failed to load run save from '{RunSavePath}': {ex}. Broken file left in place for debugging.");
                return null;
            }
        }

        public void SaveRun(RunSaveData data)
        {
            if (data == null)
            {
                Debug.LogError("SaveManager.SaveRun: data is null; nothing written.");
                return;
            }
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                WriteAtomic(RunSavePath, json);
                EventBus.Instance?.Publish(new RunSaveCompletedEvent());
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveManager.SaveRun: failed to write '{RunSavePath}': {ex}");
                EventBus.Instance?.Publish(new RunSaveFailedEvent { Reason = ex.Message });
            }
        }

        public void DeleteRunSave()
        {
            try
            {
                if (File.Exists(RunSavePath)) File.Delete(RunSavePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SaveManager.DeleteRunSave: failed to delete '{RunSavePath}': {ex.Message}");
            }
        }

        /// <summary>Mark restore-in-progress; auto-save handler short-circuits while set.</summary>
        public void BeginRestore() => _restoringFromSave = true;

        /// <summary>Clear restore-in-progress flag once the restore controller is done.</summary>
        public void EndRestore() => _restoringFromSave = false;

        private void HandleShopPhaseStarted(ShopPhaseStartedEvent _)
        {
            if (_restoringFromSave) return;
            CaptureAndSaveRun();
        }

        private void HandleRunEnded(RunEndedEvent _)
        {
            DeleteRunSave();
        }

        public void CaptureAndSaveRun()
        {
            var captured = CaptureRunState();
            if (captured == null) return;
            SaveRun(captured);
        }

        private RunSaveData CaptureRunState()
        {
            if (RunManager.Instance == null)
            {
                Debug.LogError("SaveManager.CaptureRunState: RunManager.Instance is null; aborting save.");
                return null;
            }

            var run = RunManager.Instance;
            var data = new RunSaveData
            {
                SaveFormatVersion = CurrentRunSchemaVersion,
                GameVersion = Application.version,
                SavedAtUtc = DateTime.UtcNow.ToString("o"),
                CurrentRound = run.CurrentRound,
                HeroId = run.CurrentHero != null ? run.CurrentHero.Id : null,
                Gold = run.CurrentGold,
            };

            // WS-015: HP is owned by RunManager — the Shop scene, where saves fire, has no
            // player Health component. Capture the canonical value directly.
            data.PlayerCurrentHp = run.CurrentHp;
            data.PlayerMaxHp = run.MaxHp;

            var inv = InventoryService.Instance;
            if (inv != null && inv.Grid != null)
            {
                foreach (var bag in inv.Grid.Bags)
                {
                    if (bag == null || bag.Data == null) continue;
                    data.Bags.Add(new BagSaveEntry
                    {
                        BagId = bag.Data.Id,
                        OriginX = bag.Origin.x,
                        OriginY = bag.Origin.y,
                        IsLocked = bag.IsLocked,
                    });
                }
                foreach (var item in inv.Grid.Items)
                {
                    if (item == null || item.Data == null) continue;
                    data.Items.Add(new ItemSaveEntry
                    {
                        ItemId = item.Data.Id,
                        OriginX = item.Origin.x,
                        OriginY = item.Origin.y,
                        Rotation = (int)item.Rotation,
                        IsLocked = item.IsLocked,
                    });
                }
            }

            // WS-015: ground-item state is persisted on InventoryService (it survives the
            // scene swap); GroundManager is only the Shop scene's view of it.
            if (inv != null)
            {
                foreach (var g in inv.GroundItems)
                {
                    if (g.ItemId == null) continue;
                    data.GroundItems.Add(new GroundItemSaveEntry
                    {
                        ItemId = g.ItemId.Id,
                        Rotation = (int)g.Rotation,
                        IsLocked = g.IsLocked,
                    });
                }
            }

            return data;
        }

        private static void WriteAtomic(string path, string contents)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }
}
