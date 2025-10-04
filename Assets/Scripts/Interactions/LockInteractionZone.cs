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
        private int lastManualProcessFrame = -1;

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
            ClearHandles();
            progressDisplay?.SetProgress(0f);
            progressDisplay?.HideImmediate();
            lastManualProcessFrame = -1;
        }

        private void OnDisable()
        {
            ClearHandles();
            progressDisplay?.HideImmediate();
            lastManualProcessFrame = -1;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsColliderEligible(other))
            {
                return;
            }

            LockInteractionHandle handle = other.GetComponentInParent<LockInteractionHandle>();
            BeginInteraction(handle);
        }

        private void OnTriggerExit(Collider other)
        {
            LockInteractionHandle handle = other.GetComponentInParent<LockInteractionHandle>();
            if (handle == null)
            {
                return;
            }

            CancelInteraction(handle);
        }

        private void HandleReleased(LockInteractionHandle handle)
        {
            CancelInteraction(handle);
        }

        private void Update()
        {
            if (lastManualProcessFrame == Time.frameCount)
            {
                return;
            }

            ProcessStep(Time.deltaTime);
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

        /// <summary>
        /// Explicitly registers a handle as interacting with the zone. Useful when the
        /// item logic manages overlap checks manually instead of relying on trigger events.
        /// </summary>
        /// <param name="handle">Handle that should be considered active.</param>
        public bool BeginInteraction(LockInteractionHandle handle)
        {
            if (!RegisterHandle(handle))
            {
                return false;
            }

            progressDisplay?.Show();
            progressDisplay?.SetProgress(currentProgress);
            return true;
        }

        /// <summary>
        /// Advances the interaction while a handle remains in range. This mirrors the
        /// behaviour that normally occurs in <see cref="Update"/> when using trigger-driven
        /// overlaps so that external callers can drive the progression each frame.
        /// </summary>
        /// <param name="handle">The active handle.</param>
        /// <param name="deltaTime">Time slice to apply for this update.</param>
        public void ProcessInteraction(LockInteractionHandle handle, float deltaTime = -1f)
        {
            bool hasHandle = RegisterHandle(handle);

            lastManualProcessFrame = Time.frameCount;
            float stepDelta = deltaTime < 0f ? Time.deltaTime : deltaTime;
            ProcessStep(Mathf.Max(0f, stepDelta), hasHandle ? true : (bool?)null);
        }

        /// <summary>
        /// Cancels the current interaction for the provided handle, mirroring what happens
        /// when the collider exits the trigger volume.
        /// </summary>
        /// <param name="handle">Handle leaving the zone.</param>
        public void CancelInteraction(LockInteractionHandle handle = null)
        {
            if (handle == null)
            {
                ClearHandles();
                return;
            }

            handle.Released -= HandleReleased;

            if (activeHandles.Remove(handle))
            {
                UpdateInteractionState(activeHandles.Count > 0);
            }

            if (activeHandles.Count == 0 && Mathf.Approximately(currentProgress, 0f))
            {
                progressDisplay?.Hide();
            }
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
                if (handle == null)
                {
                    activeHandles.RemoveAt(i);
                    UpdateInteractionState(activeHandles.Count > 0);
                    continue;
                }

                if (!handle.IsActive)
                {
                    CancelInteraction(handle);
                }
            }

            if (activeHandles.Count == 0)
            {
                UpdateInteractionState(false);
            }
        }

        private bool IsColliderEligible(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            return IsLayerEligible(collider.gameObject.layer);
        }

        private bool IsLayerEligible(int layer)
        {
            return (interactorLayers.value & (1 << layer)) != 0;
        }

        private bool RegisterHandle(LockInteractionHandle handle)
        {
            if (handle == null || isUnlocked || !handle.IsActive)
            {
                return false;
            }

            if (!IsLayerEligible(handle.gameObject.layer))
            {
                return false;
            }

            if (!activeHandles.Contains(handle))
            {
                activeHandles.Add(handle);
                handle.Released += HandleReleased;
            }

            UpdateInteractionState(true);
            return true;
        }

        private void ClearHandles()
        {
            for (int i = activeHandles.Count - 1; i >= 0; i--)
            {
                LockInteractionHandle handle = activeHandles[i];
                if (handle != null)
                {
                    handle.Released -= HandleReleased;
                }

                activeHandles.RemoveAt(i);
            }

            UpdateInteractionState(false);

            if (Mathf.Approximately(currentProgress, 0f))
            {
                progressDisplay?.Hide();
            }
        }

        private void ProcessStep(float deltaTime, bool? overrideHandleState = null)
        {
            if (isUnlocked)
            {
                progressDisplay?.SetProgress(1f);
                progressDisplay?.Show();
                return;
            }

            RemoveInactiveHandles();

            bool hasValidHandle = overrideHandleState ?? (activeHandles.Count > 0);

            if (cooldownTimer > 0f)
            {
                cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);
                if (cooldownTimer > 0f)
                {
                    hasValidHandle = false;
                }
            }

            UpdateInteractionState(hasValidHandle);

            float previous = currentProgress;
            if (hasValidHandle)
            {
                currentProgress = Mathf.Min(1f, currentProgress + Mathf.Max(0f, fillRate) * deltaTime);
                if (Mathf.Approximately(currentProgress, 1f))
                {
                    HandleProgressComplete();
                }
            }
            else if (currentProgress > 0f)
            {
                currentProgress = Mathf.Max(0f, currentProgress - Mathf.Max(0f, decayRate) * deltaTime);
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
    }
}
