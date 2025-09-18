using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Lightweight in-game debug control surface that exposes the core gameplay
    /// tuning values required by the design team. The panel can be toggled at
    /// runtime and allows editing movement speeds, camera settings, audio volume,
    /// and provides visibility into the current state of monsters, NPCs, and the
    /// player.
    /// </summary>
    public class GameplayDebugPanel : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField]
        private KeyCode toggleKey = KeyCode.BackQuote;

        [Tooltip("Whether the panel should be visible when entering play mode.")]
        [SerializeField]
        private bool startVisible = true;

        [Header("Player References")]
        [SerializeField]
        private PlayerStateController playerState;

        [SerializeField]
        private PlayerEyeControl eyeControl;

        [SerializeField]
        private PlayerVision playerVision;

        [SerializeField]
        private Camera playerCamera;

        [Header("Tracked Collections")]
        [SerializeField]
        private List<MonsterController> trackedMonsters = new List<MonsterController>();

        [SerializeField]
        private List<NpcDeadStareController> trackedNpcs = new List<NpcDeadStareController>();

        [SerializeField]
        private List<AudioSource> managedAudioSources = new List<AudioSource>();

        [Tooltip("Automatically populates the collections above on Start().")]
        [SerializeField]
        private bool autoPopulateOnStart = true;

        [Header("Ranges")]
        [SerializeField]
        private float playerSpeedMin = 0f;

        [SerializeField]
        private float playerSpeedMax = 10f;

        [SerializeField]
        private float monsterSpeedMin = 0f;

        [SerializeField]
        private float monsterSpeedMax = 10f;

        [SerializeField]
        private float masterVolumeMin = 0f;

        [SerializeField]
        private float masterVolumeMax = 1f;

        [SerializeField]
        private float perspectiveFovMin = 40f;

        [SerializeField]
        private float perspectiveFovMax = 100f;

        [SerializeField]
        private float orthographicSizeMin = 1f;

        [SerializeField]
        private float orthographicSizeMax = 20f;

        [Header("Audio Replacement")]
        [Tooltip("Clip that will be assigned to all managed audio sources when the replace button is pressed.")]
        [SerializeField]
        private AudioClip replacementClip;

        private readonly Dictionary<AudioSource, float> audioBaseVolumes = new Dictionary<AudioSource, float>();
        private bool isVisible;
        private Rect windowRect = new Rect(16f, 16f, 420f, 540f);
        private Vector2 scrollPosition;
        private float cachedMasterVolume = 1f;
        private float cachedMonsterSpeed;
        private GUIStyle headerStyle;

        private void Awake()
        {
            isVisible = startVisible;

            if (playerState == null)
            {
                playerState = FindObjectOfType<PlayerStateController>();
            }

            if (eyeControl == null && playerState != null)
            {
                eyeControl = playerState.GetComponent<PlayerEyeControl>();
                if (eyeControl == null)
                {
                    eyeControl = playerState.GetComponentInChildren<PlayerEyeControl>();
                }
            }

            if (playerVision == null && playerState != null)
            {
                playerVision = playerState.GetComponent<PlayerVision>();
                if (playerVision == null)
                {
                    playerVision = playerState.GetComponentInChildren<PlayerVision>();
                }
            }

            if (playerCamera == null)
            {
                playerCamera = TryResolveCamera();
            }
        }

        private void Start()
        {
            if (autoPopulateOnStart)
            {
                RefreshTrackedObjects();
            }
            else
            {
                CacheAudioSources();
            }

            SyncCachedValues();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                isVisible = !isVisible;
            }

            RemoveDestroyedReferences();

            if (managedAudioSources.Count > audioBaseVolumes.Count)
            {
                CacheAudioSources();
            }
        }

        private void OnGUI()
        {
            if (!isVisible)
            {
                return;
            }

            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold
                };
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
            }

            if (eyeControl != null)
            {
                GUILayout.Label($"Eyes Open: {eyeControl.EyesOpen}");
                GUILayout.Label($"Forced Closed: {eyeControl.IsForcedClosing}");
                GUILayout.Label($"Wetness: {eyeControl.CurrentWetness:F2} / {eyeControl.MaximumWetness:F2}");
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
                }
            }
            else
            {
                float currentFov = playerCamera.fieldOfView;
                float newFov = DrawSlider("Field of View", currentFov, perspectiveFovMin, perspectiveFovMax);
                if (!Mathf.Approximately(newFov, currentFov))
                {
                    playerCamera.fieldOfView = newFov;
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
                    if (monster == null)
                    {
                        continue;
                    }

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
                    if (npc == null)
                    {
                        continue;
                    }

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

            if (playerState == null)
            {
                playerState = FindObjectOfType<PlayerStateController>();
            }

            if (eyeControl == null)
            {
                eyeControl = FindObjectOfType<PlayerEyeControl>();
            }

            if (playerVision == null)
            {
                playerVision = FindObjectOfType<PlayerVision>();
            }

            if (playerCamera == null)
            {
                playerCamera = TryResolveCamera();
            }

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
                    {
                        audioBaseVolumes.Remove(key);
                    }
                }
            }

            cachedMonsterSpeed = Mathf.Clamp(GetRepresentativeMonsterSpeed(), monsterSpeedMin, monsterSpeedMax);
        }

        private void AddUniqueRange<T>(List<T> target, T[] additions) where T : UnityEngine.Object
        {
            foreach (T element in additions)
            {
                if (element == null || target.Contains(element))
                {
                    continue;
                }

                target.Add(element);
            }
        }

        private void CacheAudioSources()
        {
            foreach (AudioSource source in managedAudioSources)
            {
                if (source == null)
                {
                    continue;
                }

                if (audioBaseVolumes.ContainsKey(source))
                {
                    continue;
                }

                float baseVolume = source.volume;
                if (cachedMasterVolume > 0.0001f)
                {
                    baseVolume = source.volume / cachedMasterVolume;
                }

                audioBaseVolumes[source] = Mathf.Max(0f, baseVolume);
            }

            ApplyMasterVolume();
        }

        private void ApplyMasterVolume()
        {
            foreach (KeyValuePair<AudioSource, float> entry in audioBaseVolumes)
            {
                AudioSource source = entry.Key;
                if (source == null)
                {
                    continue;
                }

                source.volume = entry.Value * cachedMasterVolume;
            }
        }

        private void ApplyReplacementClip()
        {
            if (replacementClip == null)
            {
                return;
            }

            foreach (AudioSource source in managedAudioSources)
            {
                if (source == null)
                {
                    continue;
                }

                source.clip = replacementClip;
            }
        }

        private int CountActiveAudioSources()
        {
            int count = 0;
            foreach (AudioSource source in managedAudioSources)
            {
                if (source != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void SyncCachedValues()
        {
            if (playerCamera == null)
            {
                playerCamera = TryResolveCamera();
            }

            cachedMasterVolume = Mathf.Clamp(cachedMasterVolume, masterVolumeMin, masterVolumeMax);
            cachedMonsterSpeed = GetRepresentativeMonsterSpeed();
            if (cachedMonsterSpeed <= 0f)
            {
                cachedMonsterSpeed = Mathf.Clamp(cachedMonsterSpeed, monsterSpeedMin, monsterSpeedMax);
            }
        }

        private Camera TryResolveCamera()
        {
            if (playerVision != null)
            {
                Camera candidate = playerVision.GetComponentInChildren<Camera>();
                if (candidate != null)
                {
                    return candidate;
                }
            }

            PlayerCameraBinder binder = FindObjectOfType<PlayerCameraBinder>();
            if (binder != null)
            {
                Camera candidate = binder.GetComponent<Camera>();
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return Camera.main;
        }

        private float GetRepresentativeMonsterSpeed()
        {
            foreach (MonsterController monster in trackedMonsters)
            {
                if (monster != null)
                {
                    return monster.CurrentMoveSpeed;
                }
            }

            return cachedMonsterSpeed > 0f ? cachedMonsterSpeed : monsterSpeedMin;
        }

        private void ApplyMonsterSpeed(float speed)
        {
            cachedMonsterSpeed = Mathf.Clamp(speed, monsterSpeedMin, monsterSpeedMax);

            foreach (MonsterController monster in trackedMonsters)
            {
                if (monster == null)
                {
                    continue;
                }

                monster.SetMoveSpeed(cachedMonsterSpeed);
            }
        }
    }
}
