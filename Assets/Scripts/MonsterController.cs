using System.Collections;
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

        [Tooltip("How long the monster stays frozen after being hit by the disabler.")]
        [SerializeField]
        private float disablerFreezeDuration = 5f;

        private Collider monsterCollider;
        private MonsterState currentState = MonsterState.Chasing;
        private Vector3 spawnPosition;
        private Coroutine disablerRoutine;

        public MonsterState CurrentState => currentState;

        private void Awake()
        {
            monsterCollider = GetComponent<Collider>();
            spawnPosition = transform.position;
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

            bool inView = playerVision.CanSee(monsterCollider.bounds);
            currentState = inView ? MonsterState.Stationary : MonsterState.Chasing;
        }

        private void UpdateMovement(float deltaTime)
        {
            if (currentState != MonsterState.Chasing || chaseTarget == null)
            {
                return;
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
        }

        public void SetPlayerVision(PlayerVision vision)
        {
            playerVision = vision;
        }
    }
}
