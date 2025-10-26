using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LSP.Gameplay.Interactions
{
    /// <summary>
    /// Aggregates multiple <see cref="GravitySwitch"/> instances and raises events when
    /// a configured number of them are pressed simultaneously. This allows puzzles to
    /// require players and monsters to cooperate across chained switches.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("LSP/Interactions/Gravity Switch Group")]
    public class GravitySwitchGroup : MonoBehaviour
    {
        [System.Serializable]
        public class GroupStateEvent : UnityEvent<bool>
        {
        }

        [Tooltip("Switches monitored by this group. Duplicates are ignored at runtime.")]
        [SerializeField]
        private GravitySwitch[] switches;

        [Tooltip("How many switches must be pressed for the group to activate. If set to 0 the group requires all configured switches.")]
        [Min(0)]
        [SerializeField]
        private int requiredActiveSwitches;

        [Header("Events")]
        [SerializeField]
        private GroupStateEvent stateChanged = new GroupStateEvent();

        [SerializeField]
        private UnityEvent onActivated = new UnityEvent();

        [SerializeField]
        private UnityEvent onDeactivated = new UnityEvent();

        private readonly List<GravitySwitch> uniqueSwitches = new List<GravitySwitch>();

        /// <summary>
        /// True when the group has reached the required active count.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// UnityEvent exposed for designers to respond when the group activation state changes.
        /// </summary>
        public GroupStateEvent StateChangedEvent => stateChanged;

        /// <summary>
        /// UnityEvent exposed for designers to respond when the group activates.
        /// </summary>
        public UnityEvent OnActivatedEvent => onActivated;

        /// <summary>
        /// UnityEvent exposed for designers to respond when the group deactivates.
        /// </summary>
        public UnityEvent OnDeactivatedEvent => onDeactivated;

        private void OnEnable()
        {
            RefreshSubscriptions();
            EvaluateState();
        }

        private void OnDisable()
        {
            ClearSubscriptions();
            uniqueSwitches.Clear();
        }

        private void RefreshSubscriptions()
        {
            ClearSubscriptions();
            uniqueSwitches.Clear();

            if (switches == null)
            {
                return;
            }

            foreach (var gravitySwitch in switches)
            {
                if (gravitySwitch == null || uniqueSwitches.Contains(gravitySwitch))
                {
                    continue;
                }

                uniqueSwitches.Add(gravitySwitch);
                gravitySwitch.PressedStateChanged += HandleSwitchStateChanged;
            }
        }

        private void ClearSubscriptions()
        {
            foreach (var gravitySwitch in uniqueSwitches)
            {
                if (gravitySwitch != null)
                {
                    gravitySwitch.PressedStateChanged -= HandleSwitchStateChanged;
                }
            }
        }

        private void HandleSwitchStateChanged(GravitySwitch _)
        {
            EvaluateState();
        }

        private void EvaluateState()
        {
            if (uniqueSwitches.Count == 0)
            {
                SetActive(false);
                return;
            }

            var target = requiredActiveSwitches <= 0
                ? uniqueSwitches.Count
                : Mathf.Min(requiredActiveSwitches, uniqueSwitches.Count);

            var activeCount = 0;
            for (var i = 0; i < uniqueSwitches.Count; i++)
            {
                if (uniqueSwitches[i] != null && uniqueSwitches[i].IsPressed)
                {
                    activeCount++;
                }
            }

            SetActive(activeCount >= target);
        }

        private void SetActive(bool active)
        {
            if (IsActive == active)
            {
                return;
            }

            IsActive = active;
            stateChanged?.Invoke(IsActive);

            if (IsActive)
            {
                onActivated?.Invoke();
            }
            else
            {
                onDeactivated?.Invoke();
            }
        }
    }
}
