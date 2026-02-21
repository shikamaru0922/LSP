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

        [Tooltip("可选：怪物对象身上的脚本。若命中子 Collider 也能识别并销毁整只怪物。")]
        [SerializeField] private bool destroyMonsterControllerOwner = true;

        private Collider zoneCollider;

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
            if (!zoneCollider.isTrigger)
            {
                Debug.LogWarning("【WallDeathZone】当前 Collider 不是 Trigger。若希望使用 OnTriggerEnter，请勾选 Is Trigger。", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleContact(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleContact(collision.collider);
        }

        private void HandleContact(Collider other)
        {
            if (other == null)
            {
                return;
            }

            // 1. 玩家
            if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
            {
                Debug.Log("【WallDeathZone】玩家被墙壁挤死了！触发死亡事件。", other);
                onPlayerCrushed?.Invoke();
                return;
            }

            // 2. 按标签清场（支持标签挂在父物体）
            if (other.CompareTag(targetTag) || other.transform.root.CompareTag(targetTag))
            {
                GameObject victim = other.CompareTag(targetTag) ? other.gameObject : other.transform.root.gameObject;
                Debug.Log($"【WallDeathZone】墙壁碾碎目标: {victim.name}", victim);
                Destroy(victim);
                return;
            }

            // 3. 可选：只要碰到 MonsterController 任意子节点就销毁整只怪物
            if (destroyMonsterControllerOwner && other.GetComponentInParent<MonsterController>() is MonsterController monster)
            {
                Debug.Log($"【WallDeathZone】检测到 MonsterController，销毁怪物: {monster.name}", monster);
                Destroy(monster.gameObject);
            }
        }
    }
}
