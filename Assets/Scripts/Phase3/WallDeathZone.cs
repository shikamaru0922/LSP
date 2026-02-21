using UnityEngine;
using UnityEngine.Events; // 引入事件系统

namespace LSP.Gameplay
{
    /// <summary>
    /// 墙壁死区：挂在四面移动的墙上。负责冷酷清场和击杀玩家。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WallDeathZone : MonoBehaviour
    {
        [Header("===== 玩家死亡事件 =====")]
        [Tooltip("当墙壁碰到玩家时触发 (在这里连你的玩家死亡逻辑)")]
        public UnityEvent onPlayerCrushed;

        [Header("===== 清场设置 =====")]
        [Tooltip("被墙壁碾碎的普通雕像的 Tag")]
        public string targetTag = "Statue"; // 或者是 "Enemy"，看你的项目设置

        private void OnTriggerEnter(Collider other)
        {
            // 1. 碰到玩家 -> 触发你配好的 Event System 死亡事件
            if (other.CompareTag("Player"))
            {
                Debug.Log("【WallDeathZone】玩家被墙壁挤死了！触发死亡事件。");
                onPlayerCrushed?.Invoke();
            }
            // 2. 碰到雕像/怪物 -> 方案A：冷酷清场，直接销毁！
            else if (other.CompareTag(targetTag))
            {
                // Debug.Log($"【WallDeathZone】墙壁碾碎了边缘的雕像: {other.name}");
                Destroy(other.gameObject);
            }
        }
    }
}