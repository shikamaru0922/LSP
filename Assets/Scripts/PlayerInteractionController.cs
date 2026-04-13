using System;
using UnityEngine;
using UnityEngine.UI;
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
        [Min(0f)]
        private float interactionDistance = 2f;

        [SerializeField]
        [Tooltip("Physics layers considered valid when searching for interactables.")]
        private LayerMask interactableLayers = ~0;

        [Header("Interaction Feedback")]
        [SerializeField]
        [Tooltip("Optional UI graphic used as the center crosshair. Its color changes when an interactable is in focus.")]
        private Graphic crosshairGraphic;

        [SerializeField]
        [Tooltip("If enabled, the crosshair color changes when an interactable is in focus.")]
        private bool useCrosshairColorFeedback = false;

        [SerializeField]
        [Tooltip("If enabled, the crosshair swaps between idle/interaction sprites.")]
        private bool useCrosshairSpriteFeedback = true;

        [SerializeField]
        [Tooltip("Default color applied to the crosshair when no interactable is targeted.")]
        private Color defaultCrosshairColor = Color.white;

        [SerializeField]
        [Tooltip("Color applied to the crosshair while aiming at a valid interactable.")]
        private Color interactableCrosshairColor = Color.green;

        [SerializeField]
        [Tooltip("If enabled, the default crosshair color is captured from the assigned graphic during Awake.")]
        private bool captureDefaultCrosshairColorOnAwake = true;

        [SerializeField]
        [Tooltip("Optional UI object shown when the player can interact (for example, a Press F icon).")]
        private GameObject interactPromptObject;

        [SerializeField]
        [Tooltip("Idle crosshair sprite (for example, F.png).")]
        private Sprite idleCrosshairSprite;

        [SerializeField]
        [Tooltip("Interactable crosshair sprite (for example, F_onclick.png).")]
        private Sprite interactableCrosshairSprite;

        [SerializeField]
        [Tooltip("Crosshair sprite shown while holding the interaction key on a valid target.")]
        private Sprite pressedInteractableCrosshairSprite;

        [SerializeField]
        [Tooltip("How much larger the focused F prompt should be than the idle dot.")]
        [Min(1f)]
        private float focusedCrosshairScaleMultiplier = 7f;

        [SerializeField]
        [Tooltip("Optional UI object shown while there is no interactable focus (for example, an idle F icon).")]
        private GameObject interactPromptIdleObject;

        [SerializeField]
        [Tooltip("If enabled, the controller tries to auto-locate F prompt UI objects by name when fields are unassigned.")]
        private bool autoResolvePromptObjects = false;

        [SerializeField]
        [Tooltip("If no prompt object can be found, create a simple runtime F text prompt so interaction feedback still works.")]
        private bool createFallbackPromptWhenMissing = false;

        [Header("Carrying")]
        [SerializeField]
        [Tooltip("Optional transform that defines the position/rotation used when carrying interactable items.")]
        private Transform carryAnchor;

        [Header("Dependencies")]
        [SerializeField]
        private PlayerEyeControl eyeControl;

        [SerializeField]
        private DisablerDevice disablerDevice;

        [Header("Disabler Usage")]
        [SerializeField]
        [Tooltip("Key used to trigger the disabler device while interacting with it.")]
        private KeyCode disablerUseKey = KeyCode.Q;

        private IInteractable currentInteractable;
        private InteractableItem carriedItem;
        private bool uiOpen;
        private int pendingDisablerFragments;
        private bool feedbackActive;
        private bool feedbackPressed;
        private Image cachedCrosshairImage;
        private Vector2 defaultCrosshairSizeDelta;
        private bool defaultCrosshairSizeCaptured;
        private static readonly string[] ActivePromptNameTokens =
        {
            "fonclick",
            "fkeyonclick",
            "fbuttononclick",
            "finteractactive",
            "f键onclick",
            "f键按下"
        };

        private static readonly string[] IdlePromptNameTokens =
        {
            "f",
            "fkey",
            "fbutton",
            "fprompt",
            "finteract",
            "f键"
        };

        private static readonly string[] CrosshairRootNameTokens =
        {
            "corsshair",
            "crosshair"
        };

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
        /// The number of disabler fragments collected while no device was assigned.
        /// </summary>
        public int PendingDisablerFragments => pendingDisablerFragments;

        /// <summary>
        /// Gets the key used to activate the disabler device while interacting with it.
        /// </summary>
        public KeyCode DisablerUseKey => disablerUseKey;

        /// <summary>
        /// Exposes the key used to trigger interactions so other systems can detect holds.
        /// </summary>
        public KeyCode InteractKey => interactKey;

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

            if (crosshairGraphic != null && captureDefaultCrosshairColorOnAwake)
            {
                defaultCrosshairColor = crosshairGraphic.color;
            }

            ResolvePromptObjects();
            CacheCrosshairVisualDefaults();
            SetInteractionFeedback(false, true);
        }

        private void OnDisable()
        {
            ClearFocus();
            DropCarriedItem();
            SetInteractionFeedback(false, true);
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

            if (disablerDevice != null && pendingDisablerFragments > 0)
            {
                int before = disablerDevice.CollectedFragments;
                int newCount = disablerDevice.AddRepairFragments(pendingDisablerFragments);
                int consumed = Mathf.Clamp(newCount - before, 0, pendingDisablerFragments);
                pendingDisablerFragments = Mathf.Max(0, pendingDisablerFragments - consumed);
            }
        }

        /// <summary>
        /// Stores disabler fragments collected while the player has no device.
        /// </summary>
        public void AddPendingDisablerFragments(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            pendingDisablerFragments = Mathf.Max(0, pendingDisablerFragments + amount);
        }

        /// <summary>
        /// Attempts to spend pending fragments that were collected without a device.
        /// </summary>
        public bool TrySpendPendingDisablerFragments(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (pendingDisablerFragments < amount)
            {
                return false;
            }

            pendingDisablerFragments -= amount;
            return true;
        }

        /// <summary>
        /// Sets the eye control dependency used to determine when the player is blinking.
        /// </summary>
        public void SetEyeControl(PlayerEyeControl control)
        {
            if (control == null)
            {
                return;
            }

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
            SetInteractionFeedback(true);
        }

        private void ClearFocus()
        {
            currentInteractable = null;
            SetInteractionFeedback(false);
        }

        private void ResolvePromptObjects()
        {
            TryAutoResolveCrosshairGraphic();

            if (!autoResolvePromptObjects)
            {
                return;
            }

            if (interactPromptObject == null)
            {
                interactPromptObject = TryResolvePromptObject(ActivePromptNameTokens);
            }

            if (interactPromptIdleObject == null)
            {
                interactPromptIdleObject = TryResolvePromptObject(IdlePromptNameTokens);
            }

            if (interactPromptIdleObject == interactPromptObject)
            {
                interactPromptIdleObject = null;
            }

            if (interactPromptObject == null && createFallbackPromptWhenMissing)
            {
                interactPromptObject = CreateFallbackPromptObject();
            }
        }

        private void TryAutoResolveCrosshairGraphic()
        {
            if (crosshairGraphic != null)
            {
                return;
            }

            Transform[] allTransforms = FindObjectsOfType<Transform>(true);
            GameObject crosshairRoot = TryFindPromptByName(allTransforms, CrosshairRootNameTokens);
            if (crosshairRoot == null)
            {
                return;
            }

            crosshairGraphic = crosshairRoot.GetComponentInChildren<Graphic>(true);
            if (crosshairGraphic != null && captureDefaultCrosshairColorOnAwake)
            {
                defaultCrosshairColor = crosshairGraphic.color;
            }
        }

        private void CacheCrosshairVisualDefaults()
        {
            if (!(crosshairGraphic is Image crosshairImage))
            {
                return;
            }

            cachedCrosshairImage = crosshairImage;

            if (!defaultCrosshairSizeCaptured && crosshairImage.rectTransform != null)
            {
                defaultCrosshairSizeDelta = crosshairImage.rectTransform.sizeDelta;
                defaultCrosshairSizeCaptured = true;
            }

            if (idleCrosshairSprite == null)
            {
                idleCrosshairSprite = crosshairImage.sprite;
            }
        }

        private void ApplyCrosshairSize(bool focused)
        {
            if (!defaultCrosshairSizeCaptured || cachedCrosshairImage == null || cachedCrosshairImage.rectTransform == null)
            {
                return;
            }

            float multiplier = focused ? Mathf.Max(1f, focusedCrosshairScaleMultiplier) : 1f;
            cachedCrosshairImage.rectTransform.sizeDelta = defaultCrosshairSizeDelta * multiplier;
        }

        private GameObject TryResolvePromptObject(string[] normalizedNameTokens)
        {
            GameObject nearCrosshair = TryFindPromptNearCrosshair(normalizedNameTokens);
            if (nearCrosshair != null)
            {
                return nearCrosshair;
            }

            Transform[] allTransforms = FindObjectsOfType<Transform>(true);
            return TryFindPromptByName(allTransforms, normalizedNameTokens);
        }

        private GameObject TryFindPromptNearCrosshair(string[] normalizedNameTokens)
        {
            if (crosshairGraphic == null)
            {
                return null;
            }

            Transform root = crosshairGraphic.transform.parent != null
                ? crosshairGraphic.transform.parent
                : crosshairGraphic.transform;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            return TryFindPromptByName(transforms, normalizedNameTokens);
        }

        private static GameObject TryFindPromptByName(Transform[] transforms, string[] normalizedNameTokens)
        {
            if (transforms == null || normalizedNameTokens == null || normalizedNameTokens.Length == 0)
            {
                return null;
            }

            foreach (Transform candidate in transforms)
            {
                if (candidate == null)
                {
                    continue;
                }

                string normalizedCandidateName = NormalizeObjectName(candidate.name);
                if (string.IsNullOrEmpty(normalizedCandidateName))
                {
                    continue;
                }

                foreach (string token in normalizedNameTokens)
                {
                    if (!IsNameTokenMatch(normalizedCandidateName, token))
                    {
                        continue;
                    }

                    return candidate.gameObject;
                }
            }

            return null;
        }

        private static bool IsNameTokenMatch(string normalizedCandidateName, string token)
        {
            if (string.IsNullOrEmpty(normalizedCandidateName) || string.IsNullOrEmpty(token))
            {
                return false;
            }

            if (normalizedCandidateName == token)
            {
                return true;
            }

            if (token.Length >= 6 && normalizedCandidateName.IndexOf(token, StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return false;
        }

        private static string NormalizeObjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            char[] buffer = new char[name.Length];
            int length = 0;

            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    continue;
                }

                buffer[length++] = char.ToLowerInvariant(c);
            }

            return length == 0 ? string.Empty : new string(buffer, 0, length);
        }

        private GameObject CreateFallbackPromptObject()
        {
            if (crosshairGraphic == null)
            {
                return null;
            }

            Transform parent = crosshairGraphic.transform.parent != null
                ? crosshairGraphic.transform.parent
                : crosshairGraphic.transform;

            var prompt = new GameObject("F_Prompt_Auto", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            prompt.transform.SetParent(parent, false);

            var promptRect = prompt.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.anchoredPosition = Vector2.zero;
            promptRect.localScale = Vector3.one;

            if (crosshairGraphic.rectTransform != null)
            {
                promptRect.sizeDelta = crosshairGraphic.rectTransform.sizeDelta;
            }
            else
            {
                promptRect.sizeDelta = new Vector2(100f, 100f);
            }

            var promptText = prompt.GetComponent<Text>();
            promptText.text = interactKey.ToString().ToUpperInvariant();
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color = Color.white;
            promptText.resizeTextForBestFit = true;
            promptText.resizeTextMinSize = 16;
            promptText.resizeTextMaxSize = 72;
            promptText.raycastTarget = false;
            promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (promptText.font == null)
            {
                promptText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            prompt.SetActive(false);
            return prompt;
        }

        private void SetInteractionFeedback(bool hasInteractableFocus, bool force = false)
        {
            bool isInteractPressed = hasInteractableFocus && Input.GetKey(interactKey);

            if (!force && feedbackActive == hasInteractableFocus && feedbackPressed == isInteractPressed)
            {
                return;
            }

            feedbackActive = hasInteractableFocus;
            feedbackPressed = isInteractPressed;

            if (crosshairGraphic != null)
            {
                if (cachedCrosshairImage == null)
                {
                    CacheCrosshairVisualDefaults();
                }

                if (useCrosshairSpriteFeedback && cachedCrosshairImage != null)
                {
                    Sprite targetSprite = idleCrosshairSprite;
                    if (hasInteractableFocus)
                    {
                        targetSprite = isInteractPressed && pressedInteractableCrosshairSprite != null
                            ? pressedInteractableCrosshairSprite
                            : interactableCrosshairSprite;
                    }

                    if (targetSprite != null)
                    {
                        cachedCrosshairImage.sprite = targetSprite;
                        cachedCrosshairImage.preserveAspect = true;
                    }

                    ApplyCrosshairSize(hasInteractableFocus);
                }

                if (useCrosshairColorFeedback)
                {
                    crosshairGraphic.color = hasInteractableFocus ? interactableCrosshairColor : defaultCrosshairColor;
                }
                else
                {
                    crosshairGraphic.color = defaultCrosshairColor;
                }
            }

            if (interactPromptIdleObject != null && interactPromptIdleObject != interactPromptObject)
            {
                interactPromptIdleObject.SetActive(!hasInteractableFocus);
            }

            if (interactPromptObject != null)
            {
                interactPromptObject.SetActive(hasInteractableFocus);
            }
        }
    }
}
