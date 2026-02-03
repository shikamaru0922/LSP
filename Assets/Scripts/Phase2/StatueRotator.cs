using System.Collections;
using UnityEngine;
using LSP.Gameplay;
using LSP.Gameplay.Interactions;

public class StatueRotator : MonoBehaviour, IInteractable
{
    [Header("旋转设置")]
    [Tooltip("每次交互旋转多少度？(例如 30, 45, 90)")]
    public float anglePerInteraction = 30f;

    [Tooltip("旋转方向：勾选 = 顺时针，不勾选 = 逆时针")]
    public bool isClockwise = true;

    [Tooltip("旋转一次需要多少秒？(越小越快)")]
    public float rotateDuration = 0.5f;

    [Header("音效")]
    public AudioSource audioSource;
    public AudioClip grindSound; // 石头摩擦的声音

    // 内部状态锁：防止在旋转时再次触发
    private bool _isRotating = false;

    // =========================================================
    // IInteractable 接口实现
    // =========================================================

    public bool CanInteract(PlayerInteractionController caller)
    {
        // 如果正在旋转，就不允许交互 (防止玩家狂按)
        return !_isRotating;
    }

    public void Interact(PlayerInteractionController caller)
    {
        if (_isRotating) return;

        StartCoroutine(RotateRoutine());
    }

    // =========================================================
    // 平滑旋转逻辑
    // =========================================================

    private IEnumerator RotateRoutine()
    {
        _isRotating = true;

        // 1. 计算目标角度
        // 顺时针是 +角度，逆时针是 -角度
        float angleToAdd = isClockwise ? anglePerInteraction : -anglePerInteraction;

        // 记录开始时的旋转状态
        Quaternion startRot = transform.localRotation;
        
        // 计算结束时的旋转状态 (在当前基础上绕 Y 轴转动)
        Quaternion targetRot = startRot * Quaternion.Euler(0, angleToAdd, 0);

        // 2. 播放音效
        if (audioSource && grindSound)
        {
            // 稍微随机化一点音调，听起来更自然
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(grindSound);
        }

        // 3. 开始平滑插值旋转
        float timer = 0f;
        while (timer < 1f)
        {
            timer += Time.deltaTime / rotateDuration;
            
            // 使用 Slerp 进行球面插值，保证旋转平滑
            transform.localRotation = Quaternion.Slerp(startRot, targetRot, timer);
            
            yield return null; // 等待下一帧
        }

        // 4. 强制归位 (消除浮点数误差，确保度数精准)
        transform.localRotation = targetRot;

        _isRotating = false;
        
        // 可选：转完一次后，在这里打印一下当前角度，方便你调试解谜
        // Debug.Log($"当前 Y 轴角度: {transform.localEulerAngles.y}");
    }
}