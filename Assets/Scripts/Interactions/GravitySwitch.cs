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
    public class GravitySwitch : MonoBehaviour
    {
        [Serializable]
        public class SwitchStateEvent : UnityEvent<bool>
        {
        }

        [Header("Events")]
        [SerializeField]
        [Tooltip("Invoked whenever the pressed state changes.")]
        private SwitchStateEvent stateChanged;

        [SerializeField]
        [Tooltip("Raised when the switch is pressed.")]
        private UnityEvent onPressed;

        [SerializeField]
        [Tooltip("Raised when the switch is released.")]
        private UnityEvent onReleased;

        private readonly HashSet<Collider> activeColliders = new HashSet<Collider>();

        /// <summary>
        /// Current pressed state of the switch.
        /// </summary>
        public bool IsPressed { get; private set; }

        /// <summary>
        /// Event fired whenever <see cref="IsPressed"/> changes.
        /// </summary>
        public event Action<GravitySwitch> PressedStateChanged;

        private void Reset()
        {
            var triggerCollider = GetComponent<Collider>();
            triggerCollider.isTrigger = true;
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
