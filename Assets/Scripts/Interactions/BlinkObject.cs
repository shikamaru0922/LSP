using UnityEngine;

public class BlinkObject : MonoBehaviour
{
    [Header("层级设置")]
    // 1. 指定这一层的名字 (一定要和 Unity 右上角 Layers 里的一模一样)
    public string blindLayerName = "BlindVis"; 
    
    // 内部变量：记住物体原本是在哪一层的
    private int originalLayer;
    private int blindLayerId;

    void Start()
    {
        // 记录它原本的层 (比如 Default 或 Enemy)
        originalLayer = gameObject.layer;
        // 获取透视层的数字 ID
        blindLayerId = LayerMask.NameToLayer(blindLayerName);
        
        // 注册事件 (或者在 Update 里监听，看你的架构)
        // 这里假设有一个全局事件中心，或者你可以直接让 Manager 调用这个脚本
    }

    // 提供给外部调用的方法
    public void SetBlindMode(bool isBlind)
    {
        if (isBlind)
        {
            // 闭眼：切换到透视层
            // 注意：如果物体有子物体（比如手里的剑），需要递归修改，下面有辅助函数
            SetLayerRecursively(gameObject, blindLayerId);
        }
        else
        {
            // 睁眼：恢复到原本的层
            SetLayerRecursively(gameObject, originalLayer);
        }
    }

    // 辅助函数：把该物体和所有子物体的 Layer 全都改了
    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}