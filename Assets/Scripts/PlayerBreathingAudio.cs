using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Controls the player's breathing audio feedback based on the proximity of nearby monsters.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerBreathingAudio : MonoBehaviour
    {
        private const float TenseStageIntensity = 0.5f;

        [Header("Audio")]
        [Tooltip("Audio source that plays the breathing loop.")]
        [SerializeField]
        private AudioSource audioSource;

        [Tooltip("Curve mapping threat intensity (0-1) to the final audio volume.")]
        [SerializeField]
        private AnimationCurve volumeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Seconds to fully fade in the breathing audio once active again.")]
        [SerializeField]
        private float fadeInDuration = 0.75f;

        [Tooltip("Seconds to fade out the breathing audio when silenced.")]
        [SerializeField]
        private float fadeOutDuration = 0.5f;

        [Header("Distance Thresholds (metres)")]
        [Tooltip("Distance at which the breathing starts reacting to the monster.")]
        [Min(0f)]
        [SerializeField]
        private float alertDistance = 18f;

        [Tooltip("Distance where the breathing reaches the tense stage.")]
        [Min(0f)]
        [SerializeField]
        private float tenseDistance = 10f;

        [Tooltip("Distance where the breathing reaches maximum panic.")]
        [Min(0f)]
        [SerializeField]
        private float panicDistance = 4f;

        [Header("Dependencies")]
        [SerializeField]
        private PlayerStateController stateController;

        [SerializeField]
        private PlayerEyeControl eyeControl;

        private readonly List<MonsterController> monsterCache = new List<MonsterController>();
        private bool isPlayerAlive = true;
        private bool isForcedClosed;
        private bool isWorldAbnormal = true;
        private float targetFadeWeight = 1f;
        private float currentFadeWeight = 1f;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponentInChildren<AudioSource>();
            }

            if (stateController == null)
            {
                stateController = GetComponent<PlayerStateController>();
            }

            if (eyeControl == null)
            {
                eyeControl = GetComponent<PlayerEyeControl>();
            }
        }

        private void OnEnable()
        {
            RefreshMonsterCache();

            if (stateController != null)
            {
                isPlayerAlive = stateController.IsAlive;
                stateController.PlayerKilled += HandlePlayerKilled;
            }

            if (eyeControl != null)
            {
                isForcedClosed = eyeControl.IsForcedClosing;
                eyeControl.EyesForcedClosed += HandleEyesForcedClosed;
                eyeControl.EyesForcedOpened += HandleEyesForcedOpened;
            }

            GameManager.WorldAbnormalStateChanged += HandleWorldStateChanged;
            MonsterController.MonsterReset += HandleMonsterReset;

            isWorldAbnormal = GameManager.Instance == null || GameManager.Instance.IsWorldAbnormal;

            currentFadeWeight = ShouldAllowBreathingAudio ? 1f : 0f;
            targetFadeWeight = currentFadeWeight;

            ApplyVolumeImmediately();
        }

        private void OnDisable()
        {
            GameManager.WorldAbnormalStateChanged -= HandleWorldStateChanged;
            MonsterController.MonsterReset -= HandleMonsterReset;

            if (stateController != null)
            {
                stateController.PlayerKilled -= HandlePlayerKilled;
            }

            if (eyeControl != null)
            {
                eyeControl.EyesForcedClosed -= HandleEyesForcedClosed;
                eyeControl.EyesForcedOpened -= HandleEyesForcedOpened;
            }
        }

        private void Update()
        {
            UpdateFadeWeight(Time.deltaTime);
            UpdateVolume();
        }

        /// <summary>
        /// Refreshes the cached monster list. Call this if monsters are spawned dynamically.
        /// </summary>
        public void RefreshMonsterCache()
        {
            monsterCache.Clear();
            MonsterController[] monsters = FindObjectsOfType<MonsterController>(true);
            if (monsters != null)
            {
                monsterCache.AddRange(monsters);
            }
        }

        private void UpdateFadeWeight(float deltaTime)
        {
            if (Mathf.Approximately(currentFadeWeight, targetFadeWeight))
            {
                currentFadeWeight = targetFadeWeight;
                return;
            }

            float duration = targetFadeWeight > currentFadeWeight ? fadeInDuration : fadeOutDuration;
            if (duration <= 0f)
            {
                currentFadeWeight = targetFadeWeight;
                return;
            }

            float step = deltaTime / Mathf.Max(duration, Mathf.Epsilon);
            currentFadeWeight = Mathf.MoveTowards(currentFadeWeight, targetFadeWeight, step);
        }

        private void UpdateVolume()
        {
            if (audioSource == null)
            {
                return;
            }

            float intensity = ComputeThreatIntensity();
            float baseVolume = volumeCurve != null ? volumeCurve.Evaluate(intensity) : intensity;
            float finalVolume = baseVolume * currentFadeWeight;

            ApplyVolume(finalVolume);
        }

        private void ApplyVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            audioSource.volume = volume;

            if (volume > 0f)
            {
                if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            }
            else if (audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }

        private void ApplyVolumeImmediately()
        {
            if (audioSource == null)
            {
                return;
            }

            float intensity = ComputeThreatIntensity();
            float baseVolume = volumeCurve != null ? volumeCurve.Evaluate(intensity) : intensity;
            ApplyVolume(baseVolume * currentFadeWeight);
        }

        private float ComputeThreatIntensity()
        {
            if (!ShouldAllowBreathingAudio || !isWorldAbnormal)
            {
                return 0f;
            }

            float closestDistance = float.PositiveInfinity;
            Vector3 playerPosition = transform.position;

            for (int i = monsterCache.Count - 1; i >= 0; i--)
            {
                var monster = monsterCache[i];
                if (monster == null)
                {
                    monsterCache.RemoveAt(i);
                    continue;
                }

                if (!monster.isActiveAndEnabled)
                {
                    continue;
                }

                float distance = Vector3.Distance(playerPosition, monster.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                }
            }

            if (!float.IsFinite(closestDistance))
            {
                return 0f;
            }

            if (closestDistance >= alertDistance)
            {
                return 0f;
            }

            if (closestDistance <= panicDistance)
            {
                return 1f;
            }

            if (closestDistance <= tenseDistance)
            {
                if (Mathf.Approximately(tenseDistance, panicDistance))
                {
                    return TenseStageIntensity;
                }

                float t = Mathf.InverseLerp(panicDistance, tenseDistance, closestDistance);
                return Mathf.Lerp(1f, TenseStageIntensity, t);
            }

            if (Mathf.Approximately(alertDistance, tenseDistance))
            {
                return TenseStageIntensity;
            }

            float tAlert = Mathf.InverseLerp(alertDistance, tenseDistance, closestDistance);
            return Mathf.Lerp(0f, TenseStageIntensity, tAlert);
        }

        private bool ShouldAllowBreathingAudio => isPlayerAlive && !isForcedClosed;

        private void HandlePlayerKilled()
        {
            isPlayerAlive = false;
            SetTargetFadeWeight(0f);
        }

        private void HandleEyesForcedClosed()
        {
            isForcedClosed = true;
            SetTargetFadeWeight(0f);
        }

        private void HandleEyesForcedOpened()
        {
            isForcedClosed = false;
            if (isPlayerAlive)
            {
                SetTargetFadeWeight(1f);
                ApplyVolumeImmediately();
            }
        }

        private void HandleWorldStateChanged(bool abnormal)
        {
            isWorldAbnormal = abnormal;
            ApplyVolumeImmediately();
        }

        private void HandleMonsterReset(MonsterController monster)
        {
            if (monster != null && !monsterCache.Contains(monster))
            {
                monsterCache.Add(monster);
            }

            ApplyVolumeImmediately();
        }

        private void SetTargetFadeWeight(float weight)
        {
            targetFadeWeight = Mathf.Clamp01(weight);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            fadeInDuration = Mathf.Max(0f, fadeInDuration);
            fadeOutDuration = Mathf.Max(0f, fadeOutDuration);

            alertDistance = Mathf.Max(0f, alertDistance);
            tenseDistance = Mathf.Clamp(tenseDistance, 0f, alertDistance);
            panicDistance = Mathf.Clamp(panicDistance, 0f, tenseDistance);
        }
#endif
    }
}
