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

        private NpcState currentState = NpcState.Normal;
        private readonly List<bool> cachedBehaviourStates = new List<bool>();
        private float originalAnimatorSpeed = 1f;
        private bool isSubscribed;
        private bool hasCachedWorldState;
        private bool lastKnownWorldAbnormal;

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
            RestoreAnimatorSpeed();
            currentState = NpcState.Normal;
            hasCachedWorldState = false;
        }

        private void Update()
        {
            RefreshWorldState(false);

            if (currentState != NpcState.DeadStare)
            {
                return;
            }

            if (headTransform == null || playerTransform == null)
            {
                return;
            }

            Vector3 toPlayer = playerTransform.position - headTransform.position;
            if (toPlayer.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            headTransform.rotation = Quaternion.RotateTowards(headTransform.rotation, targetRotation, headTurnSpeed * Time.deltaTime);
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
            CacheAnimatorSpeed();
            SetAnimatorSpeed(0f);
        }

        private void ExitDeadStare()
        {
            RestoreBehaviours();
            RestoreAnimatorSpeed();
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

        private void CacheAnimatorSpeed()
        {
            if (animator != null)
            {
                originalAnimatorSpeed = animator.speed;
            }
        }

        private void SetAnimatorSpeed(float speed)
        {
            if (animator != null)
            {
                animator.speed = speed;
            }
        }

        private void RestoreAnimatorSpeed()
        {
            SetAnimatorSpeed(originalAnimatorSpeed);
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
