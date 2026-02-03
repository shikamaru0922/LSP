using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ElevatorDoors : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("左边的门板")]
    [SerializeField] private Transform leftDoor;
    
    [Tooltip("右边的门板")]
    [SerializeField] private Transform rightDoor;

    [Header("运动设置")]
    [Tooltip("门向两侧滑动的距离 (米)")]
    [SerializeField] private float slideDistance = 1.2f;

    [Tooltip("开门速度")]
    [SerializeField] private float openSpeed = 2.0f;

    [Header("音频")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound; // 电梯“叮”的一声或者机械声
    [SerializeField] private AudioClip closeSound;

    [Header("状态")]
    [Tooltip("游戏开始时门是否是开着的？")]
    [SerializeField] private bool startOpen = false;

    [Header("事件 (可选)")]
    [Tooltip("当电梯门完全打开后触发 (比如用来加载下一关)")]
    public UnityEvent onFullyOpened;

    // 内部记录原始位置
    private Vector3 _leftClosedPos;
    private Vector3 _rightClosedPos;
    private bool _isOpen = false;
    private Coroutine _moveCoroutine;

    private void Start()
    {
        if (leftDoor == null || rightDoor == null)
        {
            Debug.LogError("【电梯错误】请在 Inspector 里拖入左右两个门板！");
            return;
        }

        // 1. 记录关门时的原始位置 (LocalPosition)
        _leftClosedPos = leftDoor.localPosition;
        _rightClosedPos = rightDoor.localPosition;

        // 2. 初始化状态
        if (startOpen)
        {
            _isOpen = true;
            // 直接设置到开门位置
            SetDoorPositions(1.0f); 
        }
        else
        {
            _isOpen = false;
            SetDoorPositions(0.0f);
        }
    }

    // =========================================================
    //  【核心方法】供外部事件调用 (比如 UnityEvent)
    // =========================================================
    public void OpenElevator()
    {
        if (_isOpen) return; // 已经是开着的，就不管了

        Debug.Log("【电梯】正在开门...");
        StartCoroutine(MoveDoorsProcess(true));
    }

    public void CloseElevator()
    {
        if (!_isOpen) return; // 已经是关着的

        Debug.Log("【电梯】正在关门...");
        StartCoroutine(MoveDoorsProcess(false));
    }

    // =========================================================
    //  平滑移动逻辑
    // =========================================================
    private IEnumerator MoveDoorsProcess(bool open)
    {
        // 如果正在动，先停下
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        
        _isOpen = open;

        // 播放音效
        AudioClip clip = open ? openSound : closeSound;
        if (audioSource && clip) audioSource.PlayOneShot(clip);

        float timer = 0f;
        // 计算需要的总时间 = 距离 / 速度
        float duration = slideDistance / openSpeed; 

        // 获取当前进度 (0=关, 1=开)
        // 这里的计算稍微简化，假设每次都从头开始平滑过渡
        float startFactor = open ? 0f : 1f;
        float endFactor = open ? 1f : 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            
            // 使用 SmoothStep 让动作更顺滑 (起步慢，中间快，结束慢)
            float smoothT = Mathf.SmoothStep(startFactor, endFactor, t);
            
            SetDoorPositions(smoothT);
            
            yield return null;
        }

        // 强制设置到终点，消除误差
        SetDoorPositions(endFactor);

        // 如果是开门，触发完成事件
        if (open)
        {
            onFullyOpened?.Invoke();
        }
    }

    // 设置门的位置 (factor: 0 = 关闭状态, 1 = 完全打开状态)
    private void SetDoorPositions(float factor)
    {
        // 左门向负方向移动 (-X)
        // 右门向正方向移动 (+X)
        // 注意：这里假设你的门轴向是 X 轴。如果是 Z 轴，请把 Vector3.right 改为 Vector3.forward
        
        Vector3 leftTarget = _leftClosedPos - (Vector3.forward * slideDistance * factor);
        Vector3 rightTarget = _rightClosedPos + (Vector3.forward * slideDistance * factor);

        leftDoor.localPosition = leftTarget;
        rightDoor.localPosition = rightTarget;
    }
}