using UnityEngine;
using LSP.Gameplay.Interactions;
using MuseumGame.Interaction; // 引用 ElectronicDoor

namespace LSP.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class DoorHandle : MonoBehaviour, IInteractable
    {
        [Tooltip("拖入控制这扇门的 ElectronicDoor 脚本")]
        [SerializeField] private ElectronicDoor _targetDoor;

        [Tooltip("如果没有钥匙，是否允许交互并播放锁住的声音？")]
        [SerializeField] private bool _allowInteractWhenLocked = true;

        // === 接口实现 ===

        public bool CanInteract(PlayerInteractionController controller)
        {
            if (_targetDoor == null)
            {
                return false;
            }

            // 门已打开后，不再显示可交互高亮/提示。
            if (_targetDoor.IsOpen)
            {
                return false;
            }

            // 可选：锁住时是否允许继续交互（比如播放“锁住”音效）。
            if (_targetDoor.IsLocked && !_allowInteractWhenLocked)
            {
                return false;
            }

            return true;
        }

        public void Interact(PlayerInteractionController controller)
        {
            if (_targetDoor != null)
            {
                // 调用门原本的逻辑 (检查钥匙、旋转等)
                _targetDoor.OnHandleTriggered();
            }
        }
    }
}
