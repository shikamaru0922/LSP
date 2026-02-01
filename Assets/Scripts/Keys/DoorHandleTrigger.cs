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
            // 如果门已经开了，就不需要再交互了 (或者你可以允许关门，看需求)
            if (_targetDoor != null && _targetDoor.IsOpen) 
            {
                return true; // 允许关门
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