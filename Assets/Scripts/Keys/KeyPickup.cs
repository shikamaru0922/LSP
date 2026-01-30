using UnityEngine;

namespace MuseumGame.Interaction
{
    public class KeyPickup : MonoBehaviour
    {
        [Tooltip("这把钥匙是谁？")]
        [SerializeField] private KeyID _keyType;

        [Tooltip("捡起后的音效")]
        [SerializeField] private AudioClip _pickupSfx;

        [Tooltip("玩家的 Tag")]
        [SerializeField] private string _playerTag = "Player";

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(_playerTag))
            {
                // 1. 加到钥匙扣
                if (PlayerKeyRing.Instance != null)
                {
                    PlayerKeyRing.Instance.AddKey(_keyType);
                }

                // 2. 播放声音 (简单做法：在位置处生成声音)
                if (_pickupSfx != null)
                {
                    AudioSource.PlayClipAtPoint(_pickupSfx, transform.position);
                }

                // 3. 销毁地面物体
                Destroy(gameObject);
            }
        }
    }
}