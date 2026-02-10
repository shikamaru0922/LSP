using UnityEngine;
using System.Collections;

public class XAxisRotator : MonoBehaviour
{
    // 定义旋转节奏的枚举
    public enum EaseType
    {
        Linear,         // 匀速转动
        EaseIn,         // 先慢后快 (适合沉重的机关启动)
        EaseOut,        // 先快后慢 (适合弹开、迅速打开然后减速停止)
        EaseInOut       // 慢->快->慢 (最平滑、最自然的机械转动)
    }

    [Header("===== 旋转配置 =====")]
    [Tooltip("每次触发，绕 X 轴旋转多少度？(填负数就是反着转)")]
    public float rotationAngleX = 90f;

    [Tooltip("转完这一次需要多少秒？")]
    public float duration = 1.0f;

    [Tooltip("旋转的节奏感 (推荐使用 EaseInOut 或 EaseOut)")]
    public EaseType easeType = EaseType.EaseInOut;

    [Header("===== 音效 (可选) =====")]
    public AudioSource audioSource;
    public AudioClip rotateSound;

    // 内部状态锁，防止在旋转过程中被玩家狂按重复触发
    private bool _isRotating = false;

    // =========================================================
    // 【傻瓜式调用入口】
    // 你的 Event System 或者互动脚本，直接调用这个方法就行！
    // =========================================================
    public void TriggerRotation()
    {
        // 如果正在转，就无视这次调用，防止动画鬼畜
        if (_isRotating) return; 

        StartCoroutine(RotateRoutine());
    }

    // =========================================================
    // 核心旋转逻辑
    // =========================================================
    private IEnumerator RotateRoutine()
    {
        _isRotating = true;
        
        // 如果配了声音就播放
        if (audioSource != null && rotateSound != null)
        {
            audioSource.PlayOneShot(rotateSound);
        }

        float timer = 0f;

        // 记录起始角度
        Quaternion startRot = transform.localRotation;
        // 计算目标角度 (在当前基础上，X轴叠加 rotationAngleX 度)
        Quaternion targetRot = startRot * Quaternion.Euler(rotationAngleX, 0, 0);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            
            // t 是 0 到 1 之间的时间比例
            float t = Mathf.Clamp01(timer / duration);

            // 根据你选择的节奏，把纯线性的 t 转换成带节奏的 easeT
            float easeT = EvaluateEase(t);

            // 进行球面插值平滑旋转
            transform.localRotation = Quaternion.Slerp(startRot, targetRot, easeT);
            
            // 等待下一帧
            yield return null;
        }

        // 循环结束后，强行对齐目标角度，消除浮点数误差
        transform.localRotation = targetRot;
        _isRotating = false;
    }

    // =========================================================
    // 数学魔法：将匀速时间转换为带节奏的时间
    // =========================================================
    private float EvaluateEase(float t)
    {
        switch (easeType)
        {
            case EaseType.EaseIn: 
                // 先慢后快：平方曲线
                return t * t; 
            case EaseType.EaseOut: 
                // 先快后慢：反向平方曲线
                return 1f - (1f - t) * (1f - t); 
            case EaseType.EaseInOut: 
                // 慢-快-慢：平滑阶跃曲线 (Unity自带的SmoothStep)
                return Mathf.SmoothStep(0f, 1f, t); 
            case EaseType.Linear: 
            default: 
                // 匀速：直接返回原值
                return t; 
        }
    }
}