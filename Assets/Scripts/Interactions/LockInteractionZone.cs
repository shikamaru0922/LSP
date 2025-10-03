using UnityEngine;
using UnityEngine.Events;

namespace LSP.Gameplay.Interactions
{
    /// <summary>
    /// Represents a trigger volume that can be completed by holding a carried item within it.
    /// </summary>
    [DisallowMultipleComponent]
    public class LockInteractionZone : MonoBehaviour
    {
        [System.Serializable]
        public class ProgressEvent : UnityEvent<float>
        {
        }

        [Header("Progress Settings")]
        [SerializeField]
        [Tooltip("Seconds the carried item must remain in the zone to complete the lock interaction.")]
        private float requiredHoldTime = 2f;

        [Header("Events")]
        [SerializeField]
        private ProgressEvent progressUpdated;

        [SerializeField]
        private UnityEvent onLockCompleted;

        private InteractableItem activeItem;
        private float currentProgress;

        /// <summary>
        /// Begins tracking the supplied item within the lock zone.
        /// </summary>
        public void BeginInteraction(InteractableItem item)
        {
            if (item == null)
            {
                return;
            }

            if (activeItem != null && activeItem != item)
            {
                return;
            }

            activeItem = item;
            currentProgress = Mathf.Clamp(currentProgress, 0f, requiredHoldTime);
            NotifyProgress();
        }

        /// <summary>
        /// Processes an interaction tick for the active item.
        /// </summary>
        public void ProcessInteraction(InteractableItem item, float deltaTime)
        {
            if (item == null || item != activeItem)
            {
                return;
            }

            if (requiredHoldTime <= 0f)
            {
                CompleteInteraction();
                return;
            }

            currentProgress = Mathf.Min(requiredHoldTime, currentProgress + Mathf.Max(0f, deltaTime));
            NotifyProgress();

            if (currentProgress >= requiredHoldTime)
            {
                CompleteInteraction();
            }
        }

        /// <summary>
        /// Cancels the current interaction attempt, resetting progress if the supplied item matches the active one.
        /// </summary>
        public void CancelInteraction(InteractableItem item)
        {
            if (item == null || item != activeItem)
            {
                return;
            }

            activeItem = null;
            currentProgress = 0f;
            NotifyProgress();
        }

        private void CompleteInteraction()
        {
            onLockCompleted?.Invoke();
            activeItem?.NotifyLockInteractionComplete(this);
            activeItem = null;
            currentProgress = 0f;
            NotifyProgress();
        }

        private void NotifyProgress()
        {
            if (progressUpdated == null)
            {
                return;
            }

            float target = requiredHoldTime <= 0f ? 1f : Mathf.Clamp01(currentProgress / requiredHoldTime);
            progressUpdated.Invoke(target);
        }
    }
}
