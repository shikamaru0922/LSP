using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace LSP.Gameplay
{
    public enum MonsterState
    {
        Stationary,
        Chasing
    }

    /// <summary>
    /// Implements the chase / gaze behaviour loop of the monster.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterController : MonoBehaviour
    {
        [SerializeField]
        private Transform chaseTarget;

        [SerializeField]
        private PlayerVision playerVision;

        [Tooltip("How long the monster stays frozen after being hit by the disabler.")]
        [SerializeField]
        private float disablerFreezeDuration = 5f;

        [Header("Vision Handling")]
        [Tooltip("How long the monster stays frozen after briefly leaving the player's view.")]
        [Min(0f)]
        [SerializeField]
        private float visionHoldDuration = 0.2f;

        [Header("NavMesh")]
        [SerializeField]
        private NavMeshAgent navMeshAgent;

        private Collider monsterCollider;

        // ali1gq 分支：默认从 Stationary 开始（而不是 Chasing）
        private MonsterState currentState = MonsterState.Stationary;

        private Vector3 spawnPosition;
        private Coroutine disablerRoutine;
        private float timeSinceLastSeen;

        // ali1gq 分支新增：世界异常开关与订阅标记
        private bool isWorldAbnormal;
        private bool subscribedToWorldEvent;

        public MonsterState CurrentState => currentState;

        private void Awake()
        {
            monsterCollider = GetComponent<Collider>();
            spawnPosition = transform.position;

            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }

            timeSinceLastSeen = visionHoldDuration;
        }

        private void OnEnable()
        {
            SubscribeToWorldEvent();
        }

        private void OnDisable()
        {
            if (disablerRoutine != null)
            {
                StopCoroutine(disablerRoutine);
                disablerRoutine = null;
            }

            UnsubscribeWorldEvent();
            StopNavMeshAgent();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            UpdateStateFromVision(deltaTime);
            UpdateMovement(deltaTime);
        }

        private void UpdateStateFromVision(float deltaTime)
        {
            // 世界异常时，怪物应被强制静止（保留 ali1gq 分支意图）
            if (isWorldAbnormal)
            {
                if (currentState != MonsterState.Stationary)
                {
                    currentState = MonsterState.Stationary;
                    StopNavMeshAgent();
                }
                return;
            }

            if (playerVision == null || monsterCollider == null)
            {
                return;
            }

            MonsterState previousState = currentState;

            // ali1gq 分支期望与 PlayerVision 的 Collider 重载协作（含遮挡判定）
            bool inView = playerVision.CanSee(monsterCollider);

            timeSinceLastSeen = inView ? 0f : timeSinceLastSeen + deltaTime;

            bool shouldHoldStationary = inView || timeSinceLastSeen < visionHoldDuration;
            currentState = shouldHoldStationary ? MonsterState.Stationary : MonsterState.Chasing;

            if (currentState != previousState && currentState == MonsterState.Stationary)
            {
                StopNavMeshAgent();
            }
            else if (currentState != previousState && currentState == MonsterState.Chasing)
            {
                ResumeNavMeshAgent();
            }
        }

        private void UpdateMovement(float deltaTime)
        {
            if (currentState != MonsterState.Chasing || chaseTarget == null)
            {
                StopNavMeshAgent();
                return;
            }

            if (IsNavMeshAgentReady)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(chaseTarget.position);
                return;
            }

            MoveDirectly(deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (currentState != MonsterState.Chasing)
            {
                return;
            }

            if (!other.TryGetComponent<PlayerStateController>(out var player))
            {
                player = other.GetComponentInParent<PlayerStateController>();
            }

            if (player != null)
            {
                player.Kill();
            }
        }

        /// <summary>
        /// Applies the disabler effect, forcing the monster back to its spawn position and freezing it.
        /// </summary>
        public void ApplyDisablerEffect()
        {
            if (disablerRoutine != null)
            {
                StopCoroutine(disablerRoutine);
            }

            disablerRoutine = StartCoroutine(DisablerRoutine());
        }

        private IEnumerator DisablerRoutine()
        {
            bool warpedToSpawn = false;
            if (IsNavMeshAgentAvailable && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.Warp(spawnPosition);
                navMeshAgent.ResetPath();
                navMeshAgent.isStopped = true;
                warpedToSpawn = true;
            }

            if (!warpedToSpawn)
            {
                transform.position = spawnPosition;
            }

            StopNavMeshAgent();
            currentState = MonsterState.Stationary;
            yield return new WaitForSeconds(disablerFreezeDuration);
            disablerRoutine = null;
            currentState = MonsterState.Chasing;
            ResumeNavMeshAgent();
        }

        /// <summary>
        /// Explicitly sets the chase target. Useful if the monster is spawned at runtime.
        /// </summary>
        public void SetTarget(Transform target)
        {
            chaseTarget = target;

            if (currentState == MonsterState.Chasing)
            {
                ResumeNavMeshAgent();
            }
            else
            {
                StopNavMeshAgent();
            }
        }

        public void SetPlayerVision(PlayerVision vision)
        {
            playerVision = vision;
        }

        public void SetNavMeshAgent(NavMeshAgent agent)
        {
            navMeshAgent = agent;

            if (currentState == MonsterState.Chasing)
            {
                ResumeNavMeshAgent();
            }
            else
            {
                StopNavMeshAgent();
            }
        }

        private void MoveDirectly(float deltaTime)
        {
            Vector3 direction = (chaseTarget.position - transform.position);
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float speed = navMeshAgent != null ? navMeshAgent.speed : 0f;
            transform.position += direction.normalized * speed * deltaTime;
        }

        private void StopNavMeshAgent()
        {
            if (!IsNavMeshAgentAvailable)
            {
                return;
            }

            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;

            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.ResetPath();
                navMeshAgent.nextPosition = transform.position;
            }
        }

        private void ResumeNavMeshAgent()
        {
            if (!IsNavMeshAgentAvailable)
            {
                return;
            }

            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.nextPosition = transform.position;
                navMeshAgent.isStopped = false;
                navMeshAgent.velocity = Vector3.zero;
            }
        }

        private bool IsNavMeshAgentAvailable => navMeshAgent != null && navMeshAgent.enabled;

        private bool IsNavMeshAgentReady => IsNavMeshAgentAvailable && navMeshAgent.isOnNavMesh;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }
        }
#endif

        // ===== ali1gq 分支：世界异常事件订阅/回调占位（请替换为你的全局事件系统） =====
        private void SubscribeToWorldEvent()
        {
            if (subscribedToWorldEvent) return;
            subscribedToWorldEvent = true;

            // TODO: 在这里订阅你的全局事件（例如：WorldState.OnAbnormal += OnWorldAbnormalChanged;）
            // 这里先模拟为常量：正常为 false
            OnWorldAbnormalChanged(false);
        }

        private void UnsubscribeWorldEvent()
        {
            if (!subscribedToWorldEvent) return;
            subscribedToWorldEvent = false;

            // TODO: 在这里取消订阅你的全局事件
        }

        // 当世界切入“异常态”时，强制怪物静止；恢复正常后按视野规则继续
        private void OnWorldAbnormalChanged(bool abnormal)
        {
            isWorldAbnormal = abnormal;

            if (isWorldAbnormal)
            {
                currentState = MonsterState.Stationary;
                StopNavMeshAgent();
            }
        }
        // ===================================================================
    }
}
