using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// 按列表顺序逐个激活怪物。
    /// 规则：同一时间只允许一个怪物激活；当前怪物失活/销毁后才会尝试激活下一个；
    /// 且激活时必须保证怪物不在玩家视野内。
    /// </summary>
    public class MonsterSequentialActivator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerVision playerVision;

        [Tooltip("按顺序填写要激活的怪物。")]
        [SerializeField]
        private List<MonsterController> monsterQueue = new List<MonsterController>();

        [Header("Options")]
        [Tooltip("启用后是否自动开始顺序激活流程。关闭后可手动调用 StartSequence。")]
        [SerializeField]
        private bool autoStartOnEnable = true;

        [Tooltip("是否在启动序列时隐藏队列中的所有怪物。默认关闭：只启动脚本，不隐藏怪物。")]
        [SerializeField]
        private bool deactivateAllOnStart;

        [Tooltip("当队列为空时是否自动查找场景中的怪物作为队列。")]
        [SerializeField]
        private bool autoFillQueueIfEmpty = false;

        [Tooltip("是否输出调试日志。")]
        [SerializeField]
        private bool verboseLog;

        private int nextMonsterIndex;
        private MonsterController activeMonster;
        private bool hasStarted;

        private void Awake()
        {
            if (playerVision == null)
            {
                playerVision = FindObjectOfType<PlayerVision>();
            }

            if (autoFillQueueIfEmpty && monsterQueue.Count == 0)
            {
                MonsterController[] foundMonsters = FindObjectsOfType<MonsterController>(true);
                monsterQueue.AddRange(foundMonsters);
            }
        }

        private void Start()
        {
            if (autoStartOnEnable)
            {
                StartSequence();
            }
        }

        private void OnDisable()
        {
            activeMonster = null;
        }

        /// <summary>
        /// 开始（或重开）怪物顺序激活流程。
        /// </summary>
        [ContextMenu("Start Monster Sequence")]
        public void StartSequence()
        {
            nextMonsterIndex = 0;
            activeMonster = null;
            hasStarted = true;

            if (deactivateAllOnStart)
            {
                DeactivateAllQueuedMonsters();
            }
            else
            {
                if (TryUseAlreadyActiveMonster())
                {
                    return;
                }
            }

            TryActivateNextMonster();
        }

        private void Update()
        {
            if (!hasStarted)
            {
                return;
            }

            if (activeMonster != null && activeMonster.gameObject.activeInHierarchy)
            {
                return;
            }

            activeMonster = null;
            TryActivateNextMonster();
        }

        private void DeactivateAllQueuedMonsters()
        {
            for (int i = 0; i < monsterQueue.Count; i++)
            {
                MonsterController monster = monsterQueue[i];
                if (monster == null)
                {
                    continue;
                }

                monster.gameObject.SetActive(false);
            }
        }

        private void TryActivateNextMonster()
        {
            if (nextMonsterIndex >= monsterQueue.Count)
            {
                return;
            }

            MonsterController nextMonster = monsterQueue[nextMonsterIndex];
            if (nextMonster == null)
            {
                nextMonsterIndex++;
                TryActivateNextMonster();
                return;
            }

            if (IsVisibleToPlayer(nextMonster))
            {
                if (verboseLog)
                {
                    Debug.Log($"[MonsterSequentialActivator] 等待激活：{nextMonster.name}（仍在玩家视野内）", this);
                }

                return;
            }

            nextMonster.gameObject.SetActive(true);
            activeMonster = nextMonster;
            nextMonsterIndex++;

            if (verboseLog)
            {
                Debug.Log($"[MonsterSequentialActivator] 激活怪物：{nextMonster.name}", this);
            }
        }

        private bool TryUseAlreadyActiveMonster()
        {
            for (int i = 0; i < monsterQueue.Count; i++)
            {
                MonsterController monster = monsterQueue[i];
                if (monster == null)
                {
                    continue;
                }

                if (!monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                activeMonster = monster;
                nextMonsterIndex = i + 1;

                if (verboseLog)
                {
                    Debug.Log($"[MonsterSequentialActivator] 继续使用当前已激活怪物：{monster.name}", this);
                }

                return true;
            }

            return false;
        }

        private bool IsVisibleToPlayer(MonsterController monster)
        {
            if (playerVision == null || monster == null)
            {
                return false;
            }

            Collider monsterCollider = monster.GetComponent<Collider>();
            return monsterCollider != null && playerVision.CanSee(monsterCollider);
        }

        public void SetQueue(List<MonsterController> queue)
        {
            monsterQueue = queue ?? new List<MonsterController>();
            nextMonsterIndex = 0;
            activeMonster = null;
            hasStarted = false;
        }

        public void TriggerTryActivateNext()
        {
            if (activeMonster == null)
            {
                TryActivateNextMonster();
            }
        }
    }
}