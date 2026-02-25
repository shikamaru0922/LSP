using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using LSP.Gameplay;
using StarterAssets;
using UnityEngine.Events;

public class LevelOneFlowDirector : MonoBehaviour
{
    [Header("===== 核心检测 =====")]
    [SerializeField] private PlayerVision playerVision;
    [SerializeField] private Collider monsterCollider;

    [Header("===== 演员模型控制 =====")]
    [SerializeField] private GameObject npcAlive;
    [SerializeField] private GameObject npcDead;
    
    [Tooltip("怪物的所有模型父物体")]
    [SerializeField] private GameObject monsterVisualRoot;
    [SerializeField] private Animator monsterAnimator;
    [SerializeField] private string killStateName = "KillPose";

    [Header("===== 结局设置 =====")]
    [Tooltip("当玩家盯着空位看时，延迟多久触发死亡？")]
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
    [Tooltip("当怪物消失进入潜伏期时触发（请在这里绑定关门、锁死等逻辑）")]
    [SerializeField] private UnityEvent onMonsterDisappeared;
    
    [Header("===== 触发条件 =====")]
    [SerializeField] private bool isPlayerInTrapZone = false;

    [Header("===== 结局场景清理 =====")]
    [Tooltip("除了玩家和UI以外，这些物体也要保留（比如灯光、地面等）")]
    [SerializeField] private GameObject[] additionalObjectsToKeep;
    
    private enum Stage { Setup, WaitingForLookAway1, Transformation, WaitingForLookAway2, Disappearance, CountingDown, Finished }
    private Stage currentStage = Stage.Setup;

    private void Start()
    {
        if(npcAlive) npcAlive.SetActive(true);
        if(npcDead) npcDead.SetActive(false);
        if(monsterVisualRoot) monsterVisualRoot.SetActive(true);

        if (monsterAnimator != null)
        {
            monsterAnimator.enabled = true;
            monsterAnimator.speed = 1f;
        }
    }

    private void Update()
    {
        if (currentStage == Stage.Finished || playerVision == null || monsterCollider == null) return;

        bool isSeeingMonster = playerVision.CanSee(monsterCollider);

        switch (currentStage)
        {
            case Stage.Setup:
                if (isSeeingMonster) currentStage = Stage.WaitingForLookAway1;
                break;
            case Stage.WaitingForLookAway1:
                if (!isSeeingMonster)
                {
                    Debug.Log("1");
                    PerformTransformation();
                    currentStage = Stage.Transformation;
                }
                break;
            case Stage.Transformation:
                if (isSeeingMonster) currentStage = Stage.WaitingForLookAway2;
                break;
            case Stage.WaitingForLookAway2:
                if (!isSeeingMonster)
                {
                    Debug.Log("2");
                    PerformDisappearance();
                    currentStage = Stage.Disappearance;
                    Debug.Log(isSeeingMonster);
                    Debug.Log(isPlayerInTrapZone);
                }
                break;
            case Stage.Disappearance:
                if (isSeeingMonster && isPlayerInTrapZone)
                {
                    Debug.Log("3");
                    if (onMonsterDisappeared != null)
                    {
                        onMonsterDisappeared.Invoke();
                    }
                    Debug.Log("4");
                    currentStage = Stage.CountingDown;
                    StartCoroutine(CountdownToDeath());
                }
                break;
        }
    }
    
    public void SetPlayerInTrapZone(bool isInZone)
    {
        isPlayerInTrapZone = isInZone;
    }

    private void PerformTransformation()
    {
        if(npcAlive) npcAlive.SetActive(false);
        if(npcDead) npcDead.SetActive(true);
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

                // ====== 【新增】隐藏场景中除玩家和UI以外的所有物体 ======
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

    /// <summary>
    /// 遍历当前场景的所有根物体，把除了玩家、UI、以及手动指定保留的物体以外的全部隐藏。
    /// </summary>
    private void HideAllExcept(GameObject playerRoot, GameObject canvasRoot)
    {
        // 找到玩家和 UI 各自的最顶层根物体（场景层级第一层）
        Transform playerSceneRoot = GetSceneRoot(playerRoot.transform);
        Transform canvasSceneRoot = GetSceneRoot(canvasRoot.transform);

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = activeScene.GetRootGameObjects();

        foreach (GameObject rootObj in rootObjects)
        {
            // 保留玩家
            if (rootObj.transform == playerSceneRoot) continue;
            // 保留 UI
            if (rootObj.transform == canvasSceneRoot) continue;
            // 保留自身（协程还在跑，不能关自己）
            if (rootObj == this.gameObject || GetSceneRoot(this.transform) == rootObj.transform) continue;
            // 保留手动指定的额外物体
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
            // 如果保留列表里的物体本身就是根物体，或者它的根物体匹配
            if (keep == rootObj || GetSceneRoot(keep.transform).gameObject == rootObj)
                return true;
        }
        return false;
    }
}