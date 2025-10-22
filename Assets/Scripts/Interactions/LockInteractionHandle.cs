using UnityEngine;

namespace LSP.Interactions
{
    /// <summary>
    /// Marks an object that can be used to interact with a <see cref="LockInteractionZone"/>.
    /// Attach this component to the collider that should trigger the lock while the item is carried.
    /// </summary>
    public class LockInteractionHandle : MonoBehaviour
    {
        /// <summary>
        /// Raised when the handle becomes inactive (disabled or destroyed) so zones can discard it.
        /// </summary>
        public event System.Action<LockInteractionHandle> Released;

        /// <summary>
        /// Whether this handle should currently count as a valid interactor.
        /// </summary>
        public bool IsActive => isActiveAndEnabled && gameObject.activeInHierarchy;

        private void OnDisable()
        {
            Released?.Invoke(this);
        }

        private void OnDestroy()
        {
            Released?.Invoke(this);
        }
    }
}
