using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LSP.Gameplay
{
    public class PlayerExecutionDeathSequence : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField]
        private PlayerStateController stateController;

        [Tooltip("Root object for the execution model.")]
        [SerializeField]
        private GameObject executionModelRoot;

        [SerializeField]
        private Animator executionAnimator;

        [Header("Animation")]
        [SerializeField]
        private string executionStateName = "Execution";

        [SerializeField]
        private float fallbackDuration = 3f;

        [Header("Scene Transition")]
        [SerializeField]
        private string sceneToLoad;

        [SerializeField]
        private bool reloadCurrentSceneWhenEmpty = true;

        [Tooltip("Delay before loading the target scene after death completes.")]
        [Min(0f)]
        [SerializeField]
        private float sceneLoadDelay = 0f;

        [Tooltip("Use unscaled time for the scene-load delay.")]
        [SerializeField]
        private bool useUnscaledDelay = false;

        // 【新增 1】一个明确的开关，用来禁止自动跳转
        [Header("Control")]
        [Tooltip("如果勾选，动画播完后【绝对不会】自动切场景。适用于需要弹UI的情况。")]
        public bool disableAutomaticLoad = false; 

        private Coroutine deathRoutine;
        private Coroutine delayedLoadRoutine;
        private bool deathCompleted;
        private string sceneToLoadOverride;
        private float? sceneLoadDelayOverride;
        public event Action DeathSequenceCompleted;

        private void Reset()
        {
            if (stateController == null) stateController = GetComponent<PlayerStateController>();
            if (executionModelRoot == null) executionModelRoot = transform.Find("ExecutionModel")?.gameObject;
        }

        private void Awake()
        {
            if (stateController == null) stateController = GetComponent<PlayerStateController>();
            if (executionModelRoot != null && executionAnimator == null) executionAnimator = executionModelRoot.GetComponentInChildren<Animator>();
            HideExecutionModel();
        }

        private void OnEnable()
        {
            if (stateController != null) stateController.PlayerKilled += HandlePlayerKilled;
        }

        private void OnDisable()
        {
            if (stateController != null) stateController.PlayerKilled -= HandlePlayerKilled;
            if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
            if (delayedLoadRoutine != null) { StopCoroutine(delayedLoadRoutine); delayedLoadRoutine = null; }
            deathCompleted = false;
            sceneToLoadOverride = null;
            sceneLoadDelayOverride = null;
            HideExecutionModel();
        }

        public void NotifyExecutionAnimationFinished()
        {
            if (!deathCompleted) CompleteDeath();
        }

        public void OverrideSceneToLoad(string sceneName)
        {
            sceneToLoadOverride = sceneName;
        }

        public void OverrideSceneLoadDelay(float delaySeconds)
        {
            sceneLoadDelayOverride = Mathf.Max(0f, delaySeconds);
        }

        // 【新增 2】提供一个方法给导演脚本调用，专门用来“踩刹车”
        public void SuppressAutomaticSceneLoad()
        {
            disableAutomaticLoad = true;
        }

        private void HandlePlayerKilled()
        {
            if (deathRoutine != null || deathCompleted) return;
            deathRoutine = StartCoroutine(DeathSequenceRoutine());
        }

        private IEnumerator DeathSequenceRoutine()
        {
            ShowExecutionModel();

            if (executionAnimator == null && executionModelRoot != null)
                executionAnimator = executionModelRoot.GetComponentInChildren<Animator>();

            float waitTime = Mathf.Max(0f, fallbackDuration);

            if (executionAnimator != null)
            {
                if (!string.IsNullOrEmpty(executionStateName)) executionAnimator.Play(executionStateName, 0, 0f);
                yield return null;
                AnimatorStateInfo stateInfo = executionAnimator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.length > 0f)
                {
                    float speed = Mathf.Approximately(stateInfo.speed, 0f) ? 1f : stateInfo.speed;
                    waitTime = Mathf.Max(0f, stateInfo.length / Mathf.Abs(speed));
                }
            }
            else
            {
                yield return null;
            }

            float elapsed = 0f;
            while (!deathCompleted && elapsed < waitTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!deathCompleted) CompleteDeath();
            deathRoutine = null;
        }

        private void CompleteDeath()
        {
            if (deathCompleted) return;
            deathCompleted = true;

            if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
            DeathSequenceCompleted?.Invoke();

            // 【核心修改】如果在导演里被禁止了，或者Inspector里勾选了禁止，直接在这里停住！
            // 这样场景就不会重载，UI 就能稳稳地停留在那里。
            if (disableAutomaticLoad) 
            {
                Debug.Log("PlayerExecutionDeathSequence: 自动跳转已禁用，等待玩家操作 UI。");
                return;
            }

            // 下面是原本的跳转逻辑（只有没被禁用时才执行）
            string targetScene = !string.IsNullOrWhiteSpace(sceneToLoadOverride) ? sceneToLoadOverride : sceneToLoad;
            
            if (string.IsNullOrWhiteSpace(targetScene) && reloadCurrentSceneWhenEmpty)
            {
                Scene currentScene = SceneManager.GetActiveScene();
                if (currentScene.IsValid()) targetScene = currentScene.name;
            }

            if (!string.IsNullOrWhiteSpace(targetScene))
            {
                float delay = sceneLoadDelayOverride.HasValue
                    ? sceneLoadDelayOverride.Value
                    : Mathf.Max(0f, sceneLoadDelay);

                if (delay <= 0f)
                {
                    SceneManager.LoadScene(targetScene);
                }
                else
                {
                    if (delayedLoadRoutine != null)
                    {
                        StopCoroutine(delayedLoadRoutine);
                    }

                    delayedLoadRoutine = StartCoroutine(LoadSceneAfterDelay(targetScene, delay));
                }
            }
        }

        private IEnumerator LoadSceneAfterDelay(string targetScene, float delay)
        {
            if (useUnscaledDelay)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            else
            {
                yield return new WaitForSeconds(delay);
            }

            delayedLoadRoutine = null;
            SceneManager.LoadScene(targetScene);
        }

        private void ShowExecutionModel()
        {
            if (executionModelRoot != null) executionModelRoot.SetActive(true);
        }

        private void HideExecutionModel()
        {
            if (executionModelRoot != null) executionModelRoot.SetActive(false);
        }
    }
}
