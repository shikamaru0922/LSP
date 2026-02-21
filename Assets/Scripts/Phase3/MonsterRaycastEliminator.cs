using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Provides a UnityEvent-friendly flow to enable an aiming reticle,
    /// then let the player shoot a ray with left mouse to eliminate active monsters.
    /// </summary>
    public class MonsterRaycastEliminator : MonoBehaviour
    {
        [Header("Aim Setup")]
        [SerializeField]
        [Tooltip("Camera used to cast the elimination ray. Defaults to Camera.main when empty.")]
        private Camera aimCamera;

        [SerializeField]
        [Tooltip("Optional crosshair object that will be shown/hidden when aim mode toggles.")]
        private GameObject crosshair;

        [SerializeField]
        [Tooltip("Physics layers considered by the elimination raycast.")]
        private LayerMask raycastLayers = ~0;

        [SerializeField]
        [Tooltip("Maximum distance for elimination raycasts.")]
        [Min(0f)]
        private float raycastDistance = 100f;

        [Header("Input")]
        [SerializeField]
        [Tooltip("Mouse button index used to fire. 0 = left mouse button.")]
        private int fireMouseButton = 0;

        [SerializeField]
        [Tooltip("If true, aim mode is active when this component starts.")]
        private bool startAiming;

        private bool aimModeActive;

        /// <summary>
        /// Whether raycast elimination is currently active.
        /// </summary>
        public bool AimModeActive => aimModeActive;

        private void Awake()
        {
            if (aimCamera == null)
            {
                aimCamera = GetComponentInChildren<Camera>();
                if (aimCamera == null)
                {
                    aimCamera = Camera.main;
                }
            }

            SetAimMode(startAiming);
        }

        private void Update()
        {
            if (!aimModeActive)
            {
                return;
            }

            if (!Input.GetMouseButtonDown(fireMouseButton))
            {
                return;
            }

            TryEliminateMonsterByRaycast();
        }

        /// <summary>
        /// UnityEvent entrypoint: enable aiming and show crosshair.
        /// </summary>
        public void ActivateAimMode()
        {
            SetAimMode(true);
        }

        /// <summary>
        /// UnityEvent entrypoint: disable aiming and hide crosshair.
        /// </summary>
        public void DeactivateAimMode()
        {
            SetAimMode(false);
        }

        /// <summary>
        /// UnityEvent entrypoint: fires one shot immediately (only if aim mode is active).
        /// Useful for button-driven testing.
        /// </summary>
        public void FireOnce()
        {
            if (!aimModeActive)
            {
                return;
            }

            TryEliminateMonsterByRaycast();
        }

        private void SetAimMode(bool enabled)
        {
            aimModeActive = enabled;

            if (crosshair != null)
            {
                crosshair.SetActive(enabled);
            }
        }

        private void TryEliminateMonsterByRaycast()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
                if (aimCamera == null)
                {
                    Debug.LogWarning("MonsterRaycastEliminator: no camera available for raycasting.", this);
                    return;
                }
            }

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayers, QueryTriggerInteraction.Collide))
            {
                return;
            }

            MonsterController monster = hit.collider.GetComponentInParent<MonsterController>();
            if (monster == null)
            {
                return;
            }

            bool monsterIsActive = monster.isActiveAndEnabled && monster.gameObject.activeInHierarchy;
            if (!monsterIsActive)
            {
                return;
            }

            Destroy(monster.gameObject);
        }
    }
}