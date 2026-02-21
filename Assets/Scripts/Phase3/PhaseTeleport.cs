using UnityEngine;
using UnityEngine.Events; // 引入事件命名空间
using System.Collections;

namespace LSP.Gameplay
{
    /// <summary>
    /// 阶段传送器：先触发眨眼黑屏，等待视觉遮挡后，再进行物理坐标传送。
    /// </summary>
    public class PhaseTeleport : MonoBehaviour
    {
        [Header("===== 传送设置 =====")]
        [Tooltip("玩家的 Transform (拖入 Player)")]
        public Transform playerTransform;
        
        [Tooltip("要传送到哪个位置？(在1F大厅放一个空物体作为定位点，拖到这里)")]
        public Transform targetDestination;

        [Header("===== 节奏控制 =====")]
        [Tooltip("黑屏掩护时间 (秒)。触发眨眼后，等几秒再把玩家移过去？建议0.2-0.5秒，确保屏幕已黑")]
        public float delayBeforeTeleport = 0.3f;

        [Header("===== 表现事件 =====")]
        [Tooltip("在这里配置【触发眨眼】的逻辑")]
        public UnityEvent onBlinkTriggered;

        // =========================================================
        // 【供 Event System 调用的主入口】
        // =========================================================
        public void ExecuteTeleport()
        {
            if (playerTransform == null || targetDestination == null)
            {
                Debug.LogError("【传送失败】未指定玩家物体或目标位置！");
                return;
            }

            // 1. 瞬间触发眨眼表现 (调用外部统一下达的黑屏、音效等)
            onBlinkTriggered?.Invoke();
            Debug.Log("【PhaseTeleport】已触发眨眼，准备空间跳跃...");

            // 2. 开启协程，在黑暗中偷偷移动玩家
            StartCoroutine(TeleportRoutine());
        }

        private IEnumerator TeleportRoutine()
        {
            // 等待眨眼彻底黑屏
            yield return new WaitForSeconds(delayBeforeTeleport);

            // 3. 执行空间跳跃
            // 【坑点防范】如果玩家身上有 CharacterController，直接改 Position 是无效的，会被物理引擎拉回去。
            // 必须先关掉它，移动完再打开。
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            
            if (cc != null)
            {
                cc.enabled = false; 
                playerTransform.position = targetDestination.position;
                playerTransform.rotation = targetDestination.rotation; // 顺便把玩家面朝向也转正
                cc.enabled = true;  
            }
            else
            {
                // 如果没有 CharacterController，直接移动即可
                playerTransform.position = targetDestination.position;
                playerTransform.rotation = targetDestination.rotation;
            }

            Debug.Log("【PhaseTeleport】玩家已传送到 1F 大厅！等待睁眼。");
        }
    }
}