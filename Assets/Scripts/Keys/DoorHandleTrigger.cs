using UnityEngine;

namespace MuseumGame.Interaction
{
    /// <summary>
    /// 挂在门把手上。当玩家触碰时，通知主门脚本。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DoorHandleTrigger : MonoBehaviour
    {
        [Tooltip("拖入控制这扇门的 ElectronicDoor 脚本")]
        [SerializeField] private ElectronicDoor _targetDoor;

        [Tooltip("只有带有这个 Tag 的物体（比如 Player）才能触发")]
        [SerializeField] private string _triggerTag = "Player";

        private void OnTriggerEnter(Collider other)
        {
            if (_targetDoor == null) return;

            // 检查是不是玩家碰到了把手
            if (other.CompareTag(_triggerTag))
            {
                _targetDoor.OnHandleTriggered();
            }
        }
        
        // 可选：为了防止 Editor 里忘了设 IsTrigger，自动强制设一下
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }
}