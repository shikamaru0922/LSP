using UnityEngine;
using LSP.Gameplay; // 引用你项目原本的命名空间
using LSP.Gameplay.Interactions; // 引用接口所在的命名空间

// 继承你的接口 IInteractable
public class InteractableOpenUI : MonoBehaviour, IInteractable
{
    [Header("UI 连接设置")]
    [Tooltip("请把场景里的 PasswordCanvas (那个UI物体) 拖到这里")]
    public PasswordKeypad targetUI; 

    [Header("交互提示")]
    public string promptText = "输入密码"; // 比如显示 "按 E 输入密码"

    // 必须实现接口里的 CanInteract
    public bool CanInteract(PlayerInteractionController caller)
    {
        if (targetUI == null)
        {
            return false;
        }

        // 只要 UI 没打开且未解锁，就可以一次按键直接打开
        return !IsKeypadCurrentlyOpen() && !targetUI.IsSolved;
    }

    // 必须实现接口里的 Interact
    public void Interact(PlayerInteractionController caller)
    {
        Debug.Log("玩家点击了物体，准备打开UI...");

        if (targetUI != null)
        {
            if (IsKeypadCurrentlyOpen() || targetUI.IsSolved)
            {
                return;
            }

            // 调用 UI 脚本里的打开方法
            targetUI.OpenKeypad();
        }
        else
        {
            Debug.LogError("你忘记把 UI 物体拖进 Inspector 里的 TargetUI 槽位了！");
        }
    }

    private bool IsKeypadCurrentlyOpen()
    {
        if (targetUI == null)
        {
            return false;
        }

        if (targetUI.IsOpen)
        {
            return true;
        }

        if (targetUI.uiPanel != null)
        {
            return targetUI.uiPanel.activeSelf;
        }

        return false;
    }
}
