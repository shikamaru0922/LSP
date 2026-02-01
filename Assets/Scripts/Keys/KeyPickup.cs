using UnityEngine;
using LSP.Gameplay.Interactions; // 引用你的接口命名空间
using MuseumGame.Interaction;    // 引用 KeyID 和 PlayerKeyRing 的命名空间

namespace LSP.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class KeyPickup : MonoBehaviour, IInteractable
    {
        [Header("Key Settings")]
        [Tooltip("这把钥匙的类型 ID")]
        [SerializeField] private KeyID _keyType;

        [Tooltip("捡起时的音效")]
        [SerializeField] private AudioClip _pickupSfx;

        // === 接口实现 ===

        /// <summary>
        /// 告诉控制器：这把钥匙能不能被交互？
        /// </summary>
        public bool CanInteract(PlayerInteractionController caller)
        {
            // 只要钥匙还在场景里，就可以捡
            return true;
        }

        /// <summary>
        /// 当按下 F 键时执行
        /// </summary>
        public void Interact(PlayerInteractionController caller)
        {
            // 1. 存入钥匙扣 (PlayerKeyRing)
            if (PlayerKeyRing.Instance != null)
            {
                PlayerKeyRing.Instance.AddKey(_keyType);
            }
            else
            {
                Debug.LogError("场景里找不到 PlayerKeyRing 单例！无法存钥匙。请检查 Player 身上是否挂了 PlayerKeyRing。");
            }

            // 2. 播放捡起音效
            if (_pickupSfx != null)
            {
                AudioSource.PlayClipAtPoint(_pickupSfx, transform.position);
            }

            // 3. 销毁物体 (捡起来了)
            Destroy(gameObject);
            
            Debug.Log($"[KeyPickup] 玩家捡起了钥匙: {_keyType}");
        }
    }
}