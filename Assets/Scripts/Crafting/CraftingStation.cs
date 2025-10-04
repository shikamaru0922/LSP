using LSP.Gameplay.Interactions;
using LSP.Gameplay.UI;
using UnityEngine;

namespace LSP.Gameplay.Crafting
{
    /// <summary>
    /// Represents a station that repairs the player's disabler when fragments are delivered.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftingStation : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [SerializeField]
        [Tooltip("Maximum distance the player can stand from the station while crafting.")]
        private float interactionRadius = 2.5f;

        [SerializeField]
        [Tooltip("How long the player must hold the interaction key to finish crafting.")]
        private float craftingDuration = 3f;

        [SerializeField]
        [Tooltip("Maximum angle in degrees that the player can look away from the station before crafting cancels.")]
        private float viewAngleTolerance = 20f;

        [Header("Output")]
        [SerializeField]
        [Tooltip("Mount where the repaired disabler prefab is spawned upon completion.")]
        private Transform repairedDisablerMount;

        [SerializeField]
        [Tooltip("Prefab instantiated when the disabler is repaired.")]
        private GameObject repairedDisablerPrefab;

        [SerializeField]
        [Tooltip("Fragments required to craft a disabler when no device has been assigned yet.")]
        private int fallbackFragmentsRequired = 3;

        [Header("UI")]
        [SerializeField]
        private CraftingUI craftingUi;

        private PlayerInteractionController activePlayer;
        private Camera activeCamera;
        private float craftingProgress;
        private bool isCrafting;
        private bool hasCrafted;
        private int cachedPrefabFragmentsRequired = -1;

        private Transform CraftingFocus => repairedDisablerMount != null ? repairedDisablerMount : transform;

        private void Awake()
        {
            if (craftingUi == null)
            {
                craftingUi = GetComponentInChildren<CraftingUI>();
            }
        }

        private void Update()
        {
            if (!isCrafting || activePlayer == null)
            {
                return;
            }

            if (!IsPlayerWithinRange(activePlayer) || !IsPlayerLookingAtStation() || !IsPlayerHoldingInteract())
            {
                CancelCrafting();
                return;
            }

            craftingProgress += Time.deltaTime;
            float normalised = Mathf.Clamp01(craftingProgress / Mathf.Max(craftingDuration, Mathf.Epsilon));
            craftingUi?.UpdateProgress(normalised);

            if (craftingProgress >= craftingDuration)
            {
                CompleteCrafting();
            }
        }

        /// <inheritdoc />
        public bool CanInteract(PlayerInteractionController caller)
        {
            if (!isActiveAndEnabled || hasCrafted)
            {
                return false;
            }

            if (caller == null)
            {
                return false;
            }

            if (isCrafting && caller != activePlayer)
            {
                return false;
            }

            var device = caller.DisablerDevice;

            if (device != null)
            {
                if (device.CurrentState != DisablerState.Broken)
                {
                    return false;
                }

                if (device.CollectedFragments < device.FragmentsRequired)
                {
                    return false;
                }
            }
            else
            {
                int required = GetFragmentsRequired(caller);
                if (caller.PendingDisablerFragments < required)
                {
                    return false;
                }
            }

            return IsPlayerWithinRange(caller);
        }

        /// <inheritdoc />
        public void Interact(PlayerInteractionController caller)
        {
            if (!CanInteract(caller))
            {
                return;
            }

            BeginCrafting(caller);
        }

        private void BeginCrafting(PlayerInteractionController caller)
        {
            activePlayer = caller;
            activeCamera = caller.GetComponentInChildren<Camera>();
            if (activeCamera == null)
            {
                activeCamera = Camera.main;
            }

            craftingProgress = 0f;
            isCrafting = true;

            if (craftingUi != null)
            {
                craftingUi.Show(0f);
            }

            activePlayer.IsUiOpen = true;
        }

        private void CancelCrafting()
        {
            craftingProgress = 0f;
            isCrafting = false;

            if (craftingUi != null)
            {
                craftingUi.Hide();
            }

            if (activePlayer != null)
            {
                activePlayer.IsUiOpen = false;
            }

            activePlayer = null;
            activeCamera = null;
        }

        private void CompleteCrafting()
        {
            isCrafting = false;

            craftingUi?.UpdateProgress(1f);

            bool repaired = false;
            if (activePlayer != null)
            {
                var device = activePlayer.DisablerDevice;
                if (device != null)
                {
                    repaired = device.TryRepair();
                }
                else
                {
                    int required = GetFragmentsRequired(activePlayer);
                    repaired = activePlayer.TrySpendPendingDisablerFragments(required);
                }
            }

            hasCrafted = repaired;

            if (repaired)
            {
                SpawnRepairedDisabler();
            }

            craftingUi?.Hide();

            if (activePlayer != null)
            {
                activePlayer.IsUiOpen = false;
            }

            activePlayer = null;
            activeCamera = null;
            craftingProgress = 0f;
        }

        private void SpawnRepairedDisabler()
        {
            if (repairedDisablerPrefab == null)
            {
                return;
            }

            var mount = CraftingFocus;
            Instantiate(repairedDisablerPrefab, mount.position, mount.rotation, mount);
        }

        private int GetFragmentsRequired(PlayerInteractionController caller)
        {
            if (caller != null && caller.DisablerDevice != null)
            {
                return caller.DisablerDevice.FragmentsRequired;
            }

            if (cachedPrefabFragmentsRequired > 0)
            {
                return cachedPrefabFragmentsRequired;
            }

            if (repairedDisablerPrefab != null && repairedDisablerPrefab.TryGetComponent(out DisablerDevice prefabDevice))
            {
                cachedPrefabFragmentsRequired = prefabDevice.FragmentsRequired;
                return cachedPrefabFragmentsRequired;
            }

            return Mathf.Max(1, fallbackFragmentsRequired);
        }

        private bool IsPlayerWithinRange(PlayerInteractionController caller)
        {
            if (caller == null)
            {
                return false;
            }

            float distance = Vector3.Distance(caller.transform.position, CraftingFocus.position);
            return distance <= interactionRadius;
        }

        private bool IsPlayerHoldingInteract()
        {
            if (activePlayer == null)
            {
                return false;
            }

            return Input.GetKey(activePlayer.InteractKey);
        }

        private bool IsPlayerLookingAtStation()
        {
            if (activeCamera == null)
            {
                return true;
            }

            if (viewAngleTolerance <= 0f)
            {
                return true;
            }

            Vector3 directionToFocus = (CraftingFocus.position - activeCamera.transform.position).normalized;
            if (directionToFocus.sqrMagnitude <= Mathf.Epsilon)
            {
                return true;
            }

            float angle = Vector3.Angle(activeCamera.transform.forward, directionToFocus);
            return angle <= viewAngleTolerance;
        }

        private void OnDisable()
        {
            if (isCrafting)
            {
                CancelCrafting();
            }
        }
    }
}
