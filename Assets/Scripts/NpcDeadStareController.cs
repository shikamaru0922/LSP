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

        // 原来只缓存了 speed；现在同时缓存 enabled 状态
        private float originalAnimatorSpeed = 1f;
        private bool originalAnimatorEnabled = true;

        private bool isSubscribed;

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
            ApplyWorldState(GameManager.Instance != null && GameManager.Instance.IsWorldAbnormal);
        }

        private void OnDisable()
        {
            UnsubscribeFromManager();
            RestoreBehaviours();
            RestoreAnimatorState(); // 恢复 Animator 的启用与速度
            currentState = NpcState.Normal;
        }

        private void Update()
        {
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
            if (isSubscribed) return;
            GameManager.WorldAbnormalStateChanged += ApplyWorldState;
            isSubscribed = true;
        }

        private void UnsubscribeFromManager()
        {
            if (!isSubscribed) return;
            GameManager.WorldAbnormalStateChanged -= ApplyWorldState;
            isSubscribed = false;
        }

        private void ApplyWorldState(bool isWorldAbnormal)
        {
            SetState(isWorldAbnormal ? NpcState.DeadStare : NpcState.Normal);
        }

        private void SetState(NpcState newState)
        {
            if (currentState == newState) return;

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

            CacheAnimatorState();     // 先缓存
            DisableAnimator();        // ★ 直接禁用 Animator（满足你的“先直接把 animator 不启用”的需求）
        }

        private void ExitDeadStare()
        {
            RestoreBehaviours();
            RestoreAnimatorState();    // 还原 Animator 的 enabled 与 speed
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
                if (behaviour == null) continue;

                bool enabledState = i < cachedBehaviourStates.Count && cachedBehaviourStates[i];
                behaviour.enabled = enabledState;
            }
        }

        // —— Animator 相关：从只管 speed 升级为同时管理 enabled 与 speed ——
        private void CacheAnimatorState()
        {
            if (animator == null) return;
            originalAnimatorEnabled = animator.enabled;
            originalAnimatorSpeed = animator.speed;
        }

        private void DisableAnimator()
        {
            if (animator == null) return;

            // 直接禁用 Animator，确保完全停止动画/根运动/状态机
            animator.enabled = false;

            // 如果你更希望“禁用 + 避免某些系统在下一帧又启用它”，
            // 也可以顺带将 speed 置 0 作为兜底（但在 enabled=false 时 speed 实际不会生效）
            animator.speed = 0f;
        }

        private void RestoreAnimatorState()
        {
            if (animator == null) return;
            animator.enabled = originalAnimatorEnabled;
            animator.speed = originalAnimatorSpeed;
        }

        public void SetPlayerTransform(Transform player) => playerTransform = player;

        public void SetHeadTransform(Transform head) => headTransform = head;

        public void SetAnimator(Animator targetAnimator) => animator = targetAnimator;

        public void SetBehavioursToDisable(List<Behaviour> behaviours)
        {
            behavioursToDisable = behaviours ?? new List<Behaviour>();
            CacheBehaviourStates();
        }
    }
}
