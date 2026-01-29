using UnityEngine;
using System.Collections.Generic; // 必须引用这个来用 List

namespace LSP.Gameplay
{
    [RequireComponent(typeof(Collider))] // 如果你单纯把它当管理器用，这个其实可以去掉，但留着也不影响
    public class WorldAbnormalTrigger : MonoBehaviour
    {
        [Header("全局设置")]
        [SerializeField]
        private GameManager gameManager;

        [Header("场景物体管理 (关键部分)")]
        [Tooltip("把所有需要显示的怪异物体（或者它们的父物体）拖到这里")]
        [SerializeField] 
        private List<GameObject> spookyObjectsToActivate;

        [Header("触发器设置 (可选)")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool triggerOnce = true;
        private bool hasTriggered;

        // ========================================================
        // 1. 这是一个公共方法 (Public Method)
        // 专门给眼镜脚本 (GlassesPickup) 的 UnityEvent 调用的
        // ========================================================
        public void EnableAbnormalVisuals()
        {
            // 1. 遍历列表，把所有东西设为 True
            foreach (var obj in spookyObjectsToActivate)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }

            // 2. 如果还需要通知全局 GameManager，也可以在这里通知
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.SetWorldAbnormalState(true);
            }

            Debug.Log($"<color=red>世界异化已开启！激活了 {spookyObjectsToActivate.Count} 个物体。</color>");
        }

        // ========================================================
        // 2. 原有的 Trigger 逻辑 (保留，防止你以后想做踩地板触发)
        // ========================================================
        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && hasTriggered) return;
            if (!other.CompareTag(playerTag)) return;

            // 这里也可以直接复用上面的逻辑
            EnableAbnormalVisuals();
            
            hasTriggered = true;
        }
    }
}