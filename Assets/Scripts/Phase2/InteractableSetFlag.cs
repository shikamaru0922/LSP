using LSP.Gameplay;
using LSP.Gameplay.Interactions;
using UnityEngine;

public class InteractableSetFlag : MonoBehaviour, IInteractable
{
    [Header("===== 核心配置 =====")] 
    [Tooltip("交互后，要把导演里的哪个布尔值设为 true？(例如: HasDirtyArm)")]
    public string targetFlagName = "HasDirtyArm";

    [Tooltip("是否播放捡起音效？")] 
    public bool playSound = true;
    public AudioSource audioSource;
    public AudioClip pickupSound;     // 捡起的声音
    public AudioClip successSound;    // 成功/完成的声音 (如拆解声)
    
    [Tooltip("交互成功后是否隐藏自己？(捡起物品选 true，如果是开关/机器选 false)")]
    public bool hideOnInteract = true;

    // ---------------------------------------------------------
    //  【功能 1】前置条件检查
    // ---------------------------------------------------------
    [Header("===== 前置条件 (可选) =====")]
    [Tooltip("是否需要检查前置条件？(比如：必须有脏手臂才能洗)")]
    public bool checkPrerequisite = false;

    [Tooltip("需要检查的 Flag 名字 (例如: HasDirtyArm)")]
    public string prerequisiteFlagName;

    [Tooltip("如果条件不满足(Flag是false)，播放这个拒绝音效")]
    public AudioClip failSound;

    // ---------------------------------------------------------
    //  【功能 2】新增：显示其他物体
    // ---------------------------------------------------------
    [Header("===== 连带显示 (可选) =====")]
    [Tooltip("交互成功后，要显示哪个原本隐藏的物体？(例如：拆解后显示出来的钥匙)")]
    public GameObject objectToShow; 

    // ---------------------------------------------------------

    public bool CanInteract(PlayerInteractionController caller)
    {
        return true;
    }

    public void Interact(PlayerInteractionController caller)
    {
        // 0. 检查单例是否存在
        if (LevelTwoDirector.Instance == null)
        {
            Debug.LogWarning("场景里找不到 LevelTwoDirector！无法进行判定。");
            return;
        }

        // =====================================================
        // 1. 前置条件判定
        // =====================================================
        if (checkPrerequisite)
        {
            bool hasRequirement = LevelTwoDirector.Instance.GetFlag(prerequisiteFlagName);

            if (!hasRequirement)
            {
                Debug.Log($"【交互失败】缺上前置条件: {prerequisiteFlagName}");

                if (audioSource && failSound)
                {
                    AudioSource.PlayClipAtPoint(failSound, transform.position);
                }
                return; 
            }
        }

        // =====================================================
        // 2. 交互成功逻辑
        // =====================================================
        
        Debug.Log($"【交互成功】设置 Flag: {targetFlagName} = True");
        
        // A. 设置目标 Flag
        LevelTwoDirector.Instance.SetFlagTrue(targetFlagName);

        // B. 播放成功音效
        // 优先播放 successSound，如果没有则尝试播放 pickupSound
        AudioClip clipToPlay = successSound != null ? successSound : pickupSound;
        if (playSound && audioSource && clipToPlay)
        {
            AudioSource.PlayClipAtPoint(clipToPlay, transform.position);
        }

        // C. 【新增】显示原本隐藏的物体
        if (objectToShow != null)
        {
            objectToShow.SetActive(true);
            Debug.Log($"【物品】已激活显示物体: {objectToShow.name}");
        }

        // D. 隐藏自己 (如果是捡东西)
        if (hideOnInteract)
        {
            gameObject.SetActive(false);
        }
    }
}