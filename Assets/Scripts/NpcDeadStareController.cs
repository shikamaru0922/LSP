using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay
{
    public enum NpcState
    {
        Normal,
        DeadStare,
        Death
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

        [Header("Death Handling")]
        [Tooltip("Monster instance used to trigger the NPC death state when it gets close.")]
        [SerializeField]
        private MonsterController monster;

        [Tooltip("Distance threshold that determines how close the monster must be to kill the NPC.")]
        [Min(0f)]
        [SerializeField]
        private float deathTriggerDistance = 1.5f;

        [Tooltip("Animator state name that should be played when the NPC dies.")]
        [SerializeField]
        private string deathAnimationStateName = "Death";

        [Tooltip("Optional animation clip used to determine how long to wait before freezing the animator.")]
        [SerializeField]
        private AnimationClip deathAnimationClip;

        [Tooltip("Optional audio clip that is played when the NPC dies.")]
        [SerializeField]
        private AudioClip deathAudioClip;

        [SerializeField]
        private AudioSource deathAudioSource;

        private NpcState currentState = NpcState.Normal;
        private readonly List<bool> cachedBehaviourStates = new List<bool>();
        private float originalAnimatorSpeed = 1f;
        private bool originalAnimatorEnabled = true;
        private bool isSubscribed;
        private bool hasCachedWorldState;
        private bool lastKnownWorldAbnormal;
        private Coroutine deathAnimationRoutine;
        private int lastPlayedDeathStateHash;
        private bool hasLastPlayedDeathState;

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

            if (deathAudioSource == null)
            {
                deathAudioSource = GetComponent<AudioSource>();
                if (deathAudioSource == null)
                {
                    deathAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (deathAudioSource != null)
            {
                deathAudioSource.playOnAwake = false;
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

            ResolveMonsterReference();
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

            if (deathAnimationRoutine != null)
            {
                StopCoroutine(deathAnimationRoutine);
                deathAnimationRoutine = null;
            }
        }

        private void Update()
        {
            if (currentState == NpcState.Death)
            {
                return;
            }

            RefreshWorldState(false);
            CheckMonsterProximity();

            if (currentState == NpcState.Death)
            {
                return;
            }

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
            if (currentState == NpcState.Death)
            {
                return;
            }

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
            if (currentState == NpcState.Death)
            {
                return;
            }

            if (currentState == newState)
            {
                return;
            }

            currentState = newState;

            switch (currentState)
            {
                case NpcState.DeadStare:
                    EnterDeadStare();
                    break;
                case NpcState.Normal:
                    ExitDeadStare();
                    break;
                case NpcState.Death:
                    EnterDeath();
                    break;
            }
        }

        private void EnterDeadStare()
        {
            CacheBehaviourStates();
            DisableBehaviours();
            CacheAnimatorState();
            DisableAnimator();
        }

        private void ExitDeadStare()
        {
            RestoreBehaviours();
            RestoreAnimatorState();
        }

        private void EnterDeath()
        {
            DisableBehaviours();
            UnsubscribeFromManager();
            PlayDeathAudio();
            PlayDeathAnimation();
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

        private void ResolveMonsterReference()
        {
            if (monster != null)
            {
                return;
            }

            monster = FindObjectOfType<MonsterController>();
        }

        private void CheckMonsterProximity()
        {
            ResolveMonsterReference();

            if (monster == null || monster.CurrentState != MonsterState.Chasing)
            {
                return;
            }

            float distanceThreshold = Mathf.Max(0f, deathTriggerDistance);
            float thresholdSqr = distanceThreshold * distanceThreshold;
            Vector3 difference = monster.transform.position - transform.position;

            if (difference.sqrMagnitude <= thresholdSqr)
            {
                SetState(NpcState.Death);
            }
        }

        private void PlayDeathAudio()
        {
            if (deathAudioSource == null)
            {
                return;
            }

            if (deathAudioClip != null)
            {
                deathAudioSource.PlayOneShot(deathAudioClip);
            }
        }

        private void PlayDeathAnimation()
        {
            if (animator == null)
            {
                return;
            }

            animator.enabled = true;
            animator.speed = 1f;

            if (deathAnimationRoutine != null)
            {
                StopCoroutine(deathAnimationRoutine);
                deathAnimationRoutine = null;
            }

            string stateToPlay = !string.IsNullOrEmpty(deathAnimationStateName)
                ? deathAnimationStateName
                : deathAnimationClip != null
                    ? deathAnimationClip.name
                    : string.Empty;

            float clipLength = deathAnimationClip != null ? deathAnimationClip.length : 0f;

            if (!string.IsNullOrEmpty(stateToPlay))
            {
                int stateHash = Animator.StringToHash(stateToPlay);
                hasLastPlayedDeathState = animator.HasState(0, stateHash);

                if (hasLastPlayedDeathState)
                {
                    animator.Play(stateHash, 0, 0f);
                }
                else
                {
                    animator.Play(stateToPlay, 0, 0f);
                }

                animator.Update(0f);
                lastPlayedDeathStateHash = stateHash;

                if (clipLength <= 0f)
                {
                    AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    clipLength = stateInfo.length;
                }
            }
            else
            {
                hasLastPlayedDeathState = false;
                lastPlayedDeathStateHash = 0;
            }

            if (clipLength > 0f)
            {
                deathAnimationRoutine = StartCoroutine(FreezeAnimatorAfter(clipLength));
            }
            else
            {
                SnapAnimatorToLastFrame();
            }
        }

        private IEnumerator FreezeAnimatorAfter(float delay)
        {
            yield return new WaitForSeconds(delay);

            SnapAnimatorToLastFrame();
            deathAnimationRoutine = null;
        }

        private void SnapAnimatorToLastFrame()
        {
            if (animator == null)
            {
                return;
            }

            if (hasLastPlayedDeathState)
            {
                animator.Play(lastPlayedDeathStateHash, 0, 1f);
            }
            else
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                animator.Play(stateInfo.fullPathHash, 0, 1f);
            }

            animator.Update(0f);
            DisableAnimator();
        }
    }
}
