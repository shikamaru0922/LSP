using UnityEngine;

public class BlinkObject : MonoBehaviour
{
    [Header("层级设置")]
    public string blindLayerName = "BlindVis"; 
    
    private int originalLayer;
    private int blindLayerId;

    // 【新增】只有当这个变量为 true 时，这个物体才允许在眨眼时显示
    private bool isCurrentTarget = false;

    void Start()
    {
        originalLayer = gameObject.layer;
        blindLayerId = LayerMask.NameToLayer(blindLayerName);
    }

    // 【新增】由 Manager 调用，告诉这个物体：“轮到你表现了”
    public void SetAsCurrentTarget(bool active)
    {
        isCurrentTarget = active;
        
        // 如果被取消资格（active=false），强制恢复原状，防止它卡在透视层
        if (!active)
        {
            SetLayerRecursively(gameObject, originalLayer);
        }
    }

    // 外部调用（比如你的眨眼控制器）
    public void SetBlindMode(bool isBlind)
    {
        // 【关键逻辑修改】
        // 如果闭眼了 (isBlind=true)，但轮不到我 (isCurrentTarget=false)，那就直接退出，不准变身
        if (isBlind && !isCurrentTarget) return;

        if (isBlind)
        {
            // 闭眼：切换到透视层
            SetLayerRecursively(gameObject, blindLayerId);
        }
        else
        {
            // 睁眼：恢复到原本的层
            SetLayerRecursively(gameObject, originalLayer);
        }
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return; // 安全检查
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}