using LSP.Gameplay;
using LSP.Gameplay.Interactions;
using UnityEngine;
using UnityEngine.Events;

public class InteractableSetFlag : MonoBehaviour, IInteractable
{
    // 定义触发类型枚举
    public enum TriggerType
    {
        InteractOnly,   // 仅按键交互 (默认)
        TouchOnly,      // 仅触碰触发 (走进触发器)
        Both            // 按键或触碰都可以
    }

    [Header("===== 触发方式设置 =====")]
    [Tooltip("选择这个物体是如何被触发的")]
    public TriggerType triggerType = TriggerType.InteractOnly;

    [Header("===== 核心配置 =====")] 
    [Tooltip("触发后，要把导演里的哪个布尔值设为 true？(例如: HasDirtyArm)")]
    public string targetFlagName = "HasDirtyArm";

    [Tooltip("是否播放捡起音效？")] 
    public bool playSound = true;
    public AudioSource audioSource;
    public AudioClip pickupSound;     
    public AudioClip successSound;    
    
    [Tooltip("触发成功后是否隐藏自己？(捡起物品选 true，如果是开关/触发器选 false)")]
    public bool hideOnInteract = true;

    [Header("===== 前置条件 (可选) =====")]
    [Tooltip("是否需要检查前置条件？")]
    public bool checkPrerequisite = false;

    [Tooltip("需要检查的 Flag 名字")]
    public string prerequisiteFlagName;

    [Tooltip("如果条件不满足，播放这个拒绝音效")]
    public AudioClip failSound;

    [Header("===== 连带显示 (可选) =====")]
    [Tooltip("触发成功后，要显示哪个原本隐藏的物体？")]
    public GameObject objectToShow; 

    [Header("===== 成功回调 (可选) =====")]
    [Tooltip("触发成功后调用，可用于播放清洗/动画等演出。")]
    public UnityEvent onTriggeredSuccess;

    // 防止重复触发的内部标记
    private bool _hasTriggered = false;

    // =========================================================
    // 1. 玩家点击按键交互 (Interact)
    // =========================================================
    public bool CanInteract(PlayerInteractionController caller)
    {
        // 如果设置为“仅触碰”，则不显示交互提示，也不允许按键
        if (triggerType == TriggerType.TouchOnly) return false;
        return !_hasTriggered;
    }

    public void Interact(PlayerInteractionController caller)
    {
        if (triggerType == TriggerType.TouchOnly) return;
        
        ExecuteLogic("按键交互");
    }

    // =========================================================
    // 2. 玩家身体触碰交互 (Trigger Enter)
    // =========================================================
    private void OnTriggerEnter(Collider other)
    {
        // 1. 检查模式是否允许触碰
        if (triggerType == TriggerType.InteractOnly) return;

        // 2. 检查撞到的是不是玩家 (需要你的玩家物体Tag是 "Player")
        if (other.CompareTag("Player"))
        {
            ExecuteLogic("触碰触发");
        }
    }

    // =========================================================
    // 3. 核心逻辑 (被提取出来了，公用)
    // =========================================================
    public void ExecuteLogic(string source)
    {
        if (_hasTriggered) return; // 防止一瞬间触发两次

        // 0. 检查单例
        if (LevelTwoDirector.Instance == null)
        {
            Debug.LogWarning("场景里找不到 LevelTwoDirector！无法进行判定。");
            return;
        }

        // 1. 前置条件判定
        if (checkPrerequisite)
        {
            bool hasRequirement = LevelTwoDirector.Instance.GetFlag(prerequisiteFlagName);

            if (!hasRequirement)
            {
                Debug.Log($"【{source}失败】缺上前置条件: {prerequisiteFlagName}");

                // 只有按键交互时才播拒绝声音，不然走过去一直播声音很吵
                if (source == "按键交互" && failSound)
                {
                    AudioSource.PlayClipAtPoint(failSound, transform.position);
                }
                return; 
            }
        }

        // ==================== 成功逻辑 ====================
        
        Debug.Log($"【{source}成功】设置 Flag: {targetFlagName} = True");
        
        // A. 设置 Flag
        LevelTwoDirector.Instance.SetFlagTrue(targetFlagName);

        // B. 播放音效
        AudioClip clipToPlay = successSound != null ? successSound : pickupSound;
        if (playSound && clipToPlay)
        {
            AudioSource.PlayClipAtPoint(clipToPlay, transform.position);
        }

        // C. 显示隐藏物体
        if (objectToShow != null)
        {
            objectToShow.SetActive(true);
        }

        // C2. 触发扩展事件（例如：清洗演出）
        if (onTriggeredSuccess != null)
        {
            onTriggeredSuccess.Invoke();
        }

        // D. 隐藏/销毁自己
        if (hideOnInteract)
        {
            gameObject.SetActive(false);
        }
        else
        {
            // 如果不隐藏，标记为已触发，防止玩家反复刷触发
            _hasTriggered = true;
        }
    }
}
