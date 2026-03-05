using System.Collections;
using LSP.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class LevelOneFlowDirector : MonoBehaviour
{
    private enum PreJumpscareAdvanceMode
    {
        Timed,
        BlinkPerStep
    }

    [Header("===== 核心检测 =====")]
    [SerializeField] private PlayerVision playerVision;
    [SerializeField] private PlayerEyeControl eyeControl;
    [SerializeField] private Collider monsterCollider;

    [Header("===== 注视与眨眼切阶段 =====")]
    [Tooltip("每次推进阶段前，玩家需要累计注视怪物的最短时长（秒）。")]
    [SerializeField] private float requiredSightDurationBeforeBlink = 2f;

    [Tooltip("若找不到 PlayerEyeControl，是否回退为旧逻辑（移开视线推进阶段）。")]
    [SerializeField] private bool fallbackToLookAwayWhenNoEyeControl = true;

    [Header("===== 演员模型控制 =====")]
    [SerializeField] private GameObject npcAlive;
    [SerializeField] private GameObject npcDead;

    [Tooltip("怪物的所有模型父物体")]
    [SerializeField] private GameObject monsterVisualRoot;
    [SerializeField] private Animator monsterAnimator;
    [SerializeField] private string killStateName = "KillPose";

    [Header("===== 结局设置 =====")]
    [Tooltip("进入最终陷阱后，多久触发跳杀（秒）。")]
    [SerializeField] private float delayBeforeJumpscare = 1.5f;

    [Tooltip("下一关的名字")]
    [SerializeField] private string nextSceneName = "Scene2";

    [Header("===== 音效 =====")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip killSound;
    [SerializeField] private AudioClip ambianceSound;
    [SerializeField] private AudioClip jumpScareSound;

    [Tooltip("改成 Screen Space - Camera 模式的那个 UI Canvas")]
    [SerializeField] private Canvas endgameCanvas;

    [Header("===== 结局时间控制 =====")]
    [Tooltip("死亡动画大概播多久？(秒) 等动画播完才会出 UI")]
    [SerializeField] private float deathAnimationDuration = 3.5f;

    [Header("===== 剧情事件 (Events) =====")]
    [Tooltip("当怪物消失进入潜伏期时触发（可绑定额外场景逻辑）。")]
    [SerializeField] private UnityEvent onMonsterDisappeared;

    [Header("===== 触发条件 =====")]
    [SerializeField] private bool isPlayerInTrapZone;

    [Header("===== 陷阱门封锁 =====")]
    [Tooltip("玩家进入陷阱区后需要移动的门。")]
    [SerializeField] private Transform trapDoor;

    [Tooltip("门的移动方向。")]
    [SerializeField] private Vector3 trapDoorMoveDirection = Vector3.right;

    [Tooltip("门移动的总距离（米）。")]
    [Min(0f)]
    [SerializeField] private float trapDoorMoveDistance = 2f;

    [Tooltip("门移动速度（米/秒）。")]
    [Min(0f)]
    [SerializeField] private float trapDoorMoveSpeed = 2f;

    [Tooltip("门按本地坐标还是世界坐标移动。")]
    [SerializeField] private Space trapDoorMoveSpace = Space.World;

    [Tooltip("玩家进入陷阱区后是否触发关门。")]
    [SerializeField] private bool closeTrapDoorWhenPlayerEnterTrapZone = true;

    [Header("===== 跳杀前演出物体 =====")]
    [SerializeField] private GameObject preJumpscareObject1;
    [SerializeField] private GameObject preJumpscareObject2;
    [SerializeField] private GameObject preJumpscareObject3;

    [Tooltip("三物体演出的推进方式：按时间 或 每次眨眼推进一步。")]
    [SerializeField] private PreJumpscareAdvanceMode preJumpscareAdvanceMode = PreJumpscareAdvanceMode.Timed;

    [Tooltip("每一步开关之间的间隔时间。")]
    [Min(0f)]
    [SerializeField] private float preJumpscareStepDelay = 0.3f;

    [Tooltip("三物体全开后，等待多久再执行击杀。")]
    [Min(0f)]
    [SerializeField] private float preJumpscareFinalHoldDelay = 0.5f;

    [Tooltip("若演出模式是眨眼推进，但没找到眼睛控制器，是否回退到按时间推进。")]
    [SerializeField] private bool fallbackToTimedSequenceWhenNoEyeControl = true;

    [SerializeField] private bool hidePreJumpscareObjectsOnStart = true;

    [Header("===== 结局场景清理 =====")]
    [Tooltip("除了玩家和UI以外，这些物体也要保留（比如灯光、地面等）")]
    [SerializeField] private GameObject[] additionalObjectsToKeep;

    private enum Stage
    {
        Setup,
        WaitingForBlink1,
        Transformation,
        WaitingForBlink2,
        Disappearance,
        CountingDown,
        Finished
    }

    private Stage currentStage = Stage.Setup;
    private float accumulatedSightTime;
    private bool wasBlinkingLastFrame;
    private bool doorMoveTriggered;
    private bool disappearanceEventInvoked;
    private Coroutine trapDoorMoveRoutine;

    private void Start()
    {
        if (npcAlive) npcAlive.SetActive(true);
        if (npcDead) npcDead.SetActive(false);
        if (monsterVisualRoot) monsterVisualRoot.SetActive(true);

        if (monsterAnimator != null)
        {
            monsterAnimator.enabled = true;
            monsterAnimator.speed = 1f;
        }

        if (playerVision == null)
        {
            playerVision = FindObjectOfType<PlayerVision>();
        }

        if (eyeControl == null)
        {
            eyeControl = FindObjectOfType<PlayerEyeControl>();
        }

        if (eyeControl != null)
        {
            wasBlinkingLastFrame = eyeControl.IsBlinking;
        }

        if (hidePreJumpscareObjectsOnStart)
        {
            SetPreJumpscareObjectsActive(false);
        }
    }

    private void Update()
    {
        if (currentStage == Stage.Finished || playerVision == null || monsterCollider == null)
        {
            return;
        }

        if (eyeControl == null)
        {
            eyeControl = FindObjectOfType<PlayerEyeControl>();
        }

        bool isSeeingMonster = playerVision.CanSee(monsterCollider);
        bool blinkStartedThisFrame = GetBlinkStartedThisFrame();

        switch (currentStage)
        {
            case Stage.Setup:
                if (HasReachedSightRequirement(isSeeingMonster))
                {
                    accumulatedSightTime = 0f;
                    currentStage = Stage.WaitingForBlink1;
                }
                break;

            case Stage.WaitingForBlink1:
                if (ShouldAdvanceOnBlink(blinkStartedThisFrame, isSeeingMonster))
                {
                    PerformTransformation();
                    accumulatedSightTime = 0f;
                    currentStage = Stage.Transformation;
                }
                break;

            case Stage.Transformation:
                if (HasReachedSightRequirement(isSeeingMonster))
                {
                    accumulatedSightTime = 0f;
                    currentStage = Stage.WaitingForBlink2;
                }
                break;

            case Stage.WaitingForBlink2:
                if (ShouldAdvanceOnBlink(blinkStartedThisFrame, isSeeingMonster))
                {
                    PerformDisappearance();
                    currentStage = Stage.Disappearance;
                }
                break;

            case Stage.Disappearance:
                if (isPlayerInTrapZone)
                {
                    TryStartTrapDoorMovement();

                    if (!disappearanceEventInvoked)
                    {
                        disappearanceEventInvoked = true;
                        onMonsterDisappeared?.Invoke();
                    }

                    currentStage = Stage.CountingDown;
                    StartCoroutine(CountdownToDeath());
                }
                break;
        }
    }

    public void SetPlayerInTrapZone(bool isInZone)
    {
        isPlayerInTrapZone = isInZone;

        if (isInZone && currentStage == Stage.Disappearance)
        {
            TryStartTrapDoorMovement();
        }
    }

    private bool HasReachedSightRequirement(bool isSeeingMonster)
    {
        if (eyeControl == null && fallbackToLookAwayWhenNoEyeControl)
        {
            return isSeeingMonster;
        }

        if (isSeeingMonster)
        {
            accumulatedSightTime += Time.deltaTime;
        }

        return accumulatedSightTime >= Mathf.Max(0f, requiredSightDurationBeforeBlink);
    }

    private bool ShouldAdvanceOnBlink(bool blinkStartedThisFrame, bool isSeeingMonster)
    {
        if (eyeControl != null)
        {
            return blinkStartedThisFrame;
        }

        return fallbackToLookAwayWhenNoEyeControl && !isSeeingMonster;
    }

    private bool GetBlinkStartedThisFrame()
    {
        if (eyeControl == null)
        {
            wasBlinkingLastFrame = false;
            return false;
        }

        bool isBlinkingNow = eyeControl.IsBlinking;
        bool startedThisFrame = isBlinkingNow && !wasBlinkingLastFrame;
        wasBlinkingLastFrame = isBlinkingNow;
        return startedThisFrame;
    }

    private void PerformTransformation()
    {
        if (npcAlive) npcAlive.SetActive(false);
        if (npcDead) npcDead.SetActive(true);
        if (monsterAnimator != null)
        {
            monsterAnimator.Play(killStateName, 0, 1.0f);
            monsterAnimator.speed = 0f;
        }
        if (audioSource && killSound) audioSource.PlayOneShot(killSound);
    }

    private void PerformDisappearance()
    {
        if (monsterVisualRoot) monsterVisualRoot.SetActive(false);
        if (audioSource && ambianceSound) audioSource.PlayOneShot(ambianceSound);
    }

    private void TryStartTrapDoorMovement()
    {
        if (!closeTrapDoorWhenPlayerEnterTrapZone || doorMoveTriggered || trapDoor == null)
        {
            return;
        }

        doorMoveTriggered = true;

        if (trapDoorMoveRoutine != null)
        {
            StopCoroutine(trapDoorMoveRoutine);
        }

        trapDoorMoveRoutine = StartCoroutine(MoveTrapDoorRoutine());
    }

    private IEnumerator MoveTrapDoorRoutine()
    {
        Vector3 direction = trapDoorMoveDirection.normalized;
        float distance = Mathf.Max(0f, trapDoorMoveDistance);
        float speed = Mathf.Max(0f, trapDoorMoveSpeed);

        if (direction.sqrMagnitude <= Mathf.Epsilon || distance <= Mathf.Epsilon || speed <= Mathf.Epsilon)
        {
            trapDoorMoveRoutine = null;
            yield break;
        }

        float duration = distance / speed;
        float elapsed = 0f;

        if (trapDoorMoveSpace == Space.Self)
        {
            Vector3 start = trapDoor.localPosition;
            Vector3 target = start + direction * distance;

            while (trapDoor != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                trapDoor.localPosition = Vector3.Lerp(start, target, t);
                yield return null;
            }

            if (trapDoor != null)
            {
                trapDoor.localPosition = target;
            }
        }
        else
        {
            Vector3 start = trapDoor.position;
            Vector3 target = start + direction * distance;

            while (trapDoor != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                trapDoor.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            if (trapDoor != null)
            {
                trapDoor.position = target;
            }
        }

        trapDoorMoveRoutine = null;
    }

    private IEnumerator CountdownToDeath()
    {
        yield return new WaitForSeconds(delayBeforeJumpscare);
        ExecuteJumpscare();
    }

    private void ExecuteJumpscare()
    {
        currentStage = Stage.Finished;
        Debug.Log("【导演】触发死亡流程！");
        StartCoroutine(JumpscareSequenceRoutine());
    }

    private IEnumerator JumpscareSequenceRoutine()
    {
        yield return PlayPreJumpscareObjectsSequence();

        if (audioSource && jumpScareSound) audioSource.PlayOneShot(jumpScareSound);

        PlayerStateController player = FindObjectOfType<PlayerStateController>();

        if (player != null)
        {
            var deathSequence = player.GetComponent<PlayerExecutionDeathSequence>();

            if (deathSequence != null)
            {
                deathSequence.SuppressAutomaticSceneLoad();
            }

            Debug.Log($"【流程】玩家死亡，开始播放动画，等待 {deathAnimationDuration} 秒...");
            player.Kill();

            yield return new WaitForSeconds(deathAnimationDuration);

            Debug.Log("【流程】动画等待结束，显示 UI");

            if (endgameCanvas != null)
            {
                endgameCanvas.gameObject.SetActive(true);
                HideAllExcept(player.gameObject, endgameCanvas.gameObject);

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Debug.LogError("【错误】Endgame Canvas 未赋值，UI 无法显示！");
            }
        }
        else
        {
            Debug.LogError("【错误】导演找不到 PlayerStateController，无法执行死亡流程！");
        }
    }

    private IEnumerator PlayPreJumpscareObjectsSequence()
    {
        if (preJumpscareObject1 == null && preJumpscareObject2 == null && preJumpscareObject3 == null)
        {
            yield break;
        }

        if (preJumpscareAdvanceMode == PreJumpscareAdvanceMode.BlinkPerStep)
        {
            if (eyeControl == null)
            {
                eyeControl = FindObjectOfType<PlayerEyeControl>();
            }

            if (eyeControl != null)
            {
                yield return PlayPreJumpscareObjectsSequenceByBlink();
                yield break;
            }

            if (!fallbackToTimedSequenceWhenNoEyeControl)
            {
                Debug.LogWarning("【导演】眨眼推进模式启用，但找不到 PlayerEyeControl，跳过三物体演出。");
                yield break;
            }
        }

        yield return PlayPreJumpscareObjectsSequenceByTime();
    }

    private IEnumerator PlayPreJumpscareObjectsSequenceByTime()
    {
        float stepDelay = Mathf.Max(0f, preJumpscareStepDelay);
        float finalHold = Mathf.Max(0f, preJumpscareFinalHoldDelay);

        SetPreJumpscareObjectsActive(false);

        SetObjectActive(preJumpscareObject1, true);
        if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);

        SetObjectActive(preJumpscareObject2, true);
        if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);

        SetObjectActive(preJumpscareObject3, true);
        if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);

        SetObjectActive(preJumpscareObject3, false);
        if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);

        SetObjectActive(preJumpscareObject1, false);
        SetObjectActive(preJumpscareObject2, false);
        if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);

        SetPreJumpscareObjectsActive(true);
        if (finalHold > 0f) yield return new WaitForSeconds(finalHold);
    }

    private IEnumerator PlayPreJumpscareObjectsSequenceByBlink()
    {
        float finalHold = Mathf.Max(0f, preJumpscareFinalHoldDelay);

        SetPreJumpscareObjectsActive(false);

        yield return WaitForNextBlinkStart();
        SetObjectActive(preJumpscareObject1, true);

        yield return WaitForNextBlinkStart();
        SetObjectActive(preJumpscareObject2, true);

        yield return WaitForNextBlinkStart();
        SetObjectActive(preJumpscareObject3, true);

        yield return WaitForNextBlinkStart();
        SetObjectActive(preJumpscareObject3, false);

        yield return WaitForNextBlinkStart();
        SetObjectActive(preJumpscareObject1, false);
        SetObjectActive(preJumpscareObject2, false);

        yield return WaitForNextBlinkStart();
        SetPreJumpscareObjectsActive(true);

        if (finalHold > 0f) yield return new WaitForSeconds(finalHold);
    }

    private IEnumerator WaitForNextBlinkStart()
    {
        if (eyeControl == null)
        {
            eyeControl = FindObjectOfType<PlayerEyeControl>();
        }

        if (eyeControl == null)
        {
            yield break;
        }

        bool lastBlinking = eyeControl.IsBlinking;
        while (true)
        {
            if (eyeControl == null)
            {
                yield break;
            }

            bool currentBlinking = eyeControl.IsBlinking;
            if (currentBlinking && !lastBlinking)
            {
                yield break;
            }

            lastBlinking = currentBlinking;
            yield return null;
        }
    }

    private void SetPreJumpscareObjectsActive(bool active)
    {
        SetObjectActive(preJumpscareObject1, active);
        SetObjectActive(preJumpscareObject2, active);
        SetObjectActive(preJumpscareObject3, active);
    }

    private static void SetObjectActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    /// <summary>
    /// 遍历当前场景的所有根物体，把除了玩家、UI、以及手动指定保留的物体以外的全部隐藏。
    /// </summary>
    private void HideAllExcept(GameObject playerRoot, GameObject canvasRoot)
    {
        Transform playerSceneRoot = GetSceneRoot(playerRoot.transform);
        Transform canvasSceneRoot = GetSceneRoot(canvasRoot.transform);

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = activeScene.GetRootGameObjects();

        foreach (GameObject rootObj in rootObjects)
        {
            if (rootObj.transform == playerSceneRoot) continue;
            if (rootObj.transform == canvasSceneRoot) continue;
            if (rootObj == gameObject || GetSceneRoot(transform) == rootObj.transform) continue;
            if (ShouldKeep(rootObj)) continue;

            rootObj.SetActive(false);
            Debug.Log($"【清理】已隐藏: {rootObj.name}");
        }
    }

    /// <summary>
    /// 沿 parent 链向上找到场景最顶层的根 Transform
    /// </summary>
    private Transform GetSceneRoot(Transform t)
    {
        while (t.parent != null) t = t.parent;
        return t;
    }

    /// <summary>
    /// 检查某个根物体是否在"额外保留"列表中
    /// </summary>
    private bool ShouldKeep(GameObject rootObj)
    {
        if (additionalObjectsToKeep == null) return false;
        foreach (var keep in additionalObjectsToKeep)
        {
            if (keep == null) continue;
            if (keep == rootObj || GetSceneRoot(keep.transform).gameObject == rootObj)
            {
                return true;
            }
        }

        return false;
    }
}
