using System;
using System.IO;
using UnityEngine;

namespace LSP.Gameplay
{
    [Serializable]
    public class GameplayDebugSettingsData
    {
        public float PlayerSpeed = 4f;
        public float MonsterSpeed = 2.5f;
        public float MasterVolume = 1f;
        public float EyeMaximumWetness = 5f;
        public float EyeDryingRate = 1f;
        public float EyeRecoveryRate = 2f;
        public float EyeForcedOpenThreshold = 1.5f;
        public float EyeForcedCloseDuration = 2.5f;
    }

    public static class GameplayDebugSettingsStore
    {
        private const string DefaultFileName = "gameplay-debug-settings.json";

        public static GameplayDebugSettingsData Load(string fileName = null)
        {
            string path = ResolvePath(fileName);
            if (!File.Exists(path))
            {
                return new GameplayDebugSettingsData();
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json))
                {
                    return new GameplayDebugSettingsData();
                }

                GameplayDebugSettingsData data = JsonUtility.FromJson<GameplayDebugSettingsData>(json);
                return data ?? new GameplayDebugSettingsData();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load gameplay debug settings from '{path}': {ex}");
                return new GameplayDebugSettingsData();
            }
        }

        public static void Save(GameplayDebugSettingsData data, string fileName = null)
        {
            if (data == null)
            {
                Debug.LogWarning("GameplayDebugSettingsStore.Save was called with null data.");
                return;
            }

            string path = ResolvePath(fileName);
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to save gameplay debug settings to '{path}': {ex}");
            }
        }

        public static string ResolvePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = DefaultFileName;
            }

            return Path.Combine(Application.persistentDataPath, fileName);
        }
    }
}
