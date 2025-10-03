using UnityEngine;
using LSP.Gameplay.Interactions;

namespace LSP.Gameplay
{
    /// <summary>
    /// Handles player driven interactions by raycasting from the active camera and invoking
    /// the focused <see cref="IInteractable"/> when prompted.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractionController : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField]
        private KeyCode interactKey = KeyCode.F;

        [SerializeField]
        [Tooltip("Camera used to perform interaction raycasts. Defaults to the main camera if left empty.")]
        private Camera interactionCamera;

        [SerializeField]
        [Tooltip("Maximum distance from the camera that the player can interact with objects.")]
        private float interactionDistance = 3f;

        [SerializeField]
        [Tooltip("Physics layers considered valid when searching for interactables.")]
        private LayerMask interactableLayers = ~0;

        [Header("Carrying")]
        [SerializeField]
        [Tooltip("Optional transform that defines the position/rotation used when carrying interactable items.")]
        private Transform carryAnchor;

        [Header("Dependencies")]
        [SerializeField]
        private PlayerEyeControl eyeControl;

        [SerializeField]
        private DisablerDevice disablerDevice;

        private IInteractable currentInteractable;
        private InteractableItem carriedItem;
        private bool uiOpen;

        /// <summary>
        /// Gets or sets a value indicating whether the player's interaction input is currently blocked by UI.
        /// </summary>
        public bool IsUiOpen
        {
            get => uiOpen;
            set => uiOpen = value;
        }

        /// <summary>
        /// Returns the transform used as the anchor for carried items.
        /// </summary>
        public Transform CarryAnchor
        {
            get
            {
                if (carryAnchor != null)
                {
                    return carryAnchor;
                }

                if (interactionCamera != null)
                {
                    return interactionCamera.transform;
                }

                return transform;
            }
        }

        /// <summary>
        /// Provides the active disabler device so consumable items can update fragment counts.
        /// </summary>
        public DisablerDevice DisablerDevice => disablerDevice;

        /// <summary>
        /// The interactable item currently being carried by the player, if any.
        /// </summary>
        public InteractableItem CarriedItem => carriedItem;

        private void Awake()
        {
            if (interactionCamera == null)
            {
                interactionCamera = GetComponentInChildren<Camera>();
                if (interactionCamera == null)
                {
                    interactionCamera = Camera.main;
                }
            }

            if (eyeControl == null)
            {
                eyeControl = GetComponent<PlayerEyeControl>();
            }

            if (eyeControl == null)
            {
                eyeControl = GetComponentInChildren<PlayerEyeControl>();
            }
        }

        private void OnDisable()
        {
            ClearFocus();
            DropCarriedItem();
        }

        private void Update()
        {
            if (interactionCamera == null)
            {
                return;
            }

            if (IsInteractionSuspended())
            {
                ClearFocus();
                return;
            }

            UpdateFocus();
            HandleInteractionInput();
        }

        /// <summary>
        /// Updates the reference to the disabler device that should receive fragment counts.
        /// </summary>
        public void SetDisablerDevice(DisablerDevice device)
        {
            disablerDevice = device;
        }

        /// <summary>
        /// Sets the eye control dependency used to determine when the player is blinking.
        /// </summary>
        public void SetEyeControl(PlayerEyeControl control)
        {
            eyeControl = control;
        }

        /// <summary>
        /// Drops the currently carried item (if any) and restores its original transform hierarchy.
        /// </summary>
        public void DropCarriedItem()
        {
            if (carriedItem == null)
            {
                return;
            }

            var item = carriedItem;
            carriedItem = null;
            item.ReleaseFromCarrier();
        }

        internal void NotifyItemCarried(InteractableItem item)
        {
            if (carriedItem != null && carriedItem != item)
            {
                DropCarriedItem();
            }

            carriedItem = item;
        }

        internal void NotifyItemReleased(InteractableItem item)
        {
            if (carriedItem == item)
            {
                carriedItem = null;
            }
        }

        private bool IsInteractionSuspended()
        {
            if (!isActiveAndEnabled)
            {
                return true;
            }

            if (uiOpen)
            {
                return true;
            }

            return eyeControl != null && eyeControl.IsBlinking;
        }

        private void HandleInteractionInput()
        {
            if (!Input.GetKeyDown(interactKey))
            {
                return;
            }

            if (carriedItem != null)
            {
                carriedItem.HandleInteractWhileCarried(this);
                return;
            }

            if (currentInteractable == null)
            {
                return;
            }

            if (!currentInteractable.CanInteract(this))
            {
                return;
            }

            currentInteractable.Interact(this);
        }

        private void UpdateFocus()
        {
            var ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out var hit, interactionDistance, interactableLayers, QueryTriggerInteraction.Collide))
            {
                ClearFocus();
                return;
            }

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(this))
            {
                ClearFocus();
                return;
            }

            currentInteractable = interactable;
        }

        private void ClearFocus()
        {
            currentInteractable = null;
        }
    }
}
