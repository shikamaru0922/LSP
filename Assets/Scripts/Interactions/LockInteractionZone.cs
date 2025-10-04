using System.Collections.Generic;
using LSP.UI;
using UnityEngine;

namespace LSP.Interactions
{
    /// <summary>
    /// Handles the progress logic for card/head scanning locks and drives a CanvasGroup-based UI display.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LockInteractionZone : MonoBehaviour
    {
        [SerializeField]
        private ProgressCanvasGroup progressDisplay;

        [SerializeField]
        [Tooltip("Progress accumulated per second while a valid handle overlaps the zone.")]
        private float fillRate = 0.5f;

        [SerializeField]
        [Tooltip("Progress drained per second while no handle is overlapping.")]
        private float decayRate = 1.5f;

        [SerializeField]
        [Tooltip("How long to wait after a forced failure before progress can resume.")]
        private float failureCooldown = 0.5f;

        [SerializeField]
        [Tooltip("Number of times the progress bar must reach full before the lock finally opens.")]
        private int forcedFailures = 0;

        [SerializeField]
        [Tooltip("Optional layer mask filter so only specific objects can interact with the zone.")]
        private LayerMask interactorLayers = ~0;

        private readonly List<LockInteractionHandle> activeHandles = new();
        private float currentProgress;
        private float cooldownTimer;
        private int failuresConsumed;
        private bool interactionActive;
        private bool isUnlocked;

        public float Progress => currentProgress;
        public bool IsUnlocked => isUnlocked;

        public event System.Action<float> ProgressChanged;
        public event System.Action<bool> InteractionActiveChanged;
        public event System.Action ForcedFailureTriggered;
        public event System.Action ZoneUnlocked;

        private void Reset()
        {
            if (TryGetComponent(out Collider trigger))
            {
                trigger.isTrigger = true;
            }
        }

        private void Awake()
        {
            if (progressDisplay == null)
            {
                progressDisplay = GetComponentInChildren<ProgressCanvasGroup>(true);
            }

            progressDisplay?.HideImmediate();
        }

        private void OnEnable()
        {
            failuresConsumed = 0;
            cooldownTimer = 0f;
            isUnlocked = false;
            currentProgress = 0f;
            interactionActive = false;
            activeHandles.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsColliderEligible(other))
            {
                return;
            }

            LockInteractionHandle handle = other.GetComponentInParent<LockInteractionHandle>();
            if (handle == null || activeHandles.Contains(handle))
            {
                return;
            }

            if (!handle.IsActive)
            {
                return;
            }

            activeHandles.Add(handle);
            handle.Released += HandleReleased;
            UpdateInteractionState(true);
        }

        private void OnTriggerExit(Collider other)
        {
            LockInteractionHandle handle = other.GetComponentInParent<LockInteractionHandle>();
            if (handle == null)
            {
                return;
            }

            if (activeHandles.Remove(handle))
            {
                handle.Released -= HandleReleased;
                UpdateInteractionState(activeHandles.Count > 0);
            }
        }

        private void HandleReleased(LockInteractionHandle handle)
        {
            handle.Released -= HandleReleased;
            if (activeHandles.Remove(handle))
            {
                UpdateInteractionState(activeHandles.Count > 0);
            }
        }

        private void Update()
        {
            if (isUnlocked)
            {
                progressDisplay?.SetProgress(1f);
                progressDisplay?.Show();
                return;
            }

            RemoveInactiveHandles();

            bool hasValidHandle = activeHandles.Count > 0;
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f)
                {
                    cooldownTimer = 0f;
                }
                else
                {
                    hasValidHandle = false;
                }
            }

            UpdateInteractionState(hasValidHandle);

            float previous = currentProgress;
            if (hasValidHandle)
            {
                currentProgress = Mathf.Min(1f, currentProgress + fillRate * Time.deltaTime);
                if (Mathf.Approximately(currentProgress, 1f))
                {
                    HandleProgressComplete();
                }
            }
            else if (currentProgress > 0f)
            {
                currentProgress = Mathf.Max(0f, currentProgress - decayRate * Time.deltaTime);
            }

            if (!Mathf.Approximately(previous, currentProgress))
            {
                ProgressChanged?.Invoke(currentProgress);
                progressDisplay?.SetProgress(currentProgress);
            }

            if (currentProgress > 0f || hasValidHandle)
            {
                progressDisplay?.Show();
            }
            else
            {
                progressDisplay?.Hide();
            }
        }

        private void HandleProgressComplete()
        {
            if (forcedFailures > 0 && failuresConsumed < forcedFailures)
            {
                failuresConsumed++;
                ForcedFailureTriggered?.Invoke();
                currentProgress = 0f;
                cooldownTimer = failureCooldown;
                progressDisplay?.SetProgress(0f);
                return;
            }

            isUnlocked = true;
            ZoneUnlocked?.Invoke();
            progressDisplay?.SetProgress(1f);
        }

        private void UpdateInteractionState(bool state)
        {
            if (interactionActive == state)
            {
                return;
            }

            interactionActive = state;
            InteractionActiveChanged?.Invoke(interactionActive);
            if (interactionActive)
            {
                progressDisplay?.Show();
            }
        }

        private void RemoveInactiveHandles()
        {
            for (int i = activeHandles.Count - 1; i >= 0; i--)
            {
                LockInteractionHandle handle = activeHandles[i];
                if (handle == null || !handle.IsActive)
                {
                    if (handle != null)
                    {
                        handle.Released -= HandleReleased;
                    }

                    activeHandles.RemoveAt(i);
                }
            }
        }

        private bool IsColliderEligible(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            return (interactorLayers.value & (1 << collider.gameObject.layer)) != 0;
        }
    }
}
