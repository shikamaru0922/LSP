using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace LSP.Gameplay
{
    public enum MonsterState
    {
        Stationary,
        Chasing,
        Returning
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

        [Header("Chase Constraints")]
        [Tooltip("Distance from the spawn point at which the monster begins chasing the player. Set to 0 to disable.")]
        [Min(0f)]
        [SerializeField]
        private float chaseActivationRadius = 10f;

        [Tooltip("Maximum distance the monster can travel from its spawn point while chasing before disengaging. Set to 0 to disable.")]
        [Min(0f)]
        [SerializeField]
        private float chaseLeashRadius = 15f;

        [Tooltip("How long the monster stays frozen after being hit by the disabler.")]
        [SerializeField]
        private float disablerFreezeDuration = 5f;

        [Tooltip("Distance threshold to consider the monster back at its spawn point when returning.")]
        [Min(0f)]
        [SerializeField]
        private float returnDistanceThreshold = 0.5f;

        [Header("Vision Handling")]
        [Tooltip("How long the monster stays frozen after briefly leaving the player's view.")]
        [Min(0f)]
        [SerializeField]
        private float visionHoldDuration = 0.2f;

        [Header("NavMesh")]
        [SerializeField]
        private NavMeshAgent navMeshAgent;

        [Header("Animation")]
        [SerializeField]
        private Animator animator;

        [Tooltip("Name of the walking state used when the monster is moving.")]
        [SerializeField]
        private string walkingStateName = "walking";

        [Tooltip("Cross-fade duration when transitioning into the walking state.")]
        [Min(0f)]
        [SerializeField]
        private float walkingTransitionDuration = 0.1f;

        [Header("Player Proximity Restart")]
        [Tooltip("If enabled, the level restarts when the monster reaches the player.")]
        [SerializeField]
        private bool enableProximityRestart = true;

        [Tooltip("Distance at which the proximity restart triggers when enabled.")]
        [Min(0f)]
        [SerializeField]
        private float proximityRestartDistance = 1.5f;

        [Tooltip("Fallback speed used when the monster is moved directly because the NavMeshAgent is unavailable.")]
        [SerializeField]
        private float fallbackMoveSpeed = 2.5f;

        public static event Action<MonsterController> MonsterReset;

        private Collider monsterCollider;
        private MonsterState currentState = MonsterState.Stationary;
        private Vector3 spawnPosition;
        private Coroutine disablerRoutine;
        private float timeSinceLastSeen;
        private bool isWorldAbnormal;
        private bool subscribedToWorldEvent;
        private float desiredMoveSpeed;
        private bool isMoveSpeedFrozen;
        private bool navAgentDisabledByVision;
        private bool animatorFrozenByVision;
        private float cachedAnimatorSpeed = 1f;
        private bool cachedAnimatorEnabled = true;
        private int walkingStateHash;
        private bool walkingStateAvailable;
        private bool restartTriggered;
        private bool returningIgnoresVision;

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

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
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
            desiredMoveSpeed = Mathf.Max(0f, fallbackMoveSpeed);

            if (animator != null)
            {
                cachedAnimatorSpeed = animator.speed;
                cachedAnimatorEnabled = animator.enabled;
                CacheAnimatorAnimationConfiguration();
            }
        }

        private void OnEnable()
        {
            SubscribeToWorldEvent();
            bool initialState = GameManager.Instance != null && GameManager.Instance.IsWorldAbnormal;
            ApplyWorldAbnormalState(initialState);
            CacheAnimatorAnimationConfiguration();
            restartTriggered = false;
            returningIgnoresVision = false;
            ApplyAnimatorMovementState();
        }

        private void OnDisable()
        {
            if (disablerRoutine != null)
            {
                StopCoroutine(disablerRoutine);
                disablerRoutine = null;
            }

            if (navAgentDisabledByVision && navMeshAgent != null)
            {
                navMeshAgent.enabled = true;
                navAgentDisabledByVision = false;
            }

            StopNavMeshAgent();
            RestoreAnimatorFromVision();
            UnsubscribeFromWorldEvent();
            returningIgnoresVision = false;
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

                ApplyAnimatorMovementState();
                CheckForProximityRestart();
                return;
            }

            float deltaTime = Time.deltaTime;
            UpdateStateFromVision(deltaTime);
            UpdateMovement(deltaTime);
            ApplyAnimatorMovementState();
            CheckForProximityRestart();
        }

        private void UpdateStateFromVision(float deltaTime)
        {
            if (playerVision == null || monsterCollider == null)
            {
                return;
            }

            bool canChase = ShouldChaseTarget();

            if (!canChase && currentState != MonsterState.Returning && !IsAtSpawn())
            {
                BeginReturnToSpawn();
            }

            MonsterState previousState = currentState;

            bool inView = playerVision.CanSee(monsterCollider);
            timeSinceLastSeen = inView ? 0f : timeSinceLastSeen + deltaTime;

            bool allowVisionControl = currentState != MonsterState.Returning || !returningIgnoresVision;
            bool shouldHoldStationary = allowVisionControl && (inView || timeSinceLastSeen < visionHoldDuration);

            if (shouldHoldStationary)
            {
                FreezeMoveSpeed();
                FreezeAnimatorByVision();

                if (allowVisionControl && currentState != MonsterState.Returning)
                {
                    currentState = MonsterState.Stationary;
                }
            }
            else
            {
                RestoreMoveSpeed();
                RestoreAnimatorFromVision();

                if (currentState == MonsterState.Stationary)
                {
                    if (canChase)
                    {
                        currentState = MonsterState.Chasing;
                    }
                }
                else if (currentState == MonsterState.Chasing && !canChase)
                {
                    if (!IsAtSpawn())
                    {
                        BeginReturnToSpawn();
                    }
                    else
                    {
                        currentState = MonsterState.Stationary;
                    }
                }
            }

            if (currentState != previousState)
            {
                if (currentState == MonsterState.Stationary)
                {
                    StopNavMeshAgent();
                    ApplyAnimatorMovementState();
                }
                else if (currentState == MonsterState.Chasing)
                {
                    ResumeNavMeshAgent();
                    ApplyAnimatorMovementState();
                }
            }
        }

        private void UpdateMovement(float deltaTime)
        {
            if (currentState == MonsterState.Returning)
            {
                HandleReturnMovement(deltaTime);
                return;
            }

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
                disablerRoutine = null;
            }

            BeginReturnToSpawn(true);
            disablerRoutine = StartCoroutine(DisablerRoutine());
        }

        private IEnumerator DisablerRoutine()
        {
            FreezeMoveSpeed();
            yield return new WaitForSeconds(disablerFreezeDuration);
            disablerRoutine = null;
            RestoreMoveSpeed();

            if (currentState == MonsterState.Returning)
            {
                ResumeNavMeshAgent();
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

        /// <summary>
        /// Begins returning the monster to its spawn location and broadcasts the reset event.
        /// </summary>
        public void ResetToSpawn()
        {
            ResetToSpawn(false);
        }

        public void ResetToSpawn(bool ignoreVisionFreeze)
        {
            BeginReturnToSpawn(ignoreVisionFreeze);
        }

        private void BeginReturnToSpawn(bool ignoreVisionFreeze = false)
        {
            if (currentState == MonsterState.Returning)
            {
                if (ignoreVisionFreeze && !returningIgnoresVision)
                {
                    returningIgnoresVision = true;
                    RestoreMoveSpeed();
                    RestoreAnimatorFromVision();
                }

                return;
            }

            returningIgnoresVision = ignoreVisionFreeze;

            if (ignoreVisionFreeze)
            {
                RestoreMoveSpeed();
                RestoreAnimatorFromVision();
            }

            StopNavMeshAgent();
            currentState = MonsterState.Returning;
            timeSinceLastSeen = 0f;
            RaiseMonsterReset();
            ApplyAnimatorMovementState();
        }

        private void CompleteReturnToSpawn()
        {
            if (currentState != MonsterState.Returning)
            {
                return;
            }

            SnapToSpawnPosition();
            currentState = MonsterState.Stationary;
            timeSinceLastSeen = visionHoldDuration;
            returningIgnoresVision = false;
            StopNavMeshAgent();
            RestoreAnimatorFromVision();
            ApplyAnimatorMovementState();
        }

        private void SnapToSpawnPosition()
        {
            bool warpedToSpawn = false;
            if (IsNavMeshAgentAvailable && navMeshAgent.isOnNavMesh)
            {
                warpedToSpawn = navMeshAgent.Warp(spawnPosition);
                navMeshAgent.ResetPath();
                navMeshAgent.velocity = Vector3.zero;
                navMeshAgent.isStopped = true;
            }

            if (!warpedToSpawn)
            {
                transform.position = spawnPosition;
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
                if (!isMoveSpeedFrozen && !navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = true;
                }

                ApplyMoveSpeed(isMoveSpeedFrozen ? 0f : desiredMoveSpeed);

                if (isMoveSpeedFrozen)
                {
                    if (navMeshAgent.enabled)
                    {
                        if (navMeshAgent.isOnNavMesh)
                        {
                            navMeshAgent.ResetPath();
                            navMeshAgent.nextPosition = transform.position;
                        }

                        navMeshAgent.velocity = Vector3.zero;
                        navMeshAgent.isStopped = true;
                        navMeshAgent.enabled = false;
                    }

                    navAgentDisabledByVision = true;
                }
                else
                {
                    navAgentDisabledByVision = false;

                    if (navMeshAgent.isOnNavMesh)
                    {
                        navMeshAgent.nextPosition = transform.position;
                    }
                }
            }
            else
            {
                navAgentDisabledByVision = false;
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
            if (chaseTarget == null)
            {
                return;
            }

            MoveDirectlyTowards(chaseTarget.position, deltaTime);
        }

        private void MoveDirectlyTowards(Vector3 targetPosition, float deltaTime)
        {
            Vector3 direction = (targetPosition - transform.position);
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
            desiredMoveSpeed = clampedSpeed;

            if (!isMoveSpeedFrozen)
            {
                ApplyMoveSpeed(clampedSpeed);
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

        private void FreezeMoveSpeed()
        {
            if (isMoveSpeedFrozen)
            {
                return;
            }

            isMoveSpeedFrozen = true;
            ApplyMoveSpeed(0f);

            if (IsNavMeshAgentAvailable)
            {
                if (navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.ResetPath();
                    navMeshAgent.nextPosition = transform.position;
                }

                navMeshAgent.velocity = Vector3.zero;
                navMeshAgent.isStopped = true;
                navMeshAgent.enabled = false;
                navAgentDisabledByVision = true;
            }
        }

        private void FreezeAnimatorByVision()
        {
            if (animator == null || animatorFrozenByVision)
            {
                return;
            }

            cachedAnimatorSpeed = animator.speed;
            cachedAnimatorEnabled = animator.enabled;
            EnsureWalkingState();
            animator.enabled = false;
            animator.speed = 0f;
            animatorFrozenByVision = true;
        }

        private void RestoreAnimatorFromVision()
        {
            if (animator == null || !animatorFrozenByVision)
            {
                return;
            }

            animator.enabled = cachedAnimatorEnabled;
            animator.speed = cachedAnimatorSpeed;
            animatorFrozenByVision = false;
            ApplyAnimatorMovementState();
        }

        private void RestoreMoveSpeed()
        {
            if (!isMoveSpeedFrozen)
            {
                return;
            }

            isMoveSpeedFrozen = false;

            if (navAgentDisabledByVision)
            {
                if (navMeshAgent != null)
                {
                    navMeshAgent.enabled = true;
                    bool warpedToCurrentPosition = navMeshAgent.Warp(transform.position);

                    if (navMeshAgent.isOnNavMesh)
                    {
                        navMeshAgent.ResetPath();
                        navMeshAgent.nextPosition = transform.position;
                    }
                    else if (!warpedToCurrentPosition)
                    {
                        navMeshAgent.nextPosition = transform.position;
                    }

                    navMeshAgent.velocity = Vector3.zero;
                    navMeshAgent.isStopped = true;
                }

                navAgentDisabledByVision = false;
            }

            ApplyMoveSpeed(desiredMoveSpeed);
        }

        private void ApplyMoveSpeed(float speed)
        {
            fallbackMoveSpeed = Mathf.Max(0f, speed);

            if (IsNavMeshAgentAvailable)
            {
                navMeshAgent.speed = fallbackMoveSpeed;
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

        private void ApplyAnimatorMovementState()
        {
            if (animator == null)
            {
                return;
            }

            bool shouldWalk = currentState == MonsterState.Chasing || currentState == MonsterState.Returning;

            if (animatorFrozenByVision)
            {
                if (shouldWalk)
                {
                    EnsureWalkingState();
                }

                return;
            }

            if (shouldWalk)
            {
                EnsureWalkingState();
            }
        }

        private void CacheAnimatorAnimationConfiguration()
        {
            walkingStateHash = !string.IsNullOrEmpty(walkingStateName) ? Animator.StringToHash(walkingStateName) : 0;
            walkingStateAvailable = false;

            if (animator == null)
            {
                return;
            }

            if (walkingStateHash != 0 && animator.runtimeAnimatorController != null)
            {
                walkingStateAvailable = animator.HasState(0, walkingStateHash);
            }
        }

        private void EnsureWalkingState()
        {
            if (!walkingStateAvailable || animator == null)
            {
                return;
            }

            if (animator.IsInTransition(0))
            {
                return;
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash != walkingStateHash)
            {
                animator.CrossFadeInFixedTime(walkingStateHash, walkingTransitionDuration);
            }
        }

        private void CheckForProximityRestart()
        {
            if (!enableProximityRestart || restartTriggered || chaseTarget == null)
            {
                return;
            }

            if (currentState != MonsterState.Chasing)
            {
                return;
            }

            float threshold = Mathf.Max(0f, proximityRestartDistance);
            if (threshold <= 0f)
            {
                return;
            }

            Vector3 difference = chaseTarget.position - transform.position;
            difference.y = 0f;
            float distanceSqr = difference.sqrMagnitude;
            if (distanceSqr <= threshold * threshold)
            {
                TriggerProximityRestart();
            }
        }

        private void TriggerProximityRestart()
        {
            if (restartTriggered)
            {
                return;
            }

            restartTriggered = true;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return;
            }

            SceneManager.LoadScene(activeScene.name);
        }

        private bool IsNavMeshAgentAvailable => navMeshAgent != null && navMeshAgent.enabled;

        private bool IsNavMeshAgentReady => IsNavMeshAgentAvailable && navMeshAgent.isOnNavMesh;

        private void HandleReturnMovement(float deltaTime)
        {
            if (isMoveSpeedFrozen)
            {
                StopNavMeshAgent();
                return;
            }

            Vector3 targetPosition = spawnPosition;
            float threshold = Mathf.Max(returnDistanceThreshold, 0.05f);
            float thresholdSqr = threshold * threshold;

            if (IsNavMeshAgentAvailable)
            {
                if (!navMeshAgent.isOnNavMesh)
                {
                    bool warpedToCurrentPosition = navMeshAgent.Warp(transform.position);

                    if (!warpedToCurrentPosition)
                    {
                        MoveDirectlyTowards(targetPosition, deltaTime);

                        if ((transform.position - targetPosition).sqrMagnitude <= thresholdSqr)
                        {
                            CompleteReturnToSpawn();
                        }

                        return;
                    }
                }

                if (IsNavMeshAgentReady)
                {
                    navMeshAgent.isStopped = false;
                    navMeshAgent.SetDestination(targetPosition);

                    if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= threshold)
                    {
                        CompleteReturnToSpawn();
                    }

                    return;
                }
            }

            MoveDirectlyTowards(targetPosition, deltaTime);

            if ((transform.position - targetPosition).sqrMagnitude <= thresholdSqr)
            {
                CompleteReturnToSpawn();
            }
        }

        private bool ShouldChaseTarget()
        {
            if (chaseTarget == null)
            {
                return false;
            }

            if (chaseActivationRadius > 0f)
            {
                float activationRadiusSqr = chaseActivationRadius * chaseActivationRadius;
                Vector3 targetOffset = chaseTarget.position - spawnPosition;
                targetOffset.y = 0f;

                if (targetOffset.sqrMagnitude > activationRadiusSqr)
                {
                    return false;
                }
            }

            if (chaseLeashRadius > 0f)
            {
                float leashRadiusSqr = chaseLeashRadius * chaseLeashRadius;
                Vector3 leashOffset = transform.position - spawnPosition;
                leashOffset.y = 0f;

                if (leashOffset.sqrMagnitude > leashRadiusSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsAtSpawn()
        {
            float threshold = Mathf.Max(returnDistanceThreshold, 0.05f);
            float thresholdSqr = threshold * threshold;
            Vector3 offset = transform.position - spawnPosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= thresholdSqr;
        }

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
                CompleteReturnToSpawn();
                currentState = MonsterState.Stationary;
                StopNavMeshAgent();
                RaiseMonsterReset();
            }
            else if (currentState == MonsterState.Stationary)
            {
                timeSinceLastSeen = visionHoldDuration;
            }

            ApplyAnimatorMovementState();
        }

        private void RaiseMonsterReset()
        {
            MonsterReset?.Invoke(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }

            returnDistanceThreshold = Mathf.Max(0f, returnDistanceThreshold);
            walkingTransitionDuration = Mathf.Max(0f, walkingTransitionDuration);
            proximityRestartDistance = Mathf.Max(0f, proximityRestartDistance);

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            CacheAnimatorAnimationConfiguration();
        }
#endif
    }
}
