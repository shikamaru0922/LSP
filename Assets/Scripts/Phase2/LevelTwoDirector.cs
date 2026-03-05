using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
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
    public static LevelTwoDirector Instance;

    [Header("===== 所有的游戏开关 (Flags) =====")]
    [Tooltip("在这里添加你所有的布尔值，比如 HasArm, HasKeyA...")]
    public List<GameFlag> flags = new List<GameFlag>();

    [Header("===== Phase2 -> Phase3 过场 =====")]
    [Tooltip("启用后：ElevatorBoxOpen=true 时预加载场景，LevelComplete=true 后延迟关门并切场景。")]
    [SerializeField] private bool enablePhase3TransitionFlow = true;

    [Tooltip("触发异步预加载的 Flag 名字。")]
    [SerializeField] private string preloadTriggerFlagName = "ElevatorBoxOpen";

    [Tooltip("触发关门和切场景流程的 Flag 名字。")]
    [SerializeField] private string levelCompleteFlagName = "LevelComplete";

    [Tooltip("要异步预加载并切换的目标场景名。")]
    [SerializeField] private string phase3SceneName = "Remake_Phase3";

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

    // 字典：用来快速查找，代码里查起来快
    private Dictionary<string, GameFlag> flagMap = new Dictionary<string, GameFlag>();
    private AsyncOperation preloadOperation;
    private bool preloadStarted;
    private bool levelCompleteSequenceStarted;
    private bool transitionTriggered;

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

        ResolveTransitionReferences();
    }

    private void Start()
    {
        if (!enablePhase3TransitionFlow)
        {
            return;
        }

        if (TryGetFlagValue(preloadTriggerFlagName, out bool preloadTrigger) && preloadTrigger)
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
                HandleTransitionFlagRaised(name);
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

        if (!string.IsNullOrWhiteSpace(preloadTriggerFlagName) && flagName == preloadTriggerFlagName)
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
    }

    private void StartPhase3Preload()
    {
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

        if (!preloadStarted)
        {
            StartPhase3Preload();
        }

        if (preloadOperation != null)
        {
            preloadOperation.allowSceneActivation = true;
            while (!preloadOperation.isDone)
            {
                yield return null;
            }
        }
        else if (!string.IsNullOrWhiteSpace(phase3SceneName))
        {
            SceneManager.LoadScene(phase3SceneName);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        delayBeforeCloseDoor = Mathf.Max(0f, delayBeforeCloseDoor);
        delayAfterBlink = Mathf.Max(0f, delayAfterBlink);
        doorCloseTimeoutPadding = Mathf.Max(0f, doorCloseTimeoutPadding);
    }
#endif
}
