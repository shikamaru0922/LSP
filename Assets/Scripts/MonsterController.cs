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

        [Tooltip("Fallback speed used when the monster is moved directly because the NavMeshAgent is unavailable.")]
        [SerializeField]
        private float fallbackMoveSpeed = 2.5f;
        private Collider monsterCollider;
        private MonsterState currentState = MonsterState.Stationary;
        private Vector3 spawnPosition;
        private Coroutine disablerRoutine;
        private float timeSinceLastSeen;
        private bool isWorldAbnormal;
        private bool subscribedToWorldEvent;

        public MonsterState CurrentState => currentState;
        public float CurrentMoveSpeed => IsNavMeshAgentAvailable ? navMeshAgent.speed : fallbackMoveSpeed;

        private void Awake()
        {
            monsterCollider = GetComponent<Collider>();
            spawnPosition = transform.position;

            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (navMeshAgent != null && navMeshAgent.speed > 0f)
            {
                fallbackMoveSpeed = navMeshAgent.speed;
            }
            else if (navMeshAgent != null && navMeshAgent.speed <= 0f)
            {
                navMeshAgent.speed = Mathf.Max(0f, fallbackMoveSpeed);
            }

            timeSinceLastSeen = visionHoldDuration;
        }

        private void OnEnable()
        {
            SubscribeToWorldEvent();
            bool initialState = GameManager.Instance != null && GameManager.Instance.IsWorldAbnormal;
            ApplyWorldAbnormalState(initialState);
        }

        private void OnDisable()
        {
            if (disablerRoutine != null)
            {
                StopCoroutine(disablerRoutine);
                disablerRoutine = null;
            }

            StopNavMeshAgent();
            UnsubscribeFromWorldEvent();
        }

        private void Update()
        {
            if (!isWorldAbnormal)
            {
                if (currentState != MonsterState.Stationary)
                {
                    currentState = MonsterState.Stationary;
                    StopNavMeshAgent();
                }

                return;
            }

            float deltaTime = Time.deltaTime;
            UpdateStateFromVision(deltaTime);
            UpdateMovement(deltaTime);
        }

        private void UpdateStateFromVision(float deltaTime)
        {
            if (playerVision == null || monsterCollider == null)
            {
                return;
            }

            MonsterState previousState = currentState;
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
            currentState = isWorldAbnormal ? MonsterState.Chasing : MonsterState.Stationary;

            if (currentState == MonsterState.Chasing)
            {
                ResumeNavMeshAgent();
            }
            else
            {
                StopNavMeshAgent();
            }
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

            if (navMeshAgent != null)
            {
                navMeshAgent.speed = Mathf.Max(0f, fallbackMoveSpeed);
            }

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

            float speed = fallbackMoveSpeed;
            transform.position += direction.normalized * speed * deltaTime;
        }

        /// <summary>
        /// Overrides the monster's move speed. Applies to both NavMesh and direct movement.
        /// </summary>
        public void SetMoveSpeed(float speed)
        {
            float clampedSpeed = Mathf.Max(0f, speed);
            fallbackMoveSpeed = clampedSpeed;

            if (IsNavMeshAgentAvailable)
            {
                navMeshAgent.speed = clampedSpeed;
            }
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

        private void SubscribeToWorldEvent()
        {
            if (subscribedToWorldEvent)
            {
                return;
            }

            GameManager.WorldAbnormalStateChanged += ApplyWorldAbnormalState;
            subscribedToWorldEvent = true;
        }

        private void UnsubscribeFromWorldEvent()
        {
            if (!subscribedToWorldEvent)
            {
                return;
            }

            GameManager.WorldAbnormalStateChanged -= ApplyWorldAbnormalState;
            subscribedToWorldEvent = false;
        }

        private void ApplyWorldAbnormalState(bool state)
        {
            isWorldAbnormal = state;

            if (!isWorldAbnormal)
            {
                currentState = MonsterState.Stationary;
                StopNavMeshAgent();
            }
            else if (currentState == MonsterState.Stationary)
            {
                timeSinceLastSeen = visionHoldDuration;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }
        }
#endif
    }
}
