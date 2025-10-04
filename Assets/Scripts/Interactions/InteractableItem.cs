using System.Collections.Generic;
using LSP.Interactions;
using UnityEngine;

namespace LSP.Gameplay.Interactions
{
    /// <summary>
    /// Represents an interactable pickup item in the world that can either be carried or consumed.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractableItem : MonoBehaviour, IInteractable
    {
        public enum PickupMode
        {
            FloatingCarry,
            ConsumableFragment
        }

        [Header("Pickup Behaviour")]
        [SerializeField]
        private PickupMode pickupMode = PickupMode.FloatingCarry;

        [SerializeField]
        [Tooltip("Number of fragments granted to the disabler device when consumed.")]
        private int fragmentValue = 1;

        [Header("Carry Anchor Pose")]
        [SerializeField]
        private Vector3 carriedLocalPosition;

        [SerializeField]
        private Vector3 carriedLocalEulerAngles;

        [SerializeField]
        private Vector3 carriedLocalScale = Vector3.one;

        [SerializeField]
        [Tooltip("Optional reference transform whose pose can be copied into the carried pose settings.")]
        private Transform carryPoseReference;

        [Header("Lock Interaction")]
        [SerializeField]
        [Tooltip("Radius around the item to check for lock interaction trigger volumes while carried.")]
        private float lockDetectionRadius = 0.5f;

        [SerializeField]
        [Tooltip("Layers considered when searching for lock interaction volumes.")]
        private LayerMask lockZoneLayers = ~0;

        [SerializeField]
        [Tooltip("Optional tag filter applied when identifying lock interaction zones.")]
        private string lockZoneTag = "LockInteraction";

        [SerializeField]
        [Tooltip("Colliders that should become active while the item is carried to support trigger-only interactions.")]
        private Collider[] autoInteractionColliders;

        private readonly List<LockInteractionZone> activeLockZones = new List<LockInteractionZone>();
        private readonly HashSet<LockInteractionZone> lockZoneBuffer = new HashSet<LockInteractionZone>();
        private readonly Collider[] overlapResults = new Collider[8];

        private PlayerInteractionController currentCarrier;
        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private bool isCarried;

        /// <summary>
        /// Gets a value indicating whether the item is currently being carried by a player.
        /// </summary>
        public bool IsCarried => isCarried;

        private void Awake()
        {
            CacheOriginalTransform();
            ConfigureAutoInteractionColliders(false);
        }

        private void OnDisable()
        {
            if (currentCarrier != null)
            {
                currentCarrier.NotifyItemReleased(this);
            }

            ClearLockZones();
        }

        private void Update()
        {
            if (!isCarried)
            {
                return;
            }

            UpdateLockZoneTracking();
        }

        /// <inheritdoc />
        public bool CanInteract(PlayerInteractionController caller)
        {
            if (pickupMode == PickupMode.FloatingCarry)
            {
                return !isCarried || currentCarrier == caller;
            }

            return true;
        }

        /// <inheritdoc />
        public void Interact(PlayerInteractionController caller)
        {
            if (pickupMode == PickupMode.FloatingCarry)
            {
                if (isCarried)
                {
                    if (currentCarrier == caller)
                    {
                        ReleaseFromCarrier();
                    }
                    return;
                }

                BeginCarry(caller);
                return;
            }

            Consume(caller);
        }

        internal void HandleInteractWhileCarried(PlayerInteractionController caller)
        {
            if (pickupMode == PickupMode.FloatingCarry && currentCarrier == caller)
            {
                ReleaseFromCarrier();
            }
        }

        internal void ReleaseFromCarrier()
        {
            if (!isCarried)
            {
                return;
            }

            isCarried = false;

            if (currentCarrier != null)
            {
                currentCarrier.NotifyItemReleased(this);
            }

            transform.SetParent(originalParent, false);
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
            transform.localScale = originalLocalScale;

            currentCarrier = null;
            ConfigureAutoInteractionColliders(false);
            ClearLockZones();
        }

        internal void NotifyLockInteractionComplete(LockInteractionZone zone)
        {
            if (activeLockZones.Contains(zone))
            {
                zone.CancelInteraction(this);
                activeLockZones.Remove(zone);
            }
        }

        private void BeginCarry(PlayerInteractionController caller)
        {
            if (caller == null)
            {
                return;
            }

            CacheOriginalTransform();

            currentCarrier = caller;
            currentCarrier.NotifyItemCarried(this);

            var anchor = caller.CarryAnchor;
            transform.SetParent(anchor, false);
            transform.localPosition = carriedLocalPosition;
            transform.localRotation = Quaternion.Euler(carriedLocalEulerAngles);
            transform.localScale = carriedLocalScale;

            isCarried = true;
            ConfigureAutoInteractionColliders(true);
        }

        private void Consume(PlayerInteractionController caller)
        {
            if (fragmentValue > 0 && caller != null && caller.DisablerDevice != null)
            {
                caller.DisablerDevice.AddRepairFragments(fragmentValue);
            }

            Destroy(gameObject);
        }

        private void CacheOriginalTransform()
        {
            originalParent = transform.parent;
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;
            originalLocalScale = transform.localScale;
        }

        private void UpdateLockZoneTracking()
        {
            if (lockDetectionRadius <= 0f)
            {
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, lockDetectionRadius, overlapResults, lockZoneLayers, QueryTriggerInteraction.Collide);
            lockZoneBuffer.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                var collider = overlapResults[i];
                if (collider == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(lockZoneTag) && !collider.CompareTag(lockZoneTag))
                {
                    continue;
                }

                if (!collider.TryGetComponent(out LockInteractionZone zone))
                {
                    continue;
                }

                lockZoneBuffer.Add(zone);
                if (!activeLockZones.Contains(zone))
                {
                    activeLockZones.Add(zone);
                    zone.BeginInteraction(this);
                }

                zone.ProcessInteraction(this, Time.deltaTime);
            }

            for (int i = activeLockZones.Count - 1; i >= 0; i--)
            {
                var zone = activeLockZones[i];
                if (!lockZoneBuffer.Contains(zone))
                {
                    zone.CancelInteraction(this);
                    activeLockZones.RemoveAt(i);
                }
            }
        }

