using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay
{
    public enum NpcState
    {
        Normal,
        DeadStare
    }

    /// <summary>
    /// Drives the "dead stare" event behaviour on NPCs by stopping their movement and
    /// animators, then rotating the head to continuously face the player.
    /// </summary>
    public class NpcDeadStareController : MonoBehaviour
    {
        [SerializeField]
        private Transform headTransform;

        [SerializeField]
        private Transform playerTransform;

        [SerializeField]
        private Animator animator;

        [Tooltip("Optional behaviours that should be disabled when entering the dead stare state.")]
        [SerializeField]
        private List<Behaviour> behavioursToDisable = new List<Behaviour>();

        [Tooltip("Degrees per second used when rotating the head to follow the player.")]
        [Min(0f)]
        [SerializeField]
        private float headTurnSpeed = 360f;

        [Header("Dead stare jitter")]
        [Tooltip("Minimum and maximum time between head jitter bursts while staring.")]
        [SerializeField]
        private Vector2 jitterIntervalRange = new Vector2(0.1f, 0.35f);

        [Tooltip("Maximum yaw offset (in degrees) applied during a jitter burst.")]
        [Min(0f)]
        [SerializeField]
        private float jitterAngleDegrees = 12f;

        [Tooltip("Degrees per second used when snapping toward the jitter offset.")]
        [Min(0f)]
        [SerializeField]
        private float jitterSnapSpeed = 1440f;

        [Tooltip("Degrees per second used when recovering from the jitter offset back to neutral.")]
        [Min(0f)]
        [SerializeField]
        private float jitterRecoverySpeed = 540f;

        [Tooltip("Probability of the jitter offset leaning to the NPC's right side.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float jitterRightBias = 0.65f;

        private NpcState currentState = NpcState.Normal;
        private readonly List<bool> cachedBehaviourStates = new List<bool>();
        private float originalAnimatorSpeed = 1f;
        private bool originalAnimatorEnabled = true;
        private bool isSubscribed;
        private bool hasCachedWorldState;
        private bool lastKnownWorldAbnormal;
        private Quaternion cachedHeadLocalRotation;
        private bool hasCachedHeadRotation;
        private Quaternion jitterOffsetLocal = Quaternion.identity;
        private Quaternion jitterTargetLocal = Quaternion.identity;
        private float jitterCountdown;

        private enum JitterPhase
        {
            Idle,
            MovingToOffset,
            Returning
        }

        private JitterPhase jitterPhase = JitterPhase.Idle;

        public NpcState CurrentState => currentState;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (headTransform == null && animator != null)
            {
                headTransform = animator.transform;
            }

            CacheBehaviourStates();

            if (headTransform != null)
            {
                CacheHeadRotation();
            }

            if (animator != null)
            {
                originalAnimatorSpeed = animator.speed;
                originalAnimatorEnabled = animator.enabled;
            }
        }

        private void Start()
        {
            if (playerTransform == null)
            {
                PlayerStateController playerState = FindObjectOfType<PlayerStateController>();
                if (playerState != null)
                {
                    playerTransform = playerState.transform;
                }
            }
        }

        private void OnEnable()
        {
            SubscribeToManager();
            RefreshWorldState(true);
        }

        private void OnDisable()
        {
            UnsubscribeFromManager();
            RestoreBehaviours();
            RestoreAnimatorState();
            currentState = NpcState.Normal;
            hasCachedWorldState = false;
        }

        private void Update()
        {
            RefreshWorldState(false);

            if (currentState != NpcState.DeadStare)
            {
                ResetJitterState(false);
                return;
            }

            if (headTransform == null || playerTransform == null)
            {
                ResetJitterState(false);
                return;
            }

            UpdateJitter(Time.deltaTime);

            Vector3 toPlayer = playerTransform.position - headTransform.position;
            if (toPlayer.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            RotateHeadTowards(targetRotation);
        }

        private void CacheBehaviourStates()
        {
            cachedBehaviourStates.Clear();
            foreach (Behaviour behaviour in behavioursToDisable)
            {
                cachedBehaviourStates.Add(behaviour != null && behaviour.enabled);
            }
        }

        private void SubscribeToManager()
        {
            if (isSubscribed)
            {
                return;
            }

            GameManager.WorldAbnormalStateChanged += ApplyWorldState;
            isSubscribed = true;
        }

        private void UnsubscribeFromManager()
        {
            if (!isSubscribed)
            {
                return;
            }

            GameManager.WorldAbnormalStateChanged -= ApplyWorldState;
            isSubscribed = false;
        }

        private void ApplyWorldState(bool isWorldAbnormal)
        {
            hasCachedWorldState = true;
            lastKnownWorldAbnormal = isWorldAbnormal;
            SetState(isWorldAbnormal ? NpcState.DeadStare : NpcState.Normal);
        }

        private void RefreshWorldState(bool forceApply)
        {
            GameManager manager = GameManager.Instance;
            bool managerState = manager != null && manager.IsWorldAbnormal;

            if (!hasCachedWorldState || forceApply || managerState != lastKnownWorldAbnormal)
            {
                ApplyWorldState(managerState);
            }
        }

        private void SetState(NpcState newState)
        {
            if (currentState == newState)
            {
                return;
            }

            currentState = newState;

            if (currentState == NpcState.DeadStare)
            {
                EnterDeadStare();
            }
            else
            {
                ExitDeadStare();
            }
        }

        private void EnterDeadStare()
        {
            CacheBehaviourStates();
            DisableBehaviours();
            CacheAnimatorState();
            DisableAnimator();
            CacheHeadRotation();
            ResetJitterState(true);
        }

        private void ExitDeadStare()
        {
            RestoreBehaviours();
            RestoreAnimatorState();
            RestoreHeadRotation();
            ResetJitterState(false);
        }

        private void DisableBehaviours()
        {
            for (int i = 0; i < behavioursToDisable.Count; i++)
            {
                Behaviour behaviour = behavioursToDisable[i];
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private void RestoreBehaviours()
        {
            for (int i = 0; i < behavioursToDisable.Count; i++)
            {
                Behaviour behaviour = behavioursToDisable[i];
                if (behaviour == null)
                {
                    continue;
                }

                bool enabledState = i < cachedBehaviourStates.Count && cachedBehaviourStates[i];
                behaviour.enabled = enabledState;
            }
        }

        private void CacheAnimatorState()
        {
            if (animator != null)
            {
                originalAnimatorSpeed = animator.speed;
                originalAnimatorEnabled = animator.enabled;
            }
        }

        private void DisableAnimator()
        {
            if (animator != null)
            {
                animator.enabled = false;
                animator.speed = 0f;
            }
        }

        private void RestoreAnimatorState()
        {
            if (animator != null)
            {
                animator.enabled = originalAnimatorEnabled;
                animator.speed = originalAnimatorSpeed;
            }
        }

        private void CacheHeadRotation()
        {
            if (headTransform == null)
            {
                return;
            }

            cachedHeadLocalRotation = headTransform.localRotation;
            hasCachedHeadRotation = true;
        }

        private void RestoreHeadRotation()
        {
            if (!hasCachedHeadRotation || headTransform == null)
            {
                return;
            }

            headTransform.localRotation = cachedHeadLocalRotation;
        }

        private void RotateHeadTowards(Quaternion targetWorldRotation)
        {
            if (headTransform == null)
            {
                return;
            }

            Transform parent = headTransform.parent;
            if (parent != null)
            {
                Quaternion targetLocal = Quaternion.Inverse(parent.rotation) * targetWorldRotation;
                targetLocal = ApplyJitterToLocal(targetLocal);
                Quaternion nextLocal = Quaternion.RotateTowards(headTransform.localRotation, targetLocal, headTurnSpeed * Time.deltaTime);
                headTransform.localRotation = nextLocal;
            }
            else
            {
                Quaternion jitteredTarget = ApplyJitterToWorld(targetWorldRotation);
                headTransform.rotation = Quaternion.RotateTowards(headTransform.rotation, jitteredTarget, headTurnSpeed * Time.deltaTime);
            }
        }

        private void UpdateJitter(float deltaTime)
        {
            if (!IsJitterActive())
            {
                ResetJitterState(false);
                return;
            }

            switch (jitterPhase)
            {
                case JitterPhase.Idle:
                    jitterOffsetLocal = Quaternion.identity;
                    jitterTargetLocal = Quaternion.identity;
                    jitterCountdown -= deltaTime;
                    if (jitterCountdown <= 0f)
                    {
                        jitterTargetLocal = Quaternion.AngleAxis(GetNextJitterAngle(), Vector3.up);
                        jitterPhase = JitterPhase.MovingToOffset;
                    }
                    break;

                case JitterPhase.MovingToOffset:
                    jitterOffsetLocal = RotateTowards(jitterOffsetLocal, jitterTargetLocal, jitterSnapSpeed, deltaTime);
                    if (Quaternion.Angle(jitterOffsetLocal, jitterTargetLocal) <= 0.25f)
                    {
                        jitterTargetLocal = Quaternion.identity;
                        jitterPhase = JitterPhase.Returning;
                    }
                    break;

                case JitterPhase.Returning:
                    jitterOffsetLocal = RotateTowards(jitterOffsetLocal, jitterTargetLocal, jitterRecoverySpeed, deltaTime);
                    if (Quaternion.Angle(jitterOffsetLocal, Quaternion.identity) <= 0.25f)
                    {
                        jitterOffsetLocal = Quaternion.identity;
                        jitterCountdown = GetNextJitterDelay();
                        jitterPhase = JitterPhase.Idle;
                    }
                    break;
            }
        }

        private Quaternion RotateTowards(Quaternion current, Quaternion target, float speed, float deltaTime)
        {
            if (speed <= 0f)
            {
                return target;
            }

            return Quaternion.RotateTowards(current, target, speed * deltaTime);
        }

        private Quaternion ApplyJitterToLocal(Quaternion targetLocal)
        {
            if (!IsJitterActive())
            {
                return targetLocal;
            }

            return targetLocal * jitterOffsetLocal;
        }

        private Quaternion ApplyJitterToWorld(Quaternion targetWorld)
        {
            if (!IsJitterActive())
            {
                return targetWorld;
            }

            return targetWorld * jitterOffsetLocal;
        }

        private bool IsJitterActive()
        {
            return headTransform != null && currentState == NpcState.DeadStare && jitterAngleDegrees > Mathf.Epsilon;
        }

        private void ResetJitterState(bool immediateStart)
        {
            jitterOffsetLocal = Quaternion.identity;
            jitterTargetLocal = Quaternion.identity;
            jitterPhase = JitterPhase.Idle;
            jitterCountdown = immediateStart ? 0f : GetNextJitterDelay();
        }

        private float GetNextJitterDelay()
        {
            float min = Mathf.Max(0f, jitterIntervalRange.x);
            float max = Mathf.Max(min, jitterIntervalRange.y);
            return Random.Range(min, max);
        }

        private float GetNextJitterAngle()
        {
            if (jitterAngleDegrees <= 0f)
            {
                return 0f;
            }

            float magnitude = Random.Range(0.5f * jitterAngleDegrees, jitterAngleDegrees);
            float direction = Random.value <= jitterRightBias ? 1f : -1f;
            return magnitude * direction;
        }

        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
        }

        public void SetHeadTransform(Transform head)
        {
            headTransform = head;
        }

        public void SetAnimator(Animator targetAnimator)
        {
            animator = targetAnimator;
        }

        public void SetBehavioursToDisable(List<Behaviour> behaviours)
        {
            behavioursToDisable = behaviours ?? new List<Behaviour>();
            CacheBehaviourStates();
        }
    }
}
