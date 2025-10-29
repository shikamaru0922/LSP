using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Coordinates monster detection howls so only one plays at a time.
    /// Attach to a scene object to automatically discover <see cref="MonsterController"/> instances
    /// and regulate their howling audio.
    /// </summary>
    public class MonsterHowlCoordinator : MonoBehaviour
    {
        public static MonsterHowlCoordinator Instance { get; private set; }

        [Header("Audio")]
        [Range(0f, 1f)]
        [SerializeField]
        private float detectionHowlVolume = 1f;

        private readonly HashSet<MonsterController> registeredMonsters = new HashSet<MonsterController>();
        private MonsterController activeMonster;
        private Coroutine releaseRoutine;

        public float DetectionHowlVolume => Mathf.Clamp01(detectionHowlVolume);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(MonsterHowlCoordinator)} instances detected. The extra instance will be disabled.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            // Automatically discover monsters already present in the scene.
            foreach (MonsterController monster in FindObjectsOfType<MonsterController>(true))
            {
                RegisterMonster(monster);
            }

            ApplyVolumeToRegisteredMonsters();
        }

        private void OnValidate()
        {
            detectionHowlVolume = Mathf.Clamp01(detectionHowlVolume);
            ApplyVolumeToRegisteredMonsters();
        }

        private void OnDisable()
        {
            ReleaseActiveMonster();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Registers a monster so that the coordinator can keep track of it.
        /// </summary>
        public void RegisterMonster(MonsterController monster)
        {
            if (monster == null)
            {
                return;
            }

            registeredMonsters.Add(monster);
            monster.ApplyDetectionHowlVolume(DetectionHowlVolume);
        }

        /// <summary>
        /// Sets the master volume applied to all monster detection howls.
        /// </summary>
        public void SetDetectionHowlVolume(float volume)
        {
            detectionHowlVolume = Mathf.Clamp01(volume);
            ApplyVolumeToRegisteredMonsters();
        }

        /// <summary>
        /// Removes a monster from the coordinator, releasing howl control if needed.
        /// </summary>
        public void UnregisterMonster(MonsterController monster)
        {
            if (monster == null)
            {
                return;
            }

            registeredMonsters.Remove(monster);

            if (activeMonster == monster)
            {
                ReleaseActiveMonster();
            }
        }

        /// <summary>
        /// Attempts to start a howl for the specified monster.
        /// </summary>
        /// <param name="monster">The requesting monster.</param>
        /// <param name="durationSeconds">The expected duration of the howl in seconds.</param>
        /// <returns>True if the howl is allowed to play, otherwise false.</returns>
        public bool TryBeginHowl(MonsterController monster, float durationSeconds)
        {
            if (monster == null)
            {
                return false;
            }

            if (activeMonster != null && activeMonster != monster)
            {
                return false;
            }

            activeMonster = monster;

            if (durationSeconds <= 0f)
            {
                ReleaseActiveMonster();
                return true;
            }

            if (releaseRoutine != null)
            {
                StopCoroutine(releaseRoutine);
            }

            releaseRoutine = StartCoroutine(ReleaseAfterDelay(durationSeconds));
            return true;
        }

        /// <summary>
        /// Ends the howl for the provided monster, freeing control for others.
        /// </summary>
        public void EndHowl(MonsterController monster)
        {
            if (monster == null || activeMonster != monster)
            {
                return;
            }

            ReleaseActiveMonster();
        }

        private void ApplyVolumeToRegisteredMonsters()
        {
            float volume = DetectionHowlVolume;

            foreach (MonsterController monster in registeredMonsters)
            {
                monster?.ApplyDetectionHowlVolume(volume);
            }

            registeredMonsters.RemoveWhere(monster => monster == null);
        }

        private IEnumerator ReleaseAfterDelay(float duration)
        {
            yield return new WaitForSeconds(duration);
            ReleaseActiveMonster();
        }

        private void ReleaseActiveMonster()
        {
            if (releaseRoutine != null)
            {
                StopCoroutine(releaseRoutine);
                releaseRoutine = null;
            }

            activeMonster = null;
        }
    }
}
