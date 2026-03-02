using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events; // 【新增】引入事件系统命名空间

namespace LSP.Gameplay
{
    /// <summary>
    /// 按列表顺序逐个启用怪物控制脚本。
    /// 规则：同一时间只允许一个怪物控制器启用；当前怪物死亡/销毁/控制器失效后，才会尝试下一个；
    /// 且启用时必须保证怪物不在玩家视野内。
    /// </summary>
    public class MonsterSequentialActivator : MonoBehaviour
    {
        private struct AnimatorPlaybackSnapshot
        {
            public Animator animator;
            public bool wasEnabled;
            public float speed;
            public bool pausedByActivator;
        }

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

        // =========================================================
        // 【新增】事件与状态追踪
        // =========================================================
        [Header("Events (流程控制)")]
        [Tooltip("当场上存活的怪物数量小于 5 时触发 (仅触发一次)")]
        public UnityEvent onMonstersLessThanFive;

        [Tooltip("当场上所有怪物都被清空 (数量 == 0) 时触发 (仅触发一次)")]
        public UnityEvent onAllMonstersCleared;

        private bool _hasTriggeredLessThanFive = false;
        private bool _hasTriggeredAllCleared = false;


        private int nextMonsterIndex;
        private MonsterController activeMonster;
        private bool hasStarted;
        private readonly Dictionary<MonsterController, AnimatorPlaybackSnapshot> animatorSnapshots = new Dictionary<MonsterController, AnimatorPlaybackSnapshot>();

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

            // 【新增】重置事件触发状态，方便重复游玩
            _hasTriggeredLessThanFive = false;
            _hasTriggeredAllCleared = false;

            if (disableAllControllersOnStart)
            {
                DisableAllControllers();
            }
            else if (TryUseAlreadyEnabledMonster())
            {
                DisableOtherControllers(activeMonster);
                EnforceAnimatorPlaybackState();
                return;
            }

            TryActivateNextMonster();
            EnforceAnimatorPlaybackState();
        }

        private void Update()
        {
            if (!hasStarted)
            {
                return;
            }

            // 【新增】每帧检查剩余怪物数量，触发对应事件
            CheckRemainingMonsters();

            if (IsCurrentMonsterValidAndRunning())
            {
                EnforceAnimatorPlaybackState();
                return;
            }

            activeMonster = null;
            TryActivateNextMonster();
            EnforceAnimatorPlaybackState();
        }

        // =========================================================
        // 【新增方法】检查剩余怪物数量并触发事件
        // =========================================================
        private void CheckRemainingMonsters()
        {
            // 如果两个事件都已经触发过了，就不需要再数了，节省性能
            if (_hasTriggeredAllCleared) return;

            int aliveCount = 0;
            
            // 遍历列表，统计还没有被 Destroy (不为 null) 的怪物数量
            for (int i = 0; i < monsterQueue.Count; i++)
            {
                if (monsterQueue[i] != null)
                {
                    aliveCount++;
                }
            }

            // 检查：小于 5 的时候触发 (仅触发一次)
            if (aliveCount < 5 && !_hasTriggeredLessThanFive)
            {
                _hasTriggeredLessThanFive = true;
                if (verboseLog) Debug.Log("【MonsterSequentialActivator】剩余怪物小于5，触发高潮事件！", this);
                onMonstersLessThanFive?.Invoke();
            }

            // 检查：等于 0 的时候触发 (仅触发一次)
            if (aliveCount == 0 && !_hasTriggeredAllCleared)
            {
                _hasTriggeredAllCleared = true;
                if (verboseLog) Debug.Log("【MonsterSequentialActivator】所有怪物已清除，触发通关事件！", this);
                onAllMonstersCleared?.Invoke();
            }
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
            ResumeMonsterAnimation(activeMonster);

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
                PauseMonsterAnimation(monster);
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
                PauseMonsterAnimation(monster);
            }

            ResumeMonsterAnimation(current);
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
                ResumeMonsterAnimation(activeMonster);

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
            
            // 【新增】重设队列时也重置事件触发器
            _hasTriggeredLessThanFive = false;
            _hasTriggeredAllCleared = false;
        }

        public void TriggerTryActivateNext()
        {
            if (activeMonster == null)
            {
                TryActivateNextMonster();
                EnforceAnimatorPlaybackState();
            }
        }

        private void EnforceAnimatorPlaybackState()
        {
            for (int i = 0; i < monsterQueue.Count; i++)
            {
                MonsterController monster = monsterQueue[i];
                if (monster == null)
                {
                    continue;
                }

                if (ShouldPlayAnimation(monster))
                {
                    ResumeMonsterAnimation(monster);
                }
                else
                {
                    PauseMonsterAnimation(monster);
                }
            }
        }

        private bool ShouldPlayAnimation(MonsterController monster)
        {
            if (monster == null || activeMonster != monster)
            {
                return false;
            }

            if (!monster.gameObject.activeInHierarchy || !monster.enabled || monster.IsDead)
            {
                return false;
            }

            return true;
        }

        private void PauseMonsterAnimation(MonsterController monster)
        {
            Animator animator = GetAnimator(monster);
            if (animator == null)
            {
                return;
            }

            CacheAnimatorSnapshot(monster, animator);
            if (animatorSnapshots.TryGetValue(monster, out AnimatorPlaybackSnapshot snapshot))
            {
                if (!snapshot.pausedByActivator)
                {
                    snapshot.wasEnabled = animator.enabled;
                    snapshot.speed = animator.speed;
                    snapshot.pausedByActivator = true;
                    animatorSnapshots[monster] = snapshot;
                }
            }

            animator.speed = 0f;
            animator.enabled = false;
        }

        private void ResumeMonsterAnimation(MonsterController monster)
        {
            Animator animator = GetAnimator(monster);
            if (animator == null)
            {
                return;
            }

            CacheAnimatorSnapshot(monster, animator);

            if (animatorSnapshots.TryGetValue(monster, out AnimatorPlaybackSnapshot snapshot))
            {
                if (!snapshot.pausedByActivator)
                {
                    return;
                }

                animator.enabled = snapshot.wasEnabled;
                animator.speed = snapshot.speed;
                snapshot.pausedByActivator = false;
                animatorSnapshots[monster] = snapshot;
            }
            else
            {
                animator.enabled = true;
                animator.speed = 1f;
            }
        }

        private void CacheAnimatorSnapshot(MonsterController monster, Animator animator)
        {
            if (monster == null || animator == null || animatorSnapshots.ContainsKey(monster))
            {
                return;
            }

            AnimatorPlaybackSnapshot snapshot = new AnimatorPlaybackSnapshot
            {
                animator = animator,
                wasEnabled = animator.enabled,
                speed = animator.speed,
                pausedByActivator = false
            };

            animatorSnapshots.Add(monster, snapshot);
        }

        private Animator GetAnimator(MonsterController monster)
        {
            if (monster == null)
            {
                return null;
            }

            if (animatorSnapshots.TryGetValue(monster, out AnimatorPlaybackSnapshot snapshot) && snapshot.animator != null)
            {
                return snapshot.animator;
            }

            return monster.GetComponentInChildren<Animator>(true);
        }
    }
}
