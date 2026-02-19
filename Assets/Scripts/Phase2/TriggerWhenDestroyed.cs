using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// 监视器：当目标物体消失（被销毁）时，触发 SetFlag 逻辑
    /// </summary>
    public class TriggerWhenDestroyed : MonoBehaviour
    {
        [Header("===== 监视设置 =====")]
        [Tooltip("把那把【钥匙】拖到这里")]
        public GameObject targetToWatch;

        [Tooltip("把挂在这个空物体上的 InteractableSetFlag 脚本拖到这里")]
        public InteractableSetFlag flagScript;

        [Header("===== 调试 =====")]
        [Tooltip("是否已经开始监视？(自动勾选)")]
        [SerializeField] private bool _isWatching = false;

        private void Start()
        {
            if (targetToWatch != null && flagScript != null)
            {
                _isWatching = true;
            }
            else
            {
                Debug.LogWarning("【TriggerWhenDestroyed】未分配 Target 或 FlagScript，监视器无法启动。");
            }
        }

        private void Update()
        {
            // 如果正在监视，但目标突然变成了 null (说明它被 Destroy 了)
            if (_isWatching && targetToWatch == null)
            {
                TriggerNow();
            }
        }

        private void TriggerNow()
        {
            Debug.Log("【监视器】发现钥匙已销毁，正在执行 SetFlag...");

            // 停止监视，防止每一帧都触发
            _isWatching = false;

            // 调用隔壁脚本的 ExecuteLogic
            if (flagScript != null)
            {
                // 传入参数只是为了Debug Log显示来源，填什么都行
                flagScript.ExecuteLogic("钥匙销毁触发");
            }

            // 任务完成，把自己也销毁吧，节省性能
            Destroy(gameObject);
        }
    }
}