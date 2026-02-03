using UnityEngine;
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
    // === 【修改点 1】不再直接引用门，而是使用通用事件 ===
    [Header("===== 剧情事件 (Events) =====")]
    [Tooltip("当怪物消失进入潜伏期时触发（请在这里绑定关门、锁死等逻辑）")]
    [SerializeField] private UnityEvent onMonsterDisappeared;
    
    [Header("===== 触发条件 (新增) =====")]
    // 【新增 1】这就是你要加的那个 bool，用来标记玩家是否到了特定位置
    [SerializeField] private bool isPlayerInTrapZone = false;
    
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
                if (!isSeeingMonster )
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
    
    // 【新增 2】给外部触发器调用的方法
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

    // === 自动寻找玩家的核心修改 ===
    // 这里把原来的 void 改成了协程启动器
    private void ExecuteJumpscare()
    {
        currentStage = Stage.Finished;
        Debug.Log("【导演】触发死亡流程！");

        // 启动协程，开始“先杀人 -> 等动画 -> 出UI”的流程
        StartCoroutine(JumpscareSequenceRoutine());
    }

    // 新增的协程逻辑
    // 新增的协程逻辑
    private IEnumerator JumpscareSequenceRoutine()
    {
        // 1. 播放 Jumpscare 音效
        if (audioSource && jumpScareSound) audioSource.PlayOneShot(jumpScareSound);

        // 2. 找到玩家
        PlayerStateController player = FindObjectOfType<PlayerStateController>();

        if (player != null)
        {
            var deathSequence = player.GetComponent<PlayerExecutionDeathSequence>();
            
            // 【关键】防止原本的脚本自动跳场景，必须按住它
            if (deathSequence != null)
            {
                deathSequence.SuppressAutomaticSceneLoad(); 
            }

            // 3. 触发玩家死亡动画
            Debug.Log($"【流程】玩家死亡，开始播放动画，等待 {deathAnimationDuration} 秒...");
            player.Kill();
            
            // 为了防止玩家在死亡动画期间还能乱动（双重保险），可以先锁住输入
            // var input = player.GetComponent<StarterAssetsInputs>();
            // if (input != null) input.cursorInputForLook = false;

            // =======================================================
            // 4. 【核心等待】让程序暂停，等待动画播完
            // 务必在 Inspector 面板里把 deathAnimationDuration 设置得比动画长一点点
            // =======================================================
            yield return new WaitForSeconds(deathAnimationDuration);

            Debug.Log("【流程】动画等待结束，显示 UI");

            // 5. 动画播完了，现在激活 UI
            if (endgameCanvas != null) 
            {
                endgameCanvas.gameObject.SetActive(true);
                
                // 【重要】如果你希望死亡后玩家模型还在（比如躺在地上），不要 SetActive(false)
                // 但如果你希望只留下 UI，可以隐藏玩家。建议不要隐藏，否则摄像机可能会黑屏。
                // player.gameObject.SetActive(false); 

                // 6. 彻底解锁鼠标光标 (确保玩家能看见鼠标并点击 UI 按钮)
                Cursor.lockState = CursorLockMode.None; // 解除锁定
                Cursor.visible = true;                  // 显示鼠标
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
}