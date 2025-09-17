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
        /// Determines whether a target bounds is currently visible to the player.
        /// Visibility checks require the player's eyes to be open.
        /// </summary>
        public bool CanSee(Bounds bounds)
        {
            if (viewCamera == null || (eyeControl != null && !eyeControl.EyesOpen))
            {
                return false;
            }

            CacheFrustumPlanes();
            return GeometryUtility.TestPlanesAABB(cachedPlanes, bounds);
        }

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
        /// </summary>
        public void SetEyeControl(PlayerEyeControl control)
        {
            eyeControl = control;
        }
    }
}