        private void ConfigureAutoInteractionColliders(bool enabled)
        {
            if (autoInteractionColliders == null)
            {
                return;
            }

            foreach (var collider in autoInteractionColliders)
            {
                if (collider == null)
                {
                    continue;
                }

                collider.enabled = enabled;
            }
        }

        private void ClearLockZones()
        {
            for (int i = activeLockZones.Count - 1; i >= 0; i--)
            {
                var zone = activeLockZones[i];
                zone?.CancelInteraction(this);
            }

            activeLockZones.Clear();
            lockZoneBuffer.Clear();
        }

#if UNITY_EDITOR
        [ContextMenu("Copy Carry Pose From Reference")]
        private void CopyCarryPoseFromReference()
        {
            if (carryPoseReference == null)
            {
                Debug.LogWarning($"{name}: Carry pose reference is not assigned.", this);
                return;
            }

            carriedLocalPosition = carryPoseReference.localPosition;
            carriedLocalEulerAngles = carryPoseReference.localEulerAngles;
            carriedLocalScale = carryPoseReference.localScale;
        }

        [ContextMenu("Move Item To Carry Pose Reference")]
        private void MoveItemToCarryPoseReference()
        {
            if (carryPoseReference == null)
            {
                Debug.LogWarning($"{name}: Carry pose reference is not assigned.", this);
                return;
            }

            transform.SetPositionAndRotation(carryPoseReference.position, carryPoseReference.rotation);
            transform.localScale = carryPoseReference.localScale;
        }
#endif
    }
}