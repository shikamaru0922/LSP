using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay
{
    public enum NpcState
    {
        Normal,
        DeadStare
    }

    public class NpcDeadStareController : MonoBehaviour
    {
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Animator animator;

        [Tooltip("Optional behaviours that should be disabled when entering the dead stare state.")]
        [SerializeField] private List<Behaviour> behavioursToDisable = new List<Behaviour>();

        [Tooltip("Degrees per second used when rotating the head to follow the player.")]
        [Min(0f)][SerializeField] private float headTurnSpeed = 360f;

        private NpcState currentState = NpcState.Normal;
        private readonly List<bool> cachedBehaviourStates = new List<bool>();

        // 新增：缓存 Animator 的启用状态 + 速度
        private bool  originalAnimatorEnabled = true;
        private float originalAnimatorSpeed   = 1f;

        private bool isSubscribed;
        private bool hasCachedWorldState;
        private bool lastKnownWorldAbnormal;

        public NpcState CurrentState => currentState;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (headTransform == null && animator != null) headTransform = animator.transform;
            CacheBehaviourStates();
        }

        private void Start()
        {
            if (playerTransform == null)
            {
                var playerState = FindObjectOfType<PlayerStateController>();
                if (playerState != null) playerTransform = playerState.transform;
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
            RestoreAnimatorState();       // 恢复 Animator 的 enabled + speed
            currentState = NpcState.Normal;
            hasCachedWorldState = false;
        }

        private void Update()
        {
            RefreshWorldState(false);

            if (currentState != NpcState.DeadStare) return;
            if (headTransform == null || playerTransform == null) return;

            Vector3 toPlayer = playerTransform.position - headTransform.position;
            if (toPlayer.sqrMagnitude <= Mathf.Epsilon) return;

            Quaternion targetRotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            headTransform.rotation = Quaternion.RotateTowards(headTransform.rotation, targetRotation, headTurnSpeed * Time.deltaTime);
        }

        private void CacheBehaviourStates()
        {
            cachedBehaviourStates.Clear();
            foreach (var behaviour in behavioursToDisable)
                cachedBehaviourStates.Add(behaviour != null && behaviour.enabled);
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
            hasCachedWorldState = true;
            lastKnownWorldAbnormal = isWorldAbnormal;
            SetState(isWorldAbnormal ? NpcState.DeadStare : NpcState.Normal);
        }

        private void RefreshWorldState(bool forceApply)
        {
            GameManager manager = GameManager.Instance;
            bool managerState = manager != null && manager.IsWorldAbnormal;

            if (!hasCachedWorldState || forceApply || managerState != lastKnownWorldAbnormal)
                ApplyWorldState(managerState);
        }

        private void SetState(NpcState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            if (currentState == NpcState.DeadStare) EnterDeadStare();
            else                                     ExitDeadStare();
        }

        private void EnterDeadStare()
        {
            CacheBehaviourStates();
            DisableBehaviours();
            CacheAnimatorState();
            DisableAnimator();               // ★ 直接禁用 Animator 组件
        }

        private void ExitDeadStare()
        {
            RestoreBehaviours();
            RestoreAnimatorState();          // 还原 enabled 与 speed
        }

        private void DisableBehaviours()
        {
            for (int i = 0; i < behavioursToDisable.Count; i++)
                if (behavioursToDisable[i] != null)
                    behavioursToDisable[i].enabled = false;
        }

        private void RestoreBehaviours()
        {
            for (int i = 0; i < behavioursToDisable.Count; i++)
            {
                var behaviour = behavioursToDisable[i];
                if (behaviour == null) continue;
                bool enabledState = i < cachedBehaviourStates.Count && cachedBehaviourStates[i];
                behaviour.enabled = enabledState;
            }
        }

        // ===== Animator 状态管理（enabled + speed）=====
        private void CacheAnimatorState()
        {
            if (animator == null) return;
            originalAnimatorEnabled = animator.enabled;
            originalAnimatorSpeed   = animator.speed;
        }

        private void DisableAnimator()
        {
            if (animator == null) return;
            animator.enabled = false;   // 彻底禁用
            animator.speed   = 0f;      // 兜底（即使 disabled 时不会运行）
        }

        private void RestoreAnimatorState()
        {
            if (animator == null) return;
            animator.enabled = originalAnimatorEnabled;
            animator.speed   = originalAnimatorSpeed;
        }
        // ============================================

        public void SetPlayerTransform(Transform player) => playerTransform = player;
        public void SetHeadTransform(Transform head)     => headTransform = head;
        public void SetAnimator(Animator targetAnimator) => animator = targetAnimator;

        public void SetBehavioursToDisable(List<Behaviour> behaviours)
        {
            behavioursToDisable = behaviours ?? new List<Behaviour>();
            CacheBehaviourStates();
        }
    }
}
