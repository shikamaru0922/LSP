using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Provides helper utilities to test whether an actor is inside the player's field of view.
    /// </summary>
    public class PlayerVision : MonoBehaviour
    {
        [SerializeField]
        private Camera viewCamera;

        [SerializeField]
        private PlayerEyeControl eyeControl;

        [Header("Occlusion")]
        [Tooltip("Layers considered when checking if geometry blocks line of sight to the target.")]
        [SerializeField]
        private LayerMask occlusionLayers = Physics.DefaultRaycastLayers;

        [Min(0f)]
        [SerializeField]
        private float maxDetectionDistance = 999f;

        private Plane[] cachedPlanes;
        private int cachedFrame = -1;

        private void Awake()
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (eyeControl == null)
            {
                eyeControl = GetComponent<PlayerEyeControl>();
            }
        }

        /// <summary>
        /// Determines whether a target collider is currently visible to the player.
        /// Visibility checks require the player's eyes to be open and unobstructed by
        /// occluding geometry.
        /// </summary>
        public bool CanSee(Collider targetCollider)
        {
            if (targetCollider == null)
            {
                return false;
            }

            return CanSeeBounds(targetCollider.bounds, targetCollider);
        }

        /// <summary>
        /// Determines whether a target bounds is currently visible to the player.
        /// Visibility checks require the player's eyes to be open.
        /// </summary>
        public bool CanSee(Bounds bounds)
        {
            return CanSeeBounds(bounds, null);
        }

        private bool CanSeeBounds(Bounds bounds, Collider targetCollider)
        {
            if (viewCamera == null || (eyeControl != null && !eyeControl.EyesOpen))
            {
                return false;
            }

            CacheFrustumPlanes();
            if (!GeometryUtility.TestPlanesAABB(cachedPlanes, bounds))
            {
                return false;
            }

            if (maxDetectionDistance > 0f)
            {
                Vector3 viewerPosition = viewCamera.transform.position;
                Vector3 closestPoint = bounds.ClosestPoint(viewerPosition);
                float sqrDistance = (closestPoint - viewerPosition).sqrMagnitude;
                float sqrRange = maxDetectionDistance * maxDetectionDistance;
                if (sqrDistance > sqrRange)
                {
                    return false;
                }
            }

            return HasLineOfSight(bounds, targetCollider);
        }

        private bool HasLineOfSight(Bounds bounds, Collider targetCollider)
        {
            if (occlusionLayers == 0)
            {
                // Empty mask disables occlusion testing
                return true;
            }

            Vector3 viewerPosition = viewCamera.transform.position;
            Vector3 extents = bounds.extents;

            foreach (Vector3 offset in OcclusionSampleOffsets)
            {
                Vector3 samplePoint = bounds.center + Vector3.Scale(extents, offset);
                Vector3 direction = samplePoint - viewerPosition;
                float distance = direction.magnitude;

                if (distance <= Mathf.Epsilon)
                {
                    return true;
                }

                if (!Physics.Raycast(viewerPosition, direction / distance, out RaycastHit hit, distance, occlusionLayers, QueryTriggerInteraction.Ignore))
                {
                    // Nothing hit: line of sight is clear
                    return true;
                }

                // If we hit the target (or its hierarchy), that's also clear
                if (targetCollider != null && IsPartOfTarget(hit.collider, targetCollider))
                {
                    return true;
                }
            }

            // All samples blocked by non-target geometry
            return false;
        }

        private static bool IsPartOfTarget(Collider hitCollider, Collider targetCollider)
        {
            if (hitCollider == null || targetCollider == null)
            {
                return false;
            }

            if (hitCollider == targetCollider)
            {
                return true;
            }

            Transform hitTransform = hitCollider.transform;
            Transform targetTransform = targetCollider.transform;
            return hitTransform == targetTransform
                   || hitTransform.IsChildOf(targetTransform)
                   || targetTransform.IsChildOf(hitTransform)
                   || hitTransform.root == targetTransform.root;
        }

        private static readonly Vector3[] OcclusionSampleOffsets =
        {
            Vector3.zero,
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
            new Vector3(1f, 1f, 0f),  new Vector3(1f, -1f, 0f),
            new Vector3(-1f, 1f, 0f), new Vector3(-1f, -1f, 0f),
            new Vector3(1f, 0f, 1f),  new Vector3(1f, 0f, -1f),
            new Vector3(-1f, 0f, 1f), new Vector3(-1f, 0f, -1f),
            new Vector3(0f, 1f, 1f),  new Vector3(0f, 1f, -1f),
            new Vector3(0f, -1f, 1f), new Vector3(0f, -1f, -1f)
        };

        private void CacheFrustumPlanes()
        {
            if (Time.frameCount == cachedFrame && cachedPlanes != null)
            {
                return;
            }

            cachedPlanes = GeometryUtility.CalculateFrustumPlanes(viewCamera);
            cachedFrame = Time.frameCount;
        }

        /// <summary>
        /// Overrides the camera used to determine the player's field of view.
        /// This is useful when wiring the script to an existing prefab that already
        /// manages its own camera hierarchy.
        /// </summary>
        public void SetViewCamera(Camera camera)
        {
            viewCamera = camera;
            cachedFrame = -1;
        }

        /// <summary>
        /// Injects the eye control dependency at runtime, allowing prefabs with
        /// pre-configured controllers to provide their existing component.
        /// </summar
