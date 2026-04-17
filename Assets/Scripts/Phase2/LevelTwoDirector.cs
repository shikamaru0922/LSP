using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;
using StarterAssets;
using LSP.Gameplay;

[System.Serializable]
public class GameFlag
{
    public string flagName;       // 布尔值的名字 (比如 "HasArm")
    public bool currentValue;     // 当前的值 (True/False)
    
    [Header("当变成 True 时触发")]
    public UnityEvent onTrue;     // 比如: 播放获得音效

    [Header("当变成 False 时触发")]
    public UnityEvent onFalse;    // (一般用得少，备用)
}

public class LevelTwoDirector : MonoBehaviour
{
    private const string LegacyPhase3SceneName = "Level_Phase3";
    private const string RemakePhase3SceneName = "Remake_Phase3";
    private static readonly char[] LoadingSpinnerFrames = { '|', '/', '-', '\\' };
    public static LevelTwoDirector Instance;

    private static class Phase2DeathResumeCache
    {
        public static bool hasPendingRestore;
        public static string sceneName;
        public static Vector3 respawnPosition;
        public static Quaternion respawnRotation;
        public static bool hasRespawnTransform;
        public static readonly List<string> trueFlagNames = new List<string>();

        public static void Clear()
        {
            hasPendingRestore = false;
            sceneName = null;
            hasRespawnTransform = false;
            respawnPosition = Vector3.zero;
            respawnRotation = Quaternion.identity;
            trueFlagNames.Clear();
        }
    }

    [Header("===== 所有的游戏开关 (Flags) =====")]
    [Tooltip("在这里添加你所有的布尔值，比如 HasArm, HasKeyA...")]
    public List<GameFlag> flags = new List<GameFlag>();

    [Header("===== Phase2 -> Phase3 过场 =====")]
    [Tooltip("启用后：ElevatorBoxOpen=true 时预加载场景，LevelComplete=true 后延迟关门并切场景。")]
    [SerializeField] private bool enablePhase3TransitionFlow = true;

    [Tooltip("是否允许在 preloadTriggerFlagName 触发时提前预加载。关闭后只在真正切场景时开始加载。")]
    [SerializeField] private bool enablePreloadOnTriggerFlag = false;

    [Tooltip("触发异步预加载的 Flag 名字。")]
    [SerializeField] private string preloadTriggerFlagName = "ElevatorBoxOpen";

    [Tooltip("触发关门和切场景流程的 Flag 名字。")]
    [SerializeField] private string levelCompleteFlagName = "LevelComplete";

    [Tooltip("要异步预加载并切换的目标场景名。")]
    [SerializeField] private string phase3SceneName = RemakePhase3SceneName;

    [Tooltip("LevelComplete 触发后，延迟多少秒再执行关门。")]
    [Min(0f)]
    [SerializeField] private float delayBeforeCloseDoor = 2f;

    [Tooltip("电梯门对象（建议拖 Elevetor-3-Open 上的 ElevatorDoors 组件）。")]
    [SerializeField] private ElevatorDoors elevatorDoors;

    [Tooltip("当未手动拖门组件时，用这个名字自动查找。")]
    [SerializeField] private string elevatorDoorObjectName = "Elevetor-3-Open";

    [Tooltip("切场景前是否强制玩家眨眼一次作为转场。")]
    [SerializeField] private bool forceBlinkBeforeTransition = true;

    [Tooltip("触发眨眼后，等待多久再切场景。")]
    [Min(0f)]
    [SerializeField] private float delayAfterBlink = 0.15f;

    [Tooltip("可选：手动指定玩家眼睛控制脚本，不填则自动查找。")]
    [SerializeField] private PlayerEyeControl playerEyeControl;

    [Tooltip("使用非缩放时间（即使 Time.timeScale=0 也继续计时）。")]
    [SerializeField] private bool useUnscaledTime = false;

    [Tooltip("等待门关闭事件的额外超时时间（秒）。")]
    [Min(0f)]
    [SerializeField] private float doorCloseTimeoutPadding = 0.35f;

    [Header("===== 跳转加载 UI =====")]
    [Tooltip("为 true 时：真正开始切场景时显示黑底加载 UI（仅切场景期间出现）。")]
    [SerializeField] private bool showLoadingUiDuringTransition = true;

    [Tooltip("加载 UI 的主标题文本。")]
    [SerializeField] private string transitionLoadingTitle = "Transitioning to the next scene...";

    [Tooltip("加载 UI 的副标题前缀。")]
    [SerializeField] private string transitionLoadingDetailPrefix = "Loading";

    [Tooltip("黑底不透明度。1 = 全黑。")]
    [Range(0f, 1f)]
    [SerializeField] private float transitionLoadingOverlayAlpha = 1f;

