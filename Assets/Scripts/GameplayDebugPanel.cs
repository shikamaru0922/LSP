using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Lightweight in-game debug control surface that exposes the core gameplay
    /// tuning values required by the design team. The panel can be toggled at
    /// runtime and allows editing movement speeds, eye wetness behaviour, camera settings, audio volume,
    /// and provides visibility into the current state of monsters, NPCs, and the player.
    /// </summary>
    public class GameplayDebugPanel : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote;

        [Tooltip("Whether the panel should be visible when entering play mode.")]
        [SerializeField] private bool startVisible = true;

        [Header("Player References")]
        [SerializeField] private PlayerStateController playerState;
        [SerializeField] private PlayerEyeControl eyeControl;
        [SerializeField] private PlayerVision playerVision;
        [SerializeField] private Camera playerCamera;

        [Header("Tracked Collections")]
        [SerializeField] private List<MonsterController> trackedMonsters = new List<MonsterController>();
        [SerializeField] private List<NpcDeadStareController> trackedNpcs = new List<NpcDeadStareController>();
        [SerializeField] private List<AudioSource> managedAudioSources = new List<AudioSource>();

        [Tooltip("Automatically populates the collections above on Start().")]
        [SerializeField] private bool autoPopulateOnStart = true;

        [Header("Ranges")]
        [SerializeField] private float playerSpeedMin = 0f;
        [SerializeField] private float playerSpeedMax = 10f;

        [SerializeField] private float monsterSpeedMin = 0f;
        [SerializeField] private float monsterSpeedMax = 10f;

        [SerializeField] private float masterVolumeMin = 0f;
        [SerializeField] private float masterVolumeMax = 1f;

        [Header("Camera Ranges")]
        [SerializeField] private float perspectiveFovMin = 40f;
        [SerializeField] private float perspectiveFovMax = 100f;
        [SerializeField] private float orthographicSizeMin = 1f;
        [SerializeField] private float orthographicSizeMax = 20f;

        [Header("Eye Wetness Ranges")]
        [SerializeField] private float eyeMaximumWetnessMin = 1f;
        [SerializeField] private float eyeMaximumWetnessMax = 10f;

        [SerializeField] private float eyeDryingRateMin = 0f;
        [SerializeField] private float eyeDryingRateMax = 5f;

        [SerializeField] private float eyeRecoveryRateMin = 0f;
        [SerializeField] private float eyeRecoveryRateMax = 5f;

        [SerializeField] private float eyeForcedOpenThresholdMin = 0f;
        [SerializeField] private float eyeForcedOpenThresholdMax = 10f;

        [SerializeField] private float eyeForcedCloseDurationMin = 0f;
        [SerializeField] private float eyeForcedCloseDurationMax = 10f;

        [Header("Persistence")]
        [Tooltip("File name used when persisting debug-tuned values. Stored under Application.persistentDataPath.")]
        [SerializeField] private string settingsFileName = "gameplay-debug-settings.json";

        [Header("Cursor")]
        [Tooltip("When enabled, the cursor is unlocked and made visible whenever the panel is open so designers can interact with the controls.")]
        [SerializeField] private bool unlockCursorWhileVisible = true;

        [Header("Audio Replacement")]
        [Tooltip("Clip that will be assigned to all managed audio sources when the replace button is pressed.")]
        [SerializeField] private AudioClip replacementClip;

        private readonly Dictionary<AudioSource, float> audioBaseVolumes = new Dictionary<AudioSource, float>();
        private bool isVisible;
        private Rect windowRect = new Rect(16f, 16f, 420f, 540f);
        private Vector2 scrollPosition;
        private float cachedMasterVolume = 1f;
        private float cachedMonsterSpeed;
        private GUIStyle headerStyle;

        // Persistence runtime cache
        private GameplayDebugSettingsData persistedSettings;
        private string settingsFilePath;

        // Cursor state cache
        private bool cursorStateCached;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        private void Awake()
        {
            isVisible = startVisible;

            if (playerState == null)
                playerState = FindObjectOfType<PlayerStateController>();

            if (eyeControl == null && playerState != null)
            {
                eyeControl = playerState.GetComponent<PlayerEyeControl>();
                if (eyeControl == null) eyeControl = playerState.GetComponentInChildren<PlayerEyeControl>();
            }

            if (playerVision == null && playerState != null)
            {
                playerVision = playerState.GetComponent<PlayerVision>();
                if (playerVision == null) playerVision = playerState.GetComponentInChildren<PlayerVision>();
            }

            if (playerCamera == null)
                playerCamera = TryResolveCamera();

            settingsFilePath = GameplayDebugSettingsStore.ResolvePath(settingsFileName);
            persistedSettings = GameplayDebugSettingsStore.Load(settingsFileName);
            cachedMasterVolume = Mathf.Clamp(persistedSettings.MasterVolume, masterVolumeMin, masterVolumeMax);
        }

        private void Start()
        {
            if (autoPopulateOnStart) RefreshTrackedObjects();
            else CacheAudioSources();

            ApplyPersistedSettings();
            SyncCachedValues();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                isVisible = !isVisible;
                UpdateCursorState();
            }

            RemoveDestroyedReferences();

            if (managedAudioSources.Count > audioBaseVolumes.Count)
                CacheAudioSources();

            if (unlockCursorWhileVisible)
                UpdateCursorState();
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindowContents, "Gameplay Debug Panel");
        }

        private void DrawWindowContents(int windowId)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);

            DrawPlayerControls();
            GUILayout.Space(8f);
            DrawMonsterControls();
            GUILayout.Space(8f);
            DrawCameraControls();
            GUILayout.Space(8f);
            DrawAudioControls();
            GUILayout.Space(8f);
            DrawStateReadout();

            GUILayout.Space(12f);
            if (GUILayout.Button("Refresh tracked objects"))
            {
                RefreshTrackedObjects();
            }

            GUILayout.Space(12f);
            GUILayout.Label($"Settings file: {settingsFilePath}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save settings")) SaveSettingsToDisk();
            if (GUILayout.Button("Reload settings")) ReloadSettingsFromDisk();
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawPlayerControls()
        {
            GUILayout.Label("Player Controls", headerStyle);

            if (playerState == null)
            {
                GUILayout.Label("PlayerStateController not assigned.");
                return;
            }

            GUILayout.Label($"Life State: {(playerState.IsAlive ? "Alive" : "Dead")}");

            float currentSpeed = playerState.MovementSpeed;
            float newSpeed = DrawSlider("Movement Speed", currentSpeed, playerSpeedMin, playerSpeedMax);
            if (!Mathf.Approximately(newSpeed, currentSpeed))
            {
                playerState.MovementSpeed = newSpeed;
                persistedSettings.PlayerSpeed = newSpeed;
            }

            // Eye controls
            GameplayDebugSettingsData settings = persistedSettings ??= new GameplayDebugSettingsData();

            if (eyeControl != null)
            {
                GUILayout.Label($"Eyes Open: {eyeControl.EyesOpen}");
                GUILayout.Label($"Forced Closed: {eyeControl.IsForcedClosing}");
                GUILayout.Label($"Wetness: {eyeControl.CurrentWetness:F2} / {eyeControl.MaximumWetness:F2}");
            }
            else
            {
                GUILayout.Label("PlayerEyeControl not assigned.");
            }

            float currentMaxWetness = eyeControl != null ? eyeControl.MaximumWetness : settings.EyeMaximumWetness;
            float newMaxWetness = DrawSlider("Max Wetness", currentMaxWetness, eyeMaximumWetnessMin, eyeMaximumWetnessMax);
            if (!Mathf.Approximately(newMaxWetness, currentMaxWetness))
            {
                if (eyeControl != null) eyeControl.MaximumWetness = newMaxWetness;
                settings.EyeMaximumWetness = newMaxWetness;

                if (settings.EyeForcedOpenThreshold > newMaxWetness)
                    settings.EyeForcedOpenThreshold = Mathf.Min(newMaxWetness, eyeForcedOpenThresholdMax);
            }

            float currentDryingRate = eyeControl != null ? eyeControl.DryingRate : settings.EyeDryingRate;
            float newDryingRate = DrawSlider("Drying Rate", currentDryingRate, eyeDryingRateMin, eyeDryingRateMax);
            if (!Mathf.Approximately(newDryingRate, currentDryingRate))
            {
                if (eyeControl != null) eyeControl.DryingRate = newDryingRate;
                settings.EyeDryingRate = newDryingRate;
            }

            float currentRecoveryRate = eyeControl != null ? eyeControl.RecoveryRate : settings.EyeRecoveryRate;
            float newRecoveryRate = DrawSlider("Recovery Rate", currentRecoveryRate, eyeRecoveryRateMin, eyeRecoveryRateMax);
            if (!Mathf.Approximately(newRecoveryRate, currentRecoveryRate))
            {
                if (eyeControl != null) eyeControl.RecoveryRate = newRecoveryRate;
                settings.EyeRecoveryRate = newRecoveryRate;
            }

            float forcedOpenMax = Mathf.Min(
                eyeForcedOpenThresholdMax,
                Mathf.Max(eyeForcedOpenThresholdMin, eyeControl != null ? eyeControl.MaximumWetness : settings.EyeMaximumWetness)
            );
            float currentForcedOpenThreshold = eyeControl != null ? eyeControl.ForcedOpenThreshold : settings.EyeForcedOpenThreshold;
            currentForcedOpenThreshold = Mathf.Clamp(currentForcedOpenThreshold, eyeForcedOpenThresholdMin, forcedOpenMax);
            float newForcedOpenThreshold = DrawSlider("Forced Open Threshold", currentForcedOpenThreshold, eyeForcedOpenThresholdMin, forcedOpenMax);
            if (!Mathf.Approximately(newForcedOpenThreshold, currentForcedOpenThreshold))
            {
                if (eyeControl != null) eyeControl.ForcedOpenThreshold = newForcedOpenThreshold;
                settings.EyeForcedOpenThreshold = newForcedOpenThreshold;
            }

            float currentForcedCloseDuration = eyeControl != null ? eyeControl.ForcedCloseDuration : settings.EyeForcedCloseDuration;
            float newForcedCloseDuration = DrawSlider("Forced Close Duration", currentForcedCloseDuration, eyeForcedCloseDurationMin, eyeForcedCloseDurationMax);
            if (!Mathf.Approximately(newForcedCloseDuration, currentForcedCloseDuration))
            {
                if (eyeControl != null) eyeControl.ForcedCloseDuration = newForcedCloseDuration;
                settings.EyeForcedCloseDuration = newForcedCloseDuration;
            }

            if (playerVision != null)
            {
                GUILayout.Label($"Vision Range: {playerVision.MaxDetectionDistance:F1}");
            }
        }

        private void DrawMonsterControls()
        {
            GUILayout.Label("Monster Controls", headerStyle);

            if (trackedMonsters.Count == 0)
            {
                GUILayout.Label("No monsters tracked.");
                return;
            }

            float currentSpeed = GetRepresentativeMonsterSpeed();
            float newSpeed = DrawSlider("Move Speed", currentSpeed, monsterSpeedMin, monsterSpeedMax);
            if (!Mathf.Approximately(newSpeed, currentSpeed))
            {
                ApplyMonsterSpeed(newSpeed);
            }

            GUILayout.Label($"Tracked Monsters: {trackedMonsters.Count}");
        }

        private void DrawCameraControls()
        {
            GUILayout.Label("Camera", headerStyle);

            if (playerCamera == null)
            {
                GUILayout.Label("Camera reference not assigned.");
                return;
            }

            if (playerCamera.orthographic)
            {
                float currentSize = playerCamera.orthographicSize;
                float newSize = DrawSlider("Orthographic Size", currentSize, orthographicSizeMin, orthographicSizeMax);
                if (!Mathf.Approximately(newSize, currentSize))
                {
                    playerCamera.orthographicSize = newSize;
                    persistedSettings.CameraOrthographicSize = newSize;
                }
            }
            else
            {
                float currentFov = playerCamera.fieldOfView;
                float newFov = DrawSlider("Field of View", currentFov, perspectiveFovMin, perspectiveFovMax);
                if (!Mathf.Approximately(newFov, currentFov))
                {
                    playerCamera.fieldOfView = newFov;
                    persistedSettings.CameraFieldOfView = newFov;
                }
            }
        }

        private void DrawAudioControls()
        {
            GUILayout.Label("Audio", headerStyle);

            if (managedAudioSources.Count == 0)
            {
                GUILayout.Label("No audio sources tracked.");
                return;
            }

            float newVolume = DrawSlider("Master Volume", cachedMasterVolume, masterVolumeMin, masterVolumeMax);
            if (!Mathf.Approximately(newVolume, cachedMasterVolume))
            {
                cachedMasterVolume = newVolume;
                ApplyMasterVolume();
                persistedSettings.MasterVolume = cachedMasterVolume;
            }

            GUILayout.Label($"Tracked Sources: {CountActiveAudioSources()}");
            GUILayout.Label($"Replacement Clip: {(replacementClip != null ? replacementClip.name : "<none>")}");

            GUI.enabled = replacementClip != null;
            if (GUILayout.Button("Apply replacement clip"))
            {
                ApplyReplacementClip();
            }
            GUI.enabled = true;
        }

        private void DrawStateReadout()
        {
            GUILayout.Label("State Readout", headerStyle);

            GameManager manager = GameManager.Instance;
            bool worldAbnormal = manager != null && manager.IsWorldAbnormal;
            GUILayout.Label($"World Abnormal: {worldAbnormal}");

            if (playerState != null)
            {
                GUILayout.Label($"Player: {(playerState.IsAlive ? "Alive" : "Dead")}");
            }

            if (trackedMonsters.Count > 0)
            {
                GUILayout.Label($"Monsters ({trackedMonsters.Count}):");
                foreach (MonsterController monster in trackedMonsters)
                {
                    if (monster == null) continue;
                    GUILayout.Label($" • {monster.name}: {monster.CurrentState} @ {monster.CurrentMoveSpeed:F2} u/s");
                }
            }
            else
            {
                GUILayout.Label("Monsters: none");
            }

            if (trackedNpcs.Count > 0)
            {
                GUILayout.Label($"NPCs ({trackedNpcs.Count}):");
                foreach (NpcDeadStareController npc in trackedNpcs)
                {
                    if (npc == null) continue;
                    GUILayout.Label($" • {npc.name}: {npc.CurrentState}");
                }
            }
            else
            {
                GUILayout.Label("NPCs: none");
            }
        }

        private float DrawSlider(string label, float currentValue, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150f));
            float value = GUILayout.HorizontalSlider(currentValue, min, max);
            GUILayout.Space(8f);
            GUILayout.Label(value.ToString("F2"), GUILayout.Width(60f));
            GUILayout.EndHorizontal();
            return value;
        }

        private void RefreshTrackedObjects()
        {
            RemoveDestroyedReferences();

            if (playerState == null) playerState = FindObjectOfType<PlayerStateController>();
            if (eyeControl == null) eyeControl = FindObjectOfType<PlayerEyeControl>();
            if (playerVision == null) playerVision = FindObjectOfType<PlayerVision>();
            if (playerCamera == null) playerCamera = TryResolveCamera();

            AddUniqueRange(trackedMonsters, FindObjectsOfType<MonsterController>());
            AddUniqueRange(trackedNpcs, FindObjectsOfType<NpcDeadStareController>());
            AddUniqueRange(managedAudioSources, FindObjectsOfType<AudioSource>());

            CacheAudioSources();
            SyncCachedValues();
        }

        private void RemoveDestroyedReferences()
        {
            trackedMonsters.RemoveAll(monster => monster == null);
            trackedNpcs.RemoveAll(npc => npc == null);
            managedAudioSources.RemoveAll(source => source == null);

            if (audioBaseVolumes.Count > 0)
            {
                List<AudioSource> keysToRemove = null;
                foreach (KeyValuePair<AudioSource, float> entry in audioBaseVolumes)
                {
                    if (entry.Key == null || !managedAudioSources.Contains(entry.Key))
                    {
                        keysToRemove ??= new List<AudioSource>();
                        keysToRemove.Add(entry.Key);
                    }
                }

                if (keysToRemove != null)
                {
                    foreach (AudioSource key in keysToRemove)
                        audioBaseVolumes.Remove(key);
                }
            }

            cachedMonsterSpeed = Mathf.Clamp(GetRepresentativeMonsterSpeed(), monsterSpeedMin, monsterSpeedMax);
        }

        private void AddUniqueRange<T>(List<T> target, T[] additions) where T : UnityEngine.Object
        {
            foreach (T element in additions)
            {
                if (element == null || target.Contains(element)) continue;
                target.Add(element);
            }
        }

        private void CacheAudioSources()
        {
            foreach (AudioSource source in managedAudioSources)
            {
                if (source == null) continue;
                if (audioBaseVolumes.ContainsKey(source)) continue;

                float baseVolume = source.volume;
                if (cachedMasterVolume > 0.0001f) baseVolume = source.volume / cachedMasterVolume;

                audioBaseVolumes[source] = Mathf.Max(0f, baseVolume);
            }

            ApplyMasterVolume();
        }

        private void ApplyMasterVolume()
        {
            foreach (KeyValuePair<AudioSource, float> entry in audioBaseVolumes)
            {
                AudioSource source = entry.Key;
                if (source == null) continue;
                source.volume = entry.Value * cachedMasterVolume;
            }
        }

        private void ApplyReplacementClip()
        {
            if (replacementClip == null) return;

            foreach (AudioSource source in managedAudioSources)
            {
                if (source == null) continue;
                source.clip = replacementClip;
            }
        }

        private int CountActiveAudioSources()
        {
            int count = 0;
            foreach (AudioSource source in managedAudioSources)
            {
                if (source != null) count++;
            }
            return count;
        }

        private void SyncCachedValues()
        {
            if (playerCamera == null) playerCamera = TryResolveCamera();

            cachedMasterVolume = Mathf.Clamp(cachedMasterVolume, masterVolumeMin, masterVolumeMax);
            cachedMonsterSpeed = GetRepresentativeMonsterSpeed();
            if (cachedMonsterSpeed <= 0f)
                cachedMonsterSpeed = Mathf.Clamp(cachedMonsterSpeed, monsterSpeedMin, monsterSpeedMax);

            CaptureCurrentValuesForPersistence();
        }

        private Camera TryResolveCamera()
        {
            if (playerVision != null)
            {
                Camera candidate = playerVision.GetComponentInChildren<Camera>();
                if (candidate != null) return candidate;
            }

            PlayerCameraBinder binder = FindObjectOfType<PlayerCameraBinder>();
            if (binder != null)
            {
                Camera candidate = binder.GetComponent<Camera>();
                if (candidate != null) return candidate;
            }

            return Camera.main;
        }

        private float GetRepresentativeMonsterSpeed()
        {
            foreach (MonsterController monster in trackedMonsters)
            {
                if (monster != null) return monster.CurrentMoveSpeed;
            }
            return cachedMonsterSpeed > 0f ? cachedMonsterSpeed : monsterSpeedMin;
        }

        private void ApplyMonsterSpeed(float speed)
        {
            cachedMonsterSpeed = Mathf.Clamp(speed, monsterSpeedMin, monsterSpeedMax);

            foreach (MonsterController monster in trackedMonsters)
            {
                if (monster == null) continue;
                monster.SetMoveSpeed(cachedMonsterSpeed);
            }

            persistedSettings.MonsterSpeed = cachedMonsterSpeed;
        }

        private void ApplyPersistedSettings()
        {
            if (persistedSettings == null) persistedSettings = new GameplayDebugSettingsData();

            if (playerState != null)
            {
                float playerSpeed = Mathf.Clamp(persistedSettings.PlayerSpeed, playerSpeedMin, playerSpeedMax);
                playerState.MovementSpeed = playerSpeed;
            }

            float monsterSpeed = Mathf.Clamp(persistedSettings.MonsterSpeed, monsterSpeedMin, monsterSpeedMax);
            if (trackedMonsters.Count > 0) ApplyMonsterSpeed(monsterSpeed);
            else cachedMonsterSpeed = monsterSpeed;

            // Eye settings clamp + apply
            float clampedMaxWetness = Mathf.Clamp(persistedSettings.EyeMaximumWetness, eyeMaximumWetnessMin, eyeMaximumWetnessMax);
            float clampedDryingRate = Mathf.Clamp(persistedSettings.EyeDryingRate, eyeDryingRateMin, eyeDryingRateMax);
            float clampedRecoveryRate = Mathf.Clamp(persistedSettings.EyeRecoveryRate, eyeRecoveryRateMin, eyeRecoveryRateMax);
            float forcedOpenUpperBound = Mathf.Min(eyeForcedOpenThresholdMax, Mathf.Max(eyeForcedOpenThresholdMin, clampedMaxWetness));
            float clampedForcedOpenThreshold = Mathf.Clamp(persistedSettings.EyeForcedOpenThreshold, eyeForcedOpenThresholdMin, forcedOpenUpperBound);
            float clampedForcedCloseDuration = Mathf.Clamp(persistedSettings.EyeForcedCloseDuration, eyeForcedCloseDurationMin, eyeForcedCloseDurationMax);

            persistedSettings.EyeMaximumWetness = clampedMaxWetness;
            persistedSettings.EyeDryingRate = clampedDryingRate;
            persistedSettings.EyeRecoveryRate = clampedRecoveryRate;
            persistedSettings.EyeForcedOpenThreshold = clampedForcedOpenThreshold;
            persistedSettings.EyeForcedCloseDuration = clampedForcedCloseDuration;

            if (eyeControl != null)
            {
                eyeControl.MaximumWetness = clampedMaxWetness;
                eyeControl.DryingRate = clampedDryingRate;
                eyeControl.RecoveryRate = clampedRecoveryRate;
                eyeControl.ForcedOpenThreshold = clampedForcedOpenThreshold;
                eyeControl.ForcedCloseDuration = clampedForcedCloseDuration;
            }

            // Camera
            if (playerCamera != null)
            {
                if (playerCamera.orthographic)
                {
                    playerCamera.orthographicSize = Mathf.Clamp(persistedSettings.CameraOrthographicSize, orthographicSizeMin, orthographicSizeMax);
                }
                else
                {
                    playerCamera.fieldOfView = Mathf.Clamp(persistedSettings.CameraFieldOfView, perspectiveFovMin, perspectiveFovMax);
                }
            }

            cachedMasterVolume = Mathf.Clamp(persistedSettings.MasterVolume, masterVolumeMin, masterVolumeMax);
            ApplyMasterVolume();
        }

        private void CaptureCurrentValuesForPersistence()
        {
            if (persistedSettings == null) persistedSettings = new GameplayDebugSettingsData();

            if (playerState != null)
                persistedSettings.PlayerSpeed = playerState.MovementSpeed;

            persistedSettings.MonsterSpeed = cachedMonsterSpeed;
            persistedSettings.MasterVolume = cachedMasterVolume;

            if (eyeControl != null)
            {
                persistedSettings.EyeMaximumWetness = Mathf.Clamp(eyeControl.MaximumWetness, eyeMaximumWetnessMin, eyeMaximumWetnessMax);
                persistedSettings.EyeDryingRate = Mathf.Clamp(eyeControl.DryingRate, eyeDryingRateMin, eyeDryingRateMax);
                persistedSettings.EyeRecoveryRate = Mathf.Clamp(eyeControl.RecoveryRate, eyeRecoveryRateMin, eyeRecoveryRateMax);
                float forcedOpenUpperBound = Mathf.Min(eyeForcedOpenThresholdMax, Mathf.Max(eyeForcedOpenThresholdMin, persistedSettings.EyeMaximumWetness));
                persistedSettings.EyeForcedOpenThreshold = Mathf.Clamp(eyeControl.ForcedOpenThreshold, eyeForcedOpenThresholdMin, forcedOpenUpperBound);
                persistedSettings.EyeForcedCloseDuration = Mathf.Clamp(eyeControl.ForcedCloseDuration, eyeForcedCloseDurationMin, eyeForcedCloseDurationMax);
            }

            if (playerCamera != null)
            {
                if (playerCamera.orthographic)
                    persistedSettings.CameraOrthographicSize = playerCamera.orthographicSize;
                else
                    persistedSettings.CameraFieldOfView = playerCamera.fieldOfView;
            }
        }

        private void SaveSettingsToDisk()
        {
            CaptureCurrentValuesForPersistence();
            GameplayDebugSettingsStore.Save(persistedSettings, settingsFileName);
        }

        private void ReloadSettingsFromDisk()
        {
            persistedSettings = GameplayDebugSettingsStore.Load(settingsFileName);
            ApplyPersistedSettings();
            SyncCachedValues();
        }

        private void UpdateCursorState()
        {
            if (!unlockCursorWhileVisible) return;

            if (isVisible)
            {
                if (!cursorStateCached)
                {
                    previousCursorLock = Cursor.lockState;
                    previousCursorVisible = Cursor.visible;
                    cursorStateCached = true;
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (cursorStateCached)
            {
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
                cursorStateCached = false;
            }
        }

        private void OnDisable()
        {
            if (cursorStateCached)
            {
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
                cursorStateCached = false;
            }
        }
    }
}
