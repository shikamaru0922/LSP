using System;
using System.Collections.Generic;
using LSP.Gameplay;
using UnityEngine;
using UnityEngine.Events;

namespace LSP.Gameplay.Interactions
{
    /// <summary>
    /// Represents a floor switch that reacts to the player or monster standing on it.
    /// The switch raises events when pressed or released and exposes its state to
    /// other systems such as chained logic gates.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("LSP/Interactions/Gravity Switch")]
    public class GravitySwitch : MonoBehaviour
    {
        [Serializable]
        public class SwitchStateEvent : UnityEvent<bool>
        {
        }

        [Header("Events")]
        [SerializeField]
        [Tooltip("Invoked whenever the pressed state changes.")]
        private SwitchStateEvent stateChanged = new SwitchStateEvent();

        [SerializeField]
        [Tooltip("Raised when the switch is pressed.")]
        private UnityEvent onPressed = new UnityEvent();

        [SerializeField]
        [Tooltip("Raised when the switch is released.")]
        private UnityEvent onReleased = new UnityEvent();

        private readonly HashSet<Collider> activeColliders = new HashSet<Collider>();

        /// <summary>
        /// Current pressed state of the switch.
        /// </summary>
        public bool IsPressed { get; private set; }

        /// <summary>
        /// Event fired whenever <see cref="IsPressed"/> changes.
        /// </summary>
        public event Action<GravitySwitch> PressedStateChanged;

        /// <summary>
        /// UnityEvent exposed for designers to respond to pressed state changes.
        /// </summary>
        public SwitchStateEvent StateChangedEvent => stateChanged;

        /// <summary>
        /// UnityEvent exposed for designers to respond when the switch is pressed.
        /// </summary>
        public UnityEvent OnPressedEvent => onPressed;

        /// <summary>
        /// UnityEvent exposed for designers to respond when the switch is released.
        /// </summary>
        public UnityEvent OnReleasedEvent => onReleased;

        private void Reset()
        {
            var triggerCollider = GetComponent<Collider>();
            triggerCollider.isTrigger = true;
        }

        private void OnValidate()
        {
            var triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void FixedUpdate()
        {
            if (activeColliders.Count == 0)
            {
                return;
            }

            if (RemoveInvalidColliders() && activeColliders.Count == 0)
            {
                SetPressed(false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsValidActivator(other))
            {
                return;
            }

            if (activeColliders.Add(other))
            {
                SetPressed(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!activeColliders.Remove(other))
            {
                return;
            }

            if (activeColliders.Count == 0)
            {
                SetPressed(false);
            }
        }

        private void OnDisable()
        {
            if (activeColliders.Count == 0 && !IsPressed)
            {
                return;
            }

            activeColliders.Clear();
            SetPressed(false);
        }

        /// <summary>
        /// Forces the pressed state without requiring a collider.
        /// </summary>
        public void ForceSetPressed(bool pressed)
        {
            activeColliders.Clear();
            SetPressed(pressed);
        }

        private bool RemoveInvalidColliders()
        {
            var removedAny = false;

            activeColliders.RemoveWhere(collider =>
            {
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                {
                    return false;
                }

                removedAny = true;
                return true;
            });

            return removedAny;
        }

        private bool IsValidActivator(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.GetComponentInParent<PlayerStateController>() != null)
            {
                return true;
            }

            if (collider.GetComponentInParent<MonsterController>() != null)
            {
                return true;
            }

            return false;
        }

        private void SetPressed(bool pressed)
        {
            if (IsPressed == pressed)
            {
                return;
            }

            IsPressed = pressed;
            stateChanged?.Invoke(IsPressed);

            if (IsPressed)
            {
                onPressed?.Invoke();
            }
            else
            {
                onReleased?.Invoke();
            }

            PressedStateChanged?.Invoke(this);
        }
    }
}