    [Tooltip("加载期间是否阻挡 UI 射线点击。")]
    [SerializeField] private bool blockUiInputWhileLoading = true;

    [Header("===== Phase2 死亡重置 =====")]
    [Tooltip("启用后：玩家被怪物跳杀时，不切到 Restart 场景；改为本场景黑屏+重置按钮。")]
    [SerializeField] private bool enableInSceneDeathRestart = true;

    [Tooltip("玩家死亡后，黑屏持续时长（秒）。")]
    [Min(0f)]
    [SerializeField] private float deathBlackScreenDuration = 2f;

    [Tooltip("死亡黑屏的不透明度。")]
    [Range(0f, 1f)]
    [SerializeField] private float deathOverlayAlpha = 1f;

    [Tooltip("死亡流程是否使用非缩放时间。")]
    [SerializeField] private bool useUnscaledTimeForDeathFlow = true;

    [Tooltip("死亡后重置点是否使用最近怪物位置（否则使用玩家死亡点）。")]
    [SerializeField] private bool respawnAtNearestMonsterPosition = true;

    [Tooltip("优先使用该场景点位作为复活点（例如 RestPosition）。")]
    [SerializeField] private Transform respawnPointTransform;

    [Tooltip("当未手动指定复活点时，按此名字自动查找场景物体。")]
    [SerializeField] private string respawnPointObjectName = "RestPosition";

    [Tooltip("重置点的额外 Y 偏移。")]
    [SerializeField] private float respawnHeightOffset = 0f;

    [Tooltip("复活时是否一次性朝向指定目标点（不是持续追踪）。")]
    [SerializeField] private bool faceRespawnLookAtTargetOnRevive = true;

    [Tooltip("复活时玩家朝向的目标点（例如动画放置点）。")]
    [SerializeField] private Transform respawnLookAtTarget;

    [Tooltip("当未手动指定朝向目标时，按此名字自动查找场景物体。")]
    [SerializeField] private string respawnLookAtObjectName = "ExecutionPoint";

    [Tooltip("死亡 UI 主标题。")]
    [SerializeField] private string deathUiTitle = "YOU DIED";

    [Tooltip("死亡 UI 按钮文本。")]
    [SerializeField] private string deathUiButtonText = "Reset";

    // 字典：用来快速查找，代码里查起来快
    private Dictionary<string, GameFlag> flagMap = new Dictionary<string, GameFlag>();
    private AsyncOperation preloadOperation;
    private bool preloadStarted;
    private bool levelCompleteSequenceStarted;
    private bool transitionTriggered;
    private Canvas transitionLoadingCanvas;
    private CanvasGroup transitionLoadingCanvasGroup;
    private Text transitionLoadingTitleText;
    private Text transitionLoadingDetailText;
    private float transitionLoadingAnimTimer;
    private bool suppressTransitionFlagHandling;
    private PlayerStateController boundPlayerState;
    private PlayerExecutionDeathSequence boundDeathSequence;
    private bool deathFlowStarted;
    private bool waitingForDeathSequenceCompletion;
    private Coroutine deathFlowRoutine;
    private Canvas deathRestartCanvas;
    private CanvasGroup deathRestartCanvasGroup;
    private Text deathRestartTitleText;
    private Button deathRestartButton;
    private Text deathRestartButtonText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 初始化字典
        foreach (var flag in flags)
        {
            if (!flagMap.ContainsKey(flag.flagName))
            {
                flagMap.Add(flag.flagName, flag);
            }
        }

        NormalizePhase3SceneName();
        ResolveTransitionReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnbindPlayerDeathEvents();
        DestroyTransitionLoadingOverlay();
        DestroyDeathRestartOverlay();
    }

    private void Start()
    {
        ResolveTransitionReferences();
        BindPlayerDeathEvents();
        TryApplyPendingDeathRestore();

        if (!enablePhase3TransitionFlow)
        {
            return;
        }

        if (enablePreloadOnTriggerFlag &&
            TryGetFlagValue(preloadTriggerFlagName, out bool preloadTrigger) &&
            preloadTrigger)
        {
            StartPhase3Preload();
        }

        if (TryGetFlagValue(levelCompleteFlagName, out bool levelComplete) && levelComplete)
        {
            StartLevelCompleteSequence();
        }
    }

    // =========================================================
    //  【核心功能 1】设置布尔值 (SetBool)
    //  比如: SetFlag("HasArm", true);
    // =========================================================
    public void SetFlag(string name, bool value)
    {
        if (flagMap.TryGetValue(name, out GameFlag flag))
        {
            // 如果值没变，就不重复触发 (防止每一帧都触发)
            if (flag.currentValue == value) return;

            flag.currentValue = value;
            Debug.Log($"<color=cyan>【导演】开关更新: {name} = {value}</color>");

            // 触发对应的事件
            if (value == true) flag.onTrue?.Invoke();
            else flag.onFalse?.Invoke();

            if (value)
            {
                if (!suppressTransitionFlagHandling)
                {
                    HandleTransitionFlagRaised(name);
                }
            }
        }
        else
        {
            Debug.LogError($"【错误】找不到名为 '{name}' 的开关！请在 Inspector 里添加。");
        }
    }

    // 为了让 UnityEvent (比如按钮) 能调用，提供一个只设为 True 的简便方法
    public void SetFlagTrue(string name)
    {
        SetFlag(name, true);
    }

    // =========================================================
    //  【核心功能 2】检查布尔值 (GetBool)
    //  比如: if (GetFlag("HasArm")) ...
    // =========================================================
    public bool GetFlag(string name)
    {
        if (flagMap.TryGetValue(name, out GameFlag flag))
        {
            return flag.currentValue;
        }
        
        // 如果找不到这个开关，默认返回 false，并报错提醒你
        Debug.LogWarning($"【警告】试图检查不存在的开关: {name}");
        return false;
    }

    private bool TryGetFlagValue(string name, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (flagMap.TryGetValue(name, out GameFlag flag))
        {
            value = flag.currentValue;
            return true;
        }

        return false;
    }

    private void HandleTransitionFlagRaised(string flagName)
    {
        if (!enablePhase3TransitionFlow)
        {
            return;
        }

        if (enablePreloadOnTriggerFlag &&
            !string.IsNullOrWhiteSpace(preloadTriggerFlagName) &&
            flagName == preloadTriggerFlagName)
        {
            StartPhase3Preload();
        }

        if (!string.IsNullOrWhiteSpace(levelCompleteFlagName) && flagName == levelCompleteFlagName)
        {
            StartLevelCompleteSequence();
        }
    }

    private void ResolveTransitionReferences()
    {
        if (elevatorDoors == null)
        {
            if (!string.IsNullOrWhiteSpace(elevatorDoorObjectName))
            {
                GameObject doorObject = GameObject.Find(elevatorDoorObjectName);
                if (doorObject != null)
                {
                    elevatorDoors = doorObject.GetComponent<ElevatorDoors>();
                }
            }

            if (elevatorDoors == null)
            {
                ElevatorDoors[] allDoors = FindObjectsOfType<ElevatorDoors>(true);
                if (allDoors != null && allDoors.Length > 0)
                {
                    if (!string.IsNullOrWhiteSpace(elevatorDoorObjectName))
                    {
                        foreach (ElevatorDoors door in allDoors)
                        {
                            if (door != null && door.gameObject.name == elevatorDoorObjectName)
                            {
                                elevatorDoors = door;
                                break;
                            }
                        }
                    }

                    if (elevatorDoors == null)
                    {
                        elevatorDoors = allDoors[0];
                    }
                }
            }
        }

        if (playerEyeControl == null)
        {
            playerEyeControl = FindObjectOfType<PlayerEyeControl>(true);
        }

        TryResolveRespawnPointTransform();
        TryResolveRespawnLookAtTarget();
    }

    private void BindPlayerDeathEvents()
    {
        if (!enableInSceneDeathRestart)
        {
            return;
        }

        PlayerStateController playerState = FindObjectOfType<PlayerStateController>(true);
        if (boundPlayerState != null)
        {
            boundPlayerState.PlayerKilled -= HandlePlayerKilledInPhase2;
        }
        if (boundDeathSequence != null)
        {
            boundDeathSequence.DeathSequenceCompleted -= HandleDeathSequenceCompletedInPhase2;
        }

        boundPlayerState = playerState;
        boundDeathSequence = null;

        if (boundPlayerState == null)
        {
            return;
        }

        boundPlayerState.PlayerKilled += HandlePlayerKilledInPhase2;
        boundDeathSequence = boundPlayerState.GetComponent<PlayerExecutionDeathSequence>()
            ?? boundPlayerState.GetComponentInChildren<PlayerExecutionDeathSequence>(true);
        if (boundDeathSequence != null)
        {
            boundDeathSequence.DeathSequenceCompleted += HandleDeathSequenceCompletedInPhase2;
        }
    }

    private void UnbindPlayerDeathEvents()
    {
        if (boundPlayerState != null)
        {
            boundPlayerState.PlayerKilled -= HandlePlayerKilledInPhase2;
        }
        if (boundDeathSequence != null)
        {
            boundDeathSequence.DeathSequenceCompleted -= HandleDeathSequenceCompletedInPhase2;
        }

        boundPlayerState = null;
        boundDeathSequence = null;
    }

    private void HandlePlayerKilledInPhase2()
    {
        if (!enableInSceneDeathRestart || deathFlowStarted || waitingForDeathSequenceCompletion)
        {
            return;
        }

        BindPlayerDeathEvents();

        if (boundDeathSequence != null)
        {
            boundDeathSequence.SuppressAutomaticSceneLoad();
        }

        CaptureDeathRestoreSnapshot();
        waitingForDeathSequenceCompletion = boundDeathSequence != null;

        if (!waitingForDeathSequenceCompletion)
        {
            BeginDeathRestartUiFlow();
        }
    }

    private void HandleDeathSequenceCompletedInPhase2()
    {
        if (!enableInSceneDeathRestart || !waitingForDeathSequenceCompletion || deathFlowStarted)
        {
            return;
        }

        waitingForDeathSequenceCompletion = false;
        BeginDeathRestartUiFlow();
    }

    private void BeginDeathRestartUiFlow()
    {
        if (deathFlowRoutine != null)
        {
            StopCoroutine(deathFlowRoutine);
        }

        deathFlowRoutine = StartCoroutine(DeathRestartFlowRoutine());
    }

    private void CaptureDeathRestoreSnapshot()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        if (!currentScene.IsValid())
        {
            return;
        }

        Phase2DeathResumeCache.Clear();
        Phase2DeathResumeCache.hasPendingRestore = true;
        Phase2DeathResumeCache.sceneName = currentScene.name;
        CaptureTrueFlagsForRestore(Phase2DeathResumeCache.trueFlagNames);

        if (boundPlayerState == null)
        {
            return;
        }

        Vector3 respawnPosition = boundPlayerState.transform.position;
        Quaternion respawnRotation = boundPlayerState.transform.rotation;
        float baselineY = respawnPosition.y;

        if (TryGetConfiguredRespawnPoint(out Vector3 configuredPosition, out Quaternion configuredRotation))
        {
            respawnPosition = configuredPosition;
            respawnPosition.y += respawnHeightOffset;
            respawnRotation = configuredRotation;
        }
        else if (respawnAtNearestMonsterPosition && TryGetNearestMonsterPosition(respawnPosition, out Vector3 monsterPosition))
        {
            respawnPosition = monsterPosition;
            respawnPosition.y = baselineY + respawnHeightOffset;
        }
        else
        {
            respawnPosition.y += respawnHeightOffset;
        }

        Phase2DeathResumeCache.hasRespawnTransform = true;
        Phase2DeathResumeCache.respawnPosition = respawnPosition;
        Phase2DeathResumeCache.respawnRotation = respawnRotation;
    }

    private void CaptureTrueFlagsForRestore(List<string> target)
    {
        target.Clear();

        if (flags == null)
        {
            return;
        }

        foreach (GameFlag flag in flags)
        {
            if (flag == null || string.IsNullOrWhiteSpace(flag.flagName) || !flag.currentValue)
            {
                continue;
            }

            if (!target.Contains(flag.flagName))
            {
                target.Add(flag.flagName);
            }
        }
    }

    private bool TryGetNearestMonsterPosition(Vector3 origin, out Vector3 position)
    {
        position = origin;
        float bestDistanceSqr = float.MaxValue;
        bool found = false;

        MonsterController[] monsters = FindObjectsOfType<MonsterController>(true);
        foreach (MonsterController monster in monsters)
        {
            if (monster == null || !monster.gameObject.scene.IsValid() || monster.IsDead)
            {
                continue;
            }

            float distanceSqr = (monster.transform.position - origin).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            position = monster.transform.position;
            found = true;
        }

        return found;
    }

    private bool TryGetConfiguredRespawnPoint(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (respawnPointTransform == null)
        {
            ResolveTransitionReferences();
        }

        if (respawnPointTransform == null)
        {
            return false;
        }

        position = respawnPointTransform.position;
        rotation = respawnPointTransform.rotation;
        return true;
    }

    private void TryResolveRespawnPointTransform()
    {
        if (respawnPointTransform != null)
        {
            return;
        }

        respawnPointTransform = FindSceneTransformByName(respawnPointObjectName);
    }

    private void TryResolveRespawnLookAtTarget()
    {
        if (respawnLookAtTarget != null)
        {
            return;
        }

        respawnLookAtTarget = FindSceneTransformByName(respawnLookAtObjectName);
    }

    private static Transform FindSceneTransformByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        GameObject exactObject = GameObject.Find(targetName);
        if (exactObject != null)
        {
            return exactObject.transform;
        }

        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        Transform containsMatch = null;
        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            string candidateName = candidate.name;
            if (string.Equals(candidateName, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (containsMatch == null &&
                !string.IsNullOrEmpty(candidateName) &&
                candidateName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                containsMatch = candidate;
            }
        }

        return containsMatch;
    }

    private bool TryGetRespawnLookDirection(Vector3 playerPosition, out Vector3 direction)
    {
        direction = Vector3.zero;

        if (!faceRespawnLookAtTargetOnRevive)
        {
            return false;
        }

        if (respawnLookAtTarget == null)
        {
            ResolveTransitionReferences();
        }

        if (respawnLookAtTarget == null)
        {
            return false;
        }

        Vector3 rawDirection = respawnLookAtTarget.position - playerPosition;
        if (rawDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        direction = rawDirection.normalized;
        return true;
    }

    private IEnumerator DeathRestartFlowRoutine()
    {
        deathFlowStarted = true;

        ResolveTransitionReferences();
        TryPlayDeathBlink();
        ShowDeathRestartOverlay(true, true);
        yield return null;
        deathFlowRoutine = null;
    }

    private void TryPlayDeathBlink()
    {
        if (playerEyeControl == null)
        {
            return;
        }

        playerEyeControl.ManualBlinkDuration = Mathf.Max(
            playerEyeControl.ManualBlinkDuration,
            Mathf.Max(0.1f, deathBlackScreenDuration + 0.1f));
        playerEyeControl.BeginManualBlink();
    }

    private void HandleDeathResetButtonPressed()
    {
        waitingForDeathSequenceCompletion = false;

        if (!Phase2DeathResumeCache.hasPendingRestore)
        {
            CaptureDeathRestoreSnapshot();
        }

        Scene currentScene = SceneManager.GetActiveScene();
        if (!currentScene.IsValid())
        {
            return;
        }

        SceneManager.LoadScene(currentScene.name);
    }

    private void TryApplyPendingDeathRestore()
    {
        deathFlowStarted = false;
        waitingForDeathSequenceCompletion = false;

        if (!enableInSceneDeathRestart || !Phase2DeathResumeCache.hasPendingRestore)
        {
            return;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        if (!currentScene.IsValid())
        {
            Phase2DeathResumeCache.Clear();
            return;
        }

        if (!string.Equals(currentScene.name, Phase2DeathResumeCache.sceneName, StringComparison.Ordinal))
        {
            Phase2DeathResumeCache.Clear();
            return;
        }

        BindPlayerDeathEvents();
        ResolveTransitionReferences();
        RestorePlayerAfterDeath();
        RestoreFlagsAfterDeath();
        Phase2DeathResumeCache.Clear();
    }

    private void RestorePlayerAfterDeath()
    {
        if (boundPlayerState == null)
        {
            return;
        }

        Transform playerTransform = boundPlayerState.transform;
        CharacterController characterController = boundPlayerState.GetComponent<CharacterController>();

        if (Phase2DeathResumeCache.hasRespawnTransform)
        {
            bool reenableCharacterController = characterController != null && characterController.enabled;
            if (reenableCharacterController)
            {
                characterController.enabled = false;
            }

            playerTransform.SetPositionAndRotation(
                Phase2DeathResumeCache.respawnPosition,
                Phase2DeathResumeCache.respawnRotation);
            Physics.SyncTransforms();

            if (reenableCharacterController)
            {
                characterController.enabled = true;
            }
        }

        ApplyRespawnFacing(playerTransform);

        boundPlayerState.Revive();

        if (playerEyeControl != null)
        {
            playerEyeControl.ResetWetness();
        }
    }

    private void ApplyRespawnFacing(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            return;
        }

        bool hasLookTargetDirection = TryGetRespawnLookDirection(playerTransform.position, out Vector3 lookDirection);
        if (!hasLookTargetDirection && !Phase2DeathResumeCache.hasRespawnTransform)
        {
            return;
        }

        Vector3 desiredDirection = hasLookTargetDirection
            ? lookDirection
            : (Phase2DeathResumeCache.respawnRotation * Vector3.forward);
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        FirstPersonController controller = boundPlayerState != null
            ? boundPlayerState.GetComponent<FirstPersonController>()
            : null;
        if (controller != null)
        {
            controller.ForceLookDirection(desiredDirection);
            return;
        }

        Vector3 planarForward = Vector3.ProjectOnPlane(desiredDirection, Vector3.up);
        if (planarForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        playerTransform.rotation = Quaternion.LookRotation(planarForward.normalized, Vector3.up);
    }

    private void RestoreFlagsAfterDeath()
    {
        if (Phase2DeathResumeCache.trueFlagNames.Count == 0)
        {
            return;
        }

        suppressTransitionFlagHandling = true;
        try
        {
            foreach (string flagName in Phase2DeathResumeCache.trueFlagNames)
            {
                if (string.IsNullOrWhiteSpace(flagName) || !flagMap.ContainsKey(flagName))
                {
                    continue;
                }

                SetFlagTrue(flagName);
            }
        }
        finally
        {
            suppressTransitionFlagHandling = false;
        }
    }

    private void ShowDeathRestartOverlay(bool showContent, bool showResetButton)
    {
        EnsureDeathRestartOverlay();
        if (deathRestartCanvasGroup == null)
        {
            return;
        }

        deathRestartCanvasGroup.alpha = 1f;
        deathRestartCanvasGroup.interactable = showContent && showResetButton;
        deathRestartCanvasGroup.blocksRaycasts = true;

        if (deathRestartTitleText != null)
        {
            deathRestartTitleText.gameObject.SetActive(showContent);
            deathRestartTitleText.text = string.IsNullOrWhiteSpace(deathUiTitle)
                ? "YOU DIED"
                : deathUiTitle;
        }

        if (deathRestartButtonText != null)
        {
            deathRestartButtonText.text = string.IsNullOrWhiteSpace(deathUiButtonText)
                ? "Reset"
                : deathUiButtonText;
        }

        if (deathRestartButton != null)
        {
            bool shouldShowButton = showContent && showResetButton;
            deathRestartButton.gameObject.SetActive(shouldShowButton);
            deathRestartButton.interactable = shouldShowButton;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void EnsureDeathRestartOverlay()
    {
        if (deathRestartCanvasGroup != null)
        {
            return;
        }

        GameObject root = new GameObject(
            "Phase2DeathRestartUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));
        deathRestartCanvas = root.GetComponent<Canvas>();
        deathRestartCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        deathRestartCanvas.sortingOrder = short.MaxValue - 1;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        deathRestartCanvasGroup = root.GetComponent<CanvasGroup>();
        deathRestartCanvasGroup.alpha = 0f;
        deathRestartCanvasGroup.interactable = false;
        deathRestartCanvasGroup.blocksRaycasts = false;

        Font font = ResolveBuiltinUiFont();

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(root.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(deathOverlayAlpha));

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleObject.transform.SetParent(root.transform, false);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 54f);
        titleRect.sizeDelta = new Vector2(980f, 130f);

        deathRestartTitleText = titleObject.GetComponent<Text>();
        deathRestartTitleText.alignment = TextAnchor.MiddleCenter;
        deathRestartTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        deathRestartTitleText.verticalOverflow = VerticalWrapMode.Overflow;
        deathRestartTitleText.fontSize = 56;
        deathRestartTitleText.fontStyle = FontStyle.Bold;
        deathRestartTitleText.color = Color.white;
        deathRestartTitleText.raycastTarget = false;
        deathRestartTitleText.font = font;
        deathRestartTitleText.text = "YOU DIED";

        GameObject buttonObject = new GameObject("ResetButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(root.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -72f);
        buttonRect.sizeDelta = new Vector2(330f, 94f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(1f, 1f, 1f, 0.28f);

        deathRestartButton = buttonObject.GetComponent<Button>();
        deathRestartButton.targetGraphic = buttonImage;
        deathRestartButton.onClick.RemoveListener(HandleDeathResetButtonPressed);
        deathRestartButton.onClick.AddListener(HandleDeathResetButtonPressed);

        GameObject buttonTextObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        buttonTextObject.transform.SetParent(buttonObject.transform, false);
        RectTransform buttonTextRect = buttonTextObject.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        deathRestartButtonText = buttonTextObject.GetComponent<Text>();
        deathRestartButtonText.alignment = TextAnchor.MiddleCenter;
        deathRestartButtonText.fontSize = 34;
        deathRestartButtonText.fontStyle = FontStyle.Bold;
        deathRestartButtonText.color = Color.white;
        deathRestartButtonText.raycastTarget = false;
        deathRestartButtonText.font = font;
        deathRestartButtonText.text = "Reset";

        deathRestartButton.gameObject.SetActive(false);
    }

    private void StartPhase3Preload()
    {
        NormalizePhase3SceneName();

        if (preloadStarted || string.IsNullOrWhiteSpace(phase3SceneName))
        {
            return;
        }

        preloadOperation = SceneManager.LoadSceneAsync(phase3SceneName, LoadSceneMode.Single);
        if (preloadOperation == null)
        {
            Debug.LogError($"【导演】异步预加载失败，场景无效: {phase3SceneName}");
            return;
        }

        preloadOperation.priority = 100;
        preloadOperation.allowSceneActivation = false;
        preloadStarted = true;
        Debug.Log($"【导演】开始异步预加载场景: {phase3SceneName}");
    }

    private void StartLevelCompleteSequence()
    {
        if (levelCompleteSequenceStarted || transitionTriggered)
        {
            return;
        }

        StartCoroutine(LevelCompleteSequenceRoutine());
    }

    private IEnumerator LevelCompleteSequenceRoutine()
    {
        levelCompleteSequenceStarted = true;

        if (delayBeforeCloseDoor > 0f)
        {
            if (useUnscaledTime) yield return new WaitForSecondsRealtime(delayBeforeCloseDoor);
            else yield return new WaitForSeconds(delayBeforeCloseDoor);
        }

        ResolveTransitionReferences();
        yield return CloseElevatorDoorAndWait();
        yield return ExecuteTransitionToPhase3();
    }

    private IEnumerator CloseElevatorDoorAndWait()
    {
        if (elevatorDoors == null)
        {
            yield break;
        }

        if (!elevatorDoors.IsOpen && !elevatorDoors.IsMoving)
        {
            yield break;
        }

        bool closedEventRaised = false;
        UnityAction onDoorClosed = () => closedEventRaised = true;
        elevatorDoors.onFullyClosed.AddListener(onDoorClosed);
        elevatorDoors.CloseElevator();

        float timeout = elevatorDoors.EstimatedMoveDuration + Mathf.Max(0f, doorCloseTimeoutPadding);
        float elapsed = 0f;

        while (!closedEventRaised && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        elevatorDoors.onFullyClosed.RemoveListener(onDoorClosed);
    }

    private IEnumerator ExecuteTransitionToPhase3()
    {
        if (transitionTriggered)
        {
            yield break;
        }

        transitionTriggered = true;
        NormalizePhase3SceneName();

        if (forceBlinkBeforeTransition)
        {
            ResolveTransitionReferences();
            if (playerEyeControl != null)
            {
                playerEyeControl.BeginManualBlink();
            }
        }

        if (delayAfterBlink > 0f)
        {
            if (useUnscaledTime) yield return new WaitForSecondsRealtime(delayAfterBlink);
            else yield return new WaitForSeconds(delayAfterBlink);
        }

        ShowTransitionLoadingOverlay();
        // 先让 UI 至少渲染一帧，再进入可能的激活卡顿阶段。
        yield return null;

        if (!preloadStarted)
        {
            StartPhase3Preload();
        }

        if (preloadOperation != null)
        {
            while (preloadOperation.progress < 0.89f)
            {
                float normalizedPreload = Mathf.Clamp01(preloadOperation.progress / 0.9f);
                UpdateTransitionLoadingOverlay(normalizedPreload, false);
                yield return null;
            }

            UpdateTransitionLoadingOverlay(1f, false);
            preloadOperation.allowSceneActivation = true;
            while (!preloadOperation.isDone)
            {
                UpdateTransitionLoadingOverlay(1f, true);
                yield return null;
            }
        }
        else if (!string.IsNullOrWhiteSpace(phase3SceneName))
        {
            UpdateTransitionLoadingOverlay(1f, true);
            yield return null;
            SceneManager.LoadScene(phase3SceneName);
        }
    }

    private void NormalizePhase3SceneName()
    {
        if (string.IsNullOrWhiteSpace(phase3SceneName))
        {
            phase3SceneName = RemakePhase3SceneName;
            return;
        }

        if (string.Equals(phase3SceneName, LegacyPhase3SceneName, StringComparison.OrdinalIgnoreCase))
        {
            phase3SceneName = RemakePhase3SceneName;
        }
    }

    private void ShowTransitionLoadingOverlay()
    {
        if (!showLoadingUiDuringTransition)
        {
            return;
        }

        EnsureTransitionLoadingOverlay();
        if (transitionLoadingCanvasGroup == null)
        {
            return;
        }

        transitionLoadingAnimTimer = 0f;
        transitionLoadingCanvasGroup.alpha = 1f;
        transitionLoadingCanvasGroup.interactable = blockUiInputWhileLoading;
        transitionLoadingCanvasGroup.blocksRaycasts = blockUiInputWhileLoading;

        if (transitionLoadingTitleText != null)
        {
            transitionLoadingTitleText.text = string.IsNullOrWhiteSpace(transitionLoadingTitle)
                ? "Transitioning to the next scene..."
                : transitionLoadingTitle;
        }

        UpdateTransitionLoadingOverlay(0f, true);
    }

    private void UpdateTransitionLoadingOverlay(float normalizedProgress, bool indeterminate)
    {
        if (!showLoadingUiDuringTransition || transitionLoadingDetailText == null)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        transitionLoadingAnimTimer += Mathf.Max(0f, deltaTime);

        int frameIndex = Mathf.FloorToInt(transitionLoadingAnimTimer * 8f);
        frameIndex %= LoadingSpinnerFrames.Length;
        if (frameIndex < 0)
        {
            frameIndex += LoadingSpinnerFrames.Length;
        }

        char spinner = LoadingSpinnerFrames[frameIndex];
        string prefix = string.IsNullOrWhiteSpace(transitionLoadingDetailPrefix)
            ? "Loading"
            : transitionLoadingDetailPrefix;

        if (indeterminate)
        {
            transitionLoadingDetailText.text = $"{prefix} {spinner}";
            return;
        }

        int percent = Mathf.Clamp(Mathf.RoundToInt(normalizedProgress * 100f), 0, 100);
        transitionLoadingDetailText.text = $"{prefix} {spinner}  {percent}%";
    }

    private void EnsureTransitionLoadingOverlay()
    {
        if (transitionLoadingCanvasGroup != null)
        {
            return;
        }

        var root = new GameObject(
            "Phase3TransitionLoadingUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));
        transitionLoadingCanvas = root.GetComponent<Canvas>();
        transitionLoadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transitionLoadingCanvas.sortingOrder = short.MaxValue;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        transitionLoadingCanvasGroup = root.GetComponent<CanvasGroup>();
        transitionLoadingCanvasGroup.alpha = 0f;
        transitionLoadingCanvasGroup.interactable = false;
        transitionLoadingCanvasGroup.blocksRaycasts = false;

        Font font = ResolveBuiltinUiFont();

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(root.transform, false);
        var bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = background.GetComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(transitionLoadingOverlayAlpha));

        var titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleObject.transform.SetParent(root.transform, false);
        var titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 28f);
        titleRect.sizeDelta = new Vector2(1100f, 120f);

        transitionLoadingTitleText = titleObject.GetComponent<Text>();
        transitionLoadingTitleText.alignment = TextAnchor.MiddleCenter;
        transitionLoadingTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        transitionLoadingTitleText.verticalOverflow = VerticalWrapMode.Overflow;
        transitionLoadingTitleText.fontSize = 44;
        transitionLoadingTitleText.fontStyle = FontStyle.Bold;
        transitionLoadingTitleText.color = Color.white;
        transitionLoadingTitleText.raycastTarget = false;
        transitionLoadingTitleText.font = font;
        transitionLoadingTitleText.text = "Transitioning to the next scene...";

        var detailObject = new GameObject("Detail", typeof(RectTransform), typeof(Text));
        detailObject.transform.SetParent(root.transform, false);
        var detailRect = detailObject.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.5f, 0.5f);
        detailRect.anchorMax = new Vector2(0.5f, 0.5f);
        detailRect.pivot = new Vector2(0.5f, 0.5f);
        detailRect.anchoredPosition = new Vector2(0f, -46f);
        detailRect.sizeDelta = new Vector2(760f, 80f);

        transitionLoadingDetailText = detailObject.GetComponent<Text>();
        transitionLoadingDetailText.alignment = TextAnchor.MiddleCenter;
        transitionLoadingDetailText.fontSize = 30;
        transitionLoadingDetailText.color = new Color(1f, 1f, 1f, 0.95f);
        transitionLoadingDetailText.raycastTarget = false;
        transitionLoadingDetailText.font = font;
        transitionLoadingDetailText.text = "Loading";
    }

    private static Font ResolveBuiltinUiFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private void DestroyTransitionLoadingOverlay()
    {
        transitionLoadingCanvas = null;
        transitionLoadingCanvasGroup = null;
        transitionLoadingTitleText = null;
        transitionLoadingDetailText = null;

        var existing = GameObject.Find("Phase3TransitionLoadingUI");
        if (existing != null)
        {
            Destroy(existing);
        }
    }

    private void DestroyDeathRestartOverlay()
    {
        if (deathRestartButton != null)
        {
            deathRestartButton.onClick.RemoveListener(HandleDeathResetButtonPressed);
        }

        deathRestartCanvas = null;
        deathRestartCanvasGroup = null;
        deathRestartTitleText = null;
        deathRestartButton = null;
        deathRestartButtonText = null;

        GameObject existing = GameObject.Find("Phase2DeathRestartUI");
        if (existing != null)
        {
            Destroy(existing);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        delayBeforeCloseDoor = Mathf.Max(0f, delayBeforeCloseDoor);
        delayAfterBlink = Mathf.Max(0f, delayAfterBlink);
        doorCloseTimeoutPadding = Mathf.Max(0f, doorCloseTimeoutPadding);
        transitionLoadingOverlayAlpha = Mathf.Clamp01(transitionLoadingOverlayAlpha);
        deathBlackScreenDuration = Mathf.Max(0f, deathBlackScreenDuration);
        deathOverlayAlpha = Mathf.Clamp01(deathOverlayAlpha);
        NormalizePhase3SceneName();
    }
#endif
}
