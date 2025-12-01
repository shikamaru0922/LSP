using UnityEngine;
using UnityEngine.Events; // 必须引入这个命名空间

public class TriggerEventZone : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("只有带有这个Tag的物体进入才会计数")]
    public string targetTag = "Player";

    [Tooltip("玩家需要进入多少次才能触发事件？\n1 = 每次进入都触发\n3 = 第3次进入时触发")]
    [Min(1)] // 限制最小值为1，防止设置成0或负数
    public int requiredEnterTimes = 1;

    [Header("调试 (只读)")]
    [Tooltip("当前已经进入了多少次")]
    [SerializeField] private int currentEnterTimes = 0;

    [Header("事件")]
    [Tooltip("当达到指定次数时触发")]
    public UnityEvent onCountReached;

    // 可选：如果你希望达成次数后，每次进入都能触发，可以用这个事件
    // public UnityEvent onEveryEnterAfter; 

    private void OnTriggerEnter(Collider other)
    {
        // 1. 检查Tag
        if (other.CompareTag(targetTag))
        {
            // 2. 增加计数
            currentEnterTimes++;
            Debug.Log($"[{gameObject.name}] 玩家进入次数: {currentEnterTimes} / {requiredEnterTimes}");

            // 3. 判断是否达到指定次数
            // 使用 == 表示“仅在第 N 次”触发。
            // 如果你想“第 N 次及之后每次”都触发，请把 == 改为 >=
            if (currentEnterTimes == requiredEnterTimes)
            {
                Debug.Log($"[{gameObject.name}] 达成条件！触发事件。");
                onCountReached?.Invoke();
            }
        }
    }

    // 可选功能：提供一个公共方法，允许其他脚本重置计数（比如玩家失败了要重来）
    public void ResetCounter()
    {
        currentEnterTimes = 0;
        Debug.Log($"[{gameObject.name}] 计数器已重置。");
    }
}