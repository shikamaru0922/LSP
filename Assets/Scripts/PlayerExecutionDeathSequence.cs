using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LSP.Gameplay
{
    /// <summary>
    /// Handles the execution animation that plays when the player dies. The referenced
    /// execution model stays hidden until the <see cref="PlayerStateController"/> signals
    /// a death event. Once triggered, the model is enabled, the animation is played and
    /// the current scene (or an optional override) reloads only after the animation is
    /// finished.
    /// </summary>
    public class PlayerExecutionDeathSequence : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField]
        private PlayerStateController stateController;

        [Tooltip("Root object for the execution model. It will be activated when the player dies.")]
        [SerializeField]
        private GameObject executionModelRoot;

        [Tooltip("Animator responsible for playing the execution animation. If left empty the first animator under the execution model will be used.")]
        [SerializeField]
        private Animator executionAnimator;

        [Header("Animation")]
        [Tooltip("Name of the animator state that contains the execution animation.")]
        [SerializeField]
        private string executionStateName = "Execution";

        [Tooltip("Fallback duration used if the animation length cannot be determined.")]
        [SerializeField]
        private float fallbackDuration = 3f;

        [Header("Scene Transition")]
        [Tooltip("Optional scene name to load once the execution animation finishes.")]
        [SerializeField]
        private string sceneToLoad;

        [Tooltip("Reload the active scene when no explicit scene name is provided.")]
        [SerializeField]
        private bool reloadCurrentSceneWhenEmpty = true;

        private Coroutine deathRoutine;
        private bool deathCompleted;
        private string sceneToLoadOverride;
        private GameObject registeredExecutingMonster;

        private void Reset()
        {
            if (stateController == null)
            {
                stateController = GetComponent<PlayerStateController>();
            }

            if (executionModelRoot == null)
            {
                executionModelRoot = transform.Find("ExecutionModel")?.gameObject;
            }
        }

        private void Awake()
        {
            if (stateController == null)
            {
                stateController = GetComponent<PlayerStateController>();
            }

            if (executionModelRoot != null && executionAnimator == null)
            {
                executionAnimator = executionModelRoot.GetComponentInChildren<Animator>();
            }

            HideExecutionModel();
        }

        private void OnEnable()
        {
            if (stateController != null)
            {
                stateController.PlayerKilled += HandlePlayerKilled;
            }
        }

        private void OnDisable()
        {
            if (stateController != null)
            {
                stateController.PlayerKilled -= HandlePlayerKilled;
            }

            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
                deathRoutine = null;
            }

            deathCompleted = false;
            sceneToLoadOverride = null;
            registeredExecutingMonster = null;
            HideExecutionModel();
        }

        /// <summary>
        /// Can be called from an animation event when the execution animation finishes.
        /// </summary>
        public void NotifyExecutionAnimationFinished()
        {
            if (!deathCompleted)
            {
                CompleteDeath();
            }
        }

        /// <summary>
        /// Overrides the scene that will be loaded when the death sequence completes.
        /// Passing <c>null</c> or whitespace clears the override.
        /// </summary>
        public void OverrideSceneToLoad(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                sceneToLoadOverride = null;
                return;
            }

            sceneToLoadOverride = sceneName;
        }

        /// <summary>
        /// Registers the monster responsible for the execution so it can be hidden while the
        /// dedicated execution animation plays.
        /// </summary>
        /// <param name="monster">Monster GameObject that triggered the execution.</param>
        public void RegisterExecutingMonster(GameObject monster)
        {
            registeredExecutingMonster = monster;
        }

        private void HandlePlayerKilled()
        {
            if (deathRoutine != null || deathCompleted)
            {
                return;
            }

            deathRoutine = StartCoroutine(DeathSequenceRoutine());
        }

        private IEnumerator DeathSequenceRoutine()
        {
            DeactivateRegisteredMonster();
            ShowExecutionModel();

            if (executionAnimator == null && executionModelRoot != null)
            {
                executionAnimator = executionModelRoot.GetComponentInChildren<Animator>();
            }

            float waitTime = Mathf.Max(0f, fallbackDuration);

            if (executionAnimator != null)
            {
                if (!string.IsNullOrEmpty(executionStateName))
                {
                    executionAnimator.Play(executionStateName, 0, 0f);
                }

                // Wait a frame so the animator can update its current state info.
                yield return null;

                AnimatorStateInfo stateInfo = executionAnimator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.length > 0f)
                {
                    // Adjust with speed to obtain the actual playback duration.
                    float speed = Mathf.Approximately(stateInfo.speed, 0f) ? 1f : stateInfo.speed;
                    waitTime = Mathf.Max(0f, stateInfo.length / Mathf.Abs(speed));
                }
            }
            else
            {
                // Ensure we yield at least one frame if no animator is available so the
                // death sequence still pauses momentarily before completing.
                yield return null;
            }

            float elapsed = 0f;
            while (!deathCompleted && elapsed < waitTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!deathCompleted)
            {
                CompleteDeath();
            }

            deathRoutine = null;
        }

        private void CompleteDeath()
        {
            if (deathCompleted)
            {
                return;
            }

            deathCompleted = true;

            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
                deathRoutine = null;
            }

            string targetScene = !string.IsNullOrWhiteSpace(sceneToLoadOverride)
                ? sceneToLoadOverride
                : sceneToLoad;
            if (string.IsNullOrWhiteSpace(targetScene) && reloadCurrentSceneWhenEmpty)
            {
                Scene currentScene = SceneManager.GetActiveScene();
                if (currentScene.IsValid())
                {
                    targetScene = currentScene.name;
                }
            }

            if (!string.IsNullOrWhiteSpace(targetScene))
            {
                sceneToLoadOverride = null;
                SceneManager.LoadScene(targetScene);
            }
        }

        private void DeactivateRegisteredMonster()
        {
            if (registeredExecutingMonster == null)
            {
                return;
            }

            registeredExecutingMonster.SetActive(false);
            registeredExecutingMonster = null;
        }

        private void ShowExecutionModel()
        {
            if (executionModelRoot != null)
            {
                executionModelRoot.SetActive(true);
            }
        }

        private void HideExecutionModel()
        {
            if (executionModelRoot != null)
            {
                executionModelRoot.SetActive(false);
            }
        }
    }
}
