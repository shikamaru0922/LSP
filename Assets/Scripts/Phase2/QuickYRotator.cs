using UnityEngine;
using System.Collections;

public class QuickYRotator : MonoBehaviour
{
    [Header("--- 旋转设置 (Settings) ---")]
    [Tooltip("每次触发旋转的角度 (默认120度)")]
    public float targetAngle = 120f;

    [Tooltip("旋转持续时间 (秒)。设为 0 则瞬间完成")]
    public float duration = 0.5f;

    [Tooltip("是否沿世界坐标Y轴旋转？(不勾选则沿自身Y轴)")]
    public bool useWorldAxis = false;

    // 内部标记，防止在旋转过程中重复触发
    private bool isRotating = false;

    /// <summary>
    /// 【外部调用接口】: 在你的流程脚本或按钮事件中调用此方法
    /// </summary>
    public void TriggerRotation()
    {
        // 如果正在旋转且不允许打断，直接返回 (或者是选择累加，看你需求，这里默认保护)
        if (isRotating && duration > 0) return;

        if (duration <= 0)
        {
            // 瞬间旋转
            PerformInstantRotation();
        }
        else
        {
            // 平滑旋转
            StartCoroutine(RotateSmoothly());
        }
    }

    // 瞬间旋转逻辑
    private void PerformInstantRotation()
    {
        Vector3 axis = useWorldAxis ? Vector3.up : transform.up;
        transform.Rotate(axis, targetAngle, useWorldAxis ? Space.World : Space.Self);
    }

    // 平滑旋转协程
    private IEnumerator RotateSmoothly()
    {
        isRotating = true;

        Quaternion startRotation = transform.rotation;
        
        // 计算目标旋转 (根据是否是世界坐标)
        Vector3 axis = useWorldAxis ? Vector3.up : transform.up;
        Quaternion targetRotation = startRotation * Quaternion.AngleAxis(targetAngle, axis);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 使用插值进行平滑旋转
            float t = elapsed / duration;
            // 这里用了 SmoothStep 让起止更柔和，如果想要线性的改成 t 即可
            t = Mathf.SmoothStep(0f, 1f, t); 
            
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        // 确保最后角度精准
        transform.rotation = targetRotation;
        isRotating = false;
    }

    // --- 调试用 ---
    // 在编辑器脚本组件右键菜单中添加一个按钮，方便你不运行也能测试逻辑
    [ContextMenu("测试触发旋转 (Test Trigger)")]
    public void TestTrigger()
    {
        TriggerRotation();
    }
}