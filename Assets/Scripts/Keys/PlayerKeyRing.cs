using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

namespace MuseumGame.Interaction
{
    public class PlayerKeyRing : MonoBehaviour
    {
        // 单例模式，方便门脚本随时找到玩家的钥匙扣
        public static PlayerKeyRing Instance { get; private set; }

        [Header("Debug")]
        [Tooltip("当前拥有的钥匙列表")]
        [SerializeField] private List<KeyID> _myKeys = new List<KeyID>();

        [Header("Events")]
        [Tooltip("当两把钥匙合成成功时触发 (比如播放合成音效)")]
        public UnityEvent OnKeyCombined;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// 拾取钥匙时调用此方法
        /// </summary>
        public void AddKey(KeyID key)
        {
            if (_myKeys.Contains(key)) return; // 已经有了就不重复加

            _myKeys.Add(key);
            Debug.Log($"捡到了钥匙: {key}");

            // === 核心需求：检查是否需要合成子母钥匙 ===
            CheckAndCombineKeys();
        }

        /// <summary>
        /// 检查玩家是否有特定的钥匙
        /// </summary>
        public bool HasKey(KeyID key)
        {
            return _myKeys.Contains(key);
        }

        /// <summary>
        /// 合成逻辑：子母钥匙合体
        /// </summary>
        private void CheckAndCombineKeys()
        {
            // 如果同时拥有 PartA 和 PartB，并且还没有合成过 Final
            bool hasA = _myKeys.Contains(KeyID.Key_4_PartA);
            bool hasB = _myKeys.Contains(KeyID.Key_4_PartB);
            bool hasFinal = _myKeys.Contains(KeyID.Key_4_Final);

            if (hasA && hasB && !hasFinal)
            {
                // 执行合成
                _myKeys.Remove(KeyID.Key_4_PartA);
                _myKeys.Remove(KeyID.Key_4_PartB);
                _myKeys.Add(KeyID.Key_4_Final);

                Debug.Log(">>> 子母钥匙发生感应... 合成完毕！获得了 Key_4_Final <<<");
                
                // 触发合成音效或UI提示
                OnKeyCombined?.Invoke();
            }
        }
    }
}