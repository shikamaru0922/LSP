using UnityEngine;
using System.Collections;
using LSP.Gameplay; 

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
                    PerformDisappearance();
                    currentStage = Stage.Disappearance;
                }
                break;
            case Stage.Disappearance:
                if (isSeeingMonster)
                {
                    currentStage = Stage.CountingDown;
                    StartCoroutine(CountdownToDeath());
                }
                break;
        }
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
    private void ExecuteJumpscare()
    {
        currentStage = Stage.Finished;
        Debug.Log("【导演】触发死亡流程！");

        if (audioSource && jumpScareSound) audioSource.PlayOneShot(jumpScareSound);

        // 1. 自动在整个场景里寻找 PlayerStateController
        // 不管它挂在哪里，只要是 Active 的就能找到
        PlayerStateController player = FindObjectOfType<PlayerStateController>();

        if (player != null)
        {
            // 告诉死亡脚本下一关去哪
            var deathSequence = player.GetComponent<PlayerExecutionDeathSequence>();
            if (deathSequence != null)
            {
                deathSequence.OverrideSceneToLoad(nextSceneName);
            }

            // 处决玩家
            player.Kill();
        }
        else
        {
            // 如果连这也找不到，那说明场景里根本没有玩家，或者玩家被 Disable 了
            Debug.LogError("【错误】导演在场景里找不到任何 PlayerStateController！玩家是否存在？");
        }
    }
}