using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LSP.Gameplay.Navigation;
using UnityEngine;

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
    public class MonsterController : MonoBehaviour
    {
        [SerializeField]
        private float chaseSpeed = 2.5f;

        [SerializeField]
        private Transform chaseTarget;

        [SerializeField]
        private PlayerVision playerVision;


        [Header("Navigation")]
        [Tooltip("Graph used to build A* paths toward the player while chasing.")]
        [SerializeField]
        private AStarNavigationGraph navigationGraph;

        [SerializeField]
        private float repathInterval = 0.75f;

        [SerializeField]
        private float waypointTolerance = 0.25f;

        [Tooltip("How long the monster stays frozen after being hit by the disabler.")]
        [SerializeField]
        private float disablerFreezeDuration = 5f;


        private readonly List<Vector3> currentPath = new List<Vector3>();

        private Collider monsterCollider;
        private MonsterState currentState = MonsterState.Chasing;
        private Vector3 spawnPosition;
        private Coroutine disablerRoutine;

        private float repathTimer;
        private int currentPathIndex;


        public MonsterState CurrentState => currentState;

        private void Awake()
        {
            monsterCollider = GetComponent<Collider>();
            spawnPosition = transform.position;
        }


        private void OnDisable()
        {
            if (disablerRoutine != null)
            {
                StopCoroutine(disablerRoutine);
                disablerRoutine = null;
            }

            ClearPath();
        }

        private void Update()
        {
            UpdateStateFromVision();
            UpdateMovement(Time.deltaTime);
        }

        private void UpdateStateFromVision()
        {
            if (playerVision == null || monsterCollider == null)
            {
                return;
            }


            MonsterState previousState = currentState;
            bool inView = playerVision.CanSee(monsterCollider.bounds);
            currentState = inView ? MonsterState.Stationary : MonsterState.Chasing;

            if (currentState != previousState && currentState == MonsterState.Stationary)
            {
                ClearPath();
            }

            bool inView = playerVision.CanSee(monsterCollider.bounds);
            currentState = inView ? MonsterState.Stationary : MonsterState.Chasing;

        }

        private void UpdateMovement(float deltaTime)
        {
            if (currentState != MonsterState.Chasing || chaseTarget == null)
            {
                return;
            }


            if (navigationGraph != null)
            {
                repathTimer -= deltaTime;
                if (repathTimer <= 0f)
                {
                    RebuildPath();
                    repathTimer = repathInterval;
                }

                FollowPath(deltaTime);
            }
            else
            {
                MoveDirectly(deltaTime);
            }

            Vector3 direction = (chaseTarget.position - transform.position).normalized;
            transform.position += direction * chaseSpeed * deltaTime;

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
            if (other.TryGetComponent<PlayerStateController>(out var player))
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
            transform.position = spawnPosition;
            ClearPath();
            currentState = MonsterState.Stationary;
            yield return new WaitForSeconds(disablerFreezeDuration);
            disablerRoutine = null;
            currentState = MonsterState.Chasing;
        }

        /// <summary>
        /// Explicitly sets the chase target. Useful if the monster is spawned at runtime.
        /// </summary>
        public void SetTarget(Transform target)
        {
            chaseTarget = target;
            ClearPath();
        }

        public void SetPlayerVision(PlayerVision vision)
        {
            playerVision = vision;
        }

        public void SetNavigationGraph(AStarNavigationGraph graph)
        {
            navigationGraph = graph;
        }

        private void RebuildPath()
        {
            if (navigationGraph == null || chaseTarget == null)
            {
                return;
            }

            if (navigationGraph.TryBuildPath(transform.position, chaseTarget.position, currentPath))
            {
                currentPathIndex = 0;
            }
            else
            {
                currentPath.Clear();
                currentPathIndex = 0;
            }
        }

        private void FollowPath(float deltaTime)
        {
            if (currentPath.Count == 0)
            {
                MoveDirectly(deltaTime);
                return;
            }

            Vector3 targetPosition = currentPath[Mathf.Clamp(currentPathIndex, 0, currentPath.Count - 1)];
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= waypointTolerance * waypointTolerance)
            {
                currentPathIndex++;
                if (currentPathIndex >= currentPath.Count)
                {
                    currentPath.Clear();
                    MoveDirectly(deltaTime);
                    return;
                }

                targetPosition = currentPath[currentPathIndex];
                toTarget = targetPosition - transform.position;
                toTarget.y = 0f;
            }

            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 direction = toTarget.normalized;
            transform.position += direction * chaseSpeed * deltaTime;
        }

        private void MoveDirectly(float deltaTime)
        {
            Vector3 direction = (chaseTarget.position - transform.position);
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.position += direction.normalized * chaseSpeed * deltaTime;
        }

        private void ClearPath()
        {
            currentPath.Clear();
            currentPathIndex = 0;
            repathTimer = 0f;
        }
    }
}
