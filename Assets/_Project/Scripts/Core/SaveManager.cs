using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace HellpitRampage.Core
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const string SAVE_FILENAME = "save.json";

        internal string _savePathOverride;

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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

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

        private SaveData MigrateIfNeeded(SaveData data)
        {
            if (data.SchemaVersion != 1)
            {
                Debug.LogWarning($"Save schema version mismatch: {data.SchemaVersion}. No migration available yet.");
            }
            return data;
        }
    }
}
