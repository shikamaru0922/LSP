using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Coordinates the interaction between the prefab's pre-existing controllers and the
    /// new survival mechanics. By referencing the existing movement behaviours the
    /// coordinator can temporarily disable them when the player dies or is forced to
    /// close their eyes, without modifying the original scripts.
    /// </summary>
    public class PlayerMechanicsCoordinator : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField]
        private PlayerStateController stateController;

        [SerializeField]
        private PlayerEyeControl eyeControl;

        [Tooltip("Behaviour scripts that control the player's movement on the prefab.")]
        [SerializeField]
        private MonoBehaviour[] movementBehaviours;

        [SerializeField]
        private PlayerInteractionController interactionController;

        [SerializeField]
        private DisablerDevice disablerDevice;

        [Tooltip("Disable player movement whenever a forced blink is active.")]
        [SerializeField]
        private bool lockMovementDuringForcedBlink = true;

        private bool[] movementInitiallyEnabled;
        private bool movementCurrentlyEnabled;
        private bool movementStateInitialised;

        private void Awake()
        {
            if (stateController == null)
            {
                stateController = GetComponent<PlayerStateController>();
            }

            if (eyeControl == null)
            {
                eyeControl = GetComponent<PlayerEyeControl>();
            }

            if (interactionController == null)
            {
                interactionController = GetComponent<PlayerInteractionController>();
            }

            if (disablerDevice == null)
            {
                disablerDevice = GetComponentInChildren<DisablerDevice>();
            }

            if (interactionController != null)
            {
                interactionController.SetEyeControl(eyeControl);
                interactionController.SetDisablerDevice(disablerDevice);
            }

            if (movementBehaviours != null && movementBehaviours.Length > 0)
            {
                movementInitiallyEnabled = new bool[movementBehaviours.Length];
                for (int i = 0; i < movementBehaviours.Length; i++)
                {
                    var behaviour = movementBehaviours[i];
                    movementInitiallyEnabled[i] = behaviour != null && behaviour.enabled;
                }
            }
        }

        private void OnEnable()
        {
            if (stateController != null)
            {
                stateController.PlayerKilled += HandlePlayerKilled;
            }

            if (eyeControl != null)
            {
                eyeControl.EyesForcedClosed += HandleEyesForcedClosed;
                eyeControl.EyesForcedOpened += HandleEyesForcedOpened;
            }

            RefreshMovementState();
            RefreshInteractionState();
        }

        private void OnDisable()
        {
            if (stateController != null)
            {
                stateController.PlayerKilled -= HandlePlayerKilled;
            }

            if (eyeControl != null)
            {
                eyeControl.EyesForcedClosed -= HandleEyesForcedClosed;
                eyeControl.EyesForcedOpened -= HandleEyesForcedOpened;
            }

            if (interactionController != null)
            {
                interactionController.DropCarriedItem();
                interactionController.enabled = false;
            }
        }

        private void Update()
        {
            RefreshMovementState();
            RefreshInteractionState();
        }

        private void RefreshMovementState()
        {
            bool shouldMove = stateController == null || stateController.IsAlive;

            if (lockMovementDuringForcedBlink && eyeControl != null && eyeControl.IsForcedClosing)
            {
                shouldMove = false;
            }

            SetMovementEnabled(shouldMove);
        }

        private void RefreshInteractionState()
        {
            if (interactionController == null)
            {
                return;
            }

            interactionController.SetEyeControl(eyeControl);
            interactionController.SetDisablerDevice(disablerDevice);

            bool shouldEnable = stateController == null || stateController.IsAlive;
            if (interactionController.enabled != shouldEnable)
            {
                interactionController.enabled = shouldEnable;
            }
        }

        private void SetMovementEnabled(bool enabled)
        {
            if (movementStateInitialised && movementCurrentlyEnabled == enabled)
            {
                return;
            }

            movementStateInitialised = true;
            movementCurrentlyEnabled = enabled;

            if (movementBehaviours == null)
            {
                return;
            }

            for (int i = 0; i < movementBehaviours.Length; i++)
            {
                var behaviour = movementBehaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                bool wasInitiallyEnabled = movementInitiallyEnabled == null || movementInitiallyEnabled.Length <= i || movementInitiallyEnabled[i];

                if (enabled)
                {
                    if (wasInitiallyEnabled)
                    {
                        behaviour.enabled = true;
                    }
                }
                else
                {
                    behaviour.enabled = false;
                }
            }
        }

        private void HandlePlayerKilled()
        {
            SetMovementEnabled(false);

            if (interactionController != null)
            {
                interactionController.DropCarriedItem();
                interactionController.enabled = false;
            }
        }

        private void HandleEyesForcedClosed()
        {
            if (lockMovementDuringForcedBlink)
            {
                SetMovementEnabled(false);
                RefreshInteractionState();
            }
        }

        private void HandleEyesForcedOpened()
        {
            RefreshMovementState();
            RefreshInteractionState();
        }

    }
}
