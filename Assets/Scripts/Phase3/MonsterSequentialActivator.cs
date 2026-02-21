using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// 按列表顺序逐个启用怪物控制脚本。
    /// 规则：同一时间只允许一个怪物控制器启用；当前怪物死亡/销毁/控制器失效后，才会尝试下一个；
    /// 且启用时必须保证怪物不在玩家视野内。
    /// </summary>
    public class MonsterSequentialActivator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerVision playerVision;

        [Tooltip("按顺序填写要激活的怪物控制器。")]
        [SerializeField]
        private List<MonsterController> monsterQueue = new List<MonsterController>();

        [Header("Options")]
        [Tooltip("启用后是否自动开始顺序激活流程。关闭后可手动调用 StartSequence。")]
        [SerializeField]
        private bool autoStartOnEnable = true;

        [Tooltip("开始序列时是否先禁用队列中全部怪物控制器（只禁用脚本，不隐藏怪物）。")]
        [SerializeField]
        private bool disableAllControllersOnStart = true;

        [Tooltip("当队列为空时是否自动查找场景中的怪物作为队列。")]
        [SerializeField]
        private bool autoFillQueueIfEmpty;

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

        [ContextMenu("Start Monster Sequence")]
        public void StartSequence()
        {
            nextMonsterIndex = 0;
            activeMonster = null;
            hasStarted = true;

            if (disableAllControllersOnStart)
            {
                DisableAllControllers();
            }
            else if (TryUseAlreadyEnabledMonster())
            {
                DisableOtherControllers(activeMonster);
                return;
            }

            TryActivateNextMonster();
        }

        private void Update()
        {
            if (!hasStarted)
            {
                return;
            }

            if (IsCurrentMonsterValidAndRunning())
            {
                return;
            }

            activeMonster = null;
            TryActivateNextMonster();
        }

        private bool IsCurrentMonsterValidAndRunning()
        {
            return activeMonster != null &&
                   activeMonster.gameObject.activeInHierarchy &&
                   activeMonster.enabled;
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

            if (!nextMonster.gameObject.activeInHierarchy)
            {
                if (verboseLog)
                {
                    Debug.Log($"[MonsterSequentialActivator] 跳过未激活对象：{nextMonster.name}", this);
                }

                nextMonsterIndex++;
                TryActivateNextMonster();
                return;
            }

            if (IsVisibleToPlayer(nextMonster))
            {
                if (verboseLog)
                {
                    Debug.Log($"[MonsterSequentialActivator] 等待启用控制器：{nextMonster.name}（仍在玩家视野内）", this);
                }

                return;
            }

            nextMonster.enabled = true;
            activeMonster = nextMonster;
            nextMonsterIndex++;
            DisableOtherControllers(activeMonster);

            if (verboseLog)
            {
                Debug.Log($"[MonsterSequentialActivator] 启用怪物控制器：{nextMonster.name}", this);
            }
        }

        private void DisableAllControllers()
        {
            for (int i = 0; i < monsterQueue.Count; i++)
            {
                MonsterController monster = monsterQueue[i];
                if (monster == null)
                {
                    continue;
                }

                monster.enabled = false;
            }
        }

        private void DisableOtherControllers(MonsterController current)
        {
            for (int i = 0; i < monsterQueue.Count; i++)
            {
                MonsterController monster = monsterQueue[i];
                if (monster == null || monster == current)
                {
                    continue;
                }

                monster.enabled = false;
            }
        }

        private bool TryUseAlreadyEnabledMonster()
        {
            for (int i = 0; i < monsterQueue.Count; i++)
            {
                MonsterController monster = monsterQueue[i];
                if (monster == null)
                {
                    continue;
                }

                if (!monster.gameObject.activeInHierarchy || !monster.enabled)
                {
                    continue;
                }

                activeMonster = monster;
                nextMonsterIndex = i + 1;

                if (verboseLog)
                {
                    Debug.Log($"[MonsterSequentialActivator] 继续使用当前已启用控制器：{monster.name}", this);
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