using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Bridges the existing player prefab camera hierarchy with the gaze-driven
    /// mechanics scripts. The binder sits on the active player camera and wires the
    /// camera into the <see cref="PlayerVision"/> and <see cref="PlayerEyeControl"/>
    /// components that might live on parent objects.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class PlayerCameraBinder : MonoBehaviour
    {
        [SerializeField]
        private PlayerVision playerVision;

        [SerializeField]
        private PlayerEyeControl eyeControl;

        [Tooltip("Optional overlay that will be faded in while the player's eyes are closed.")]
        [SerializeField]
        private CanvasGroup eyelidOverlay;

        private Camera attachedCamera;

        private void Awake()
        {
            attachedCamera = GetComponent<Camera>();

            if (playerVision == null)
            {
                playerVision = GetComponentInParent<PlayerVision>();
            }

            if (eyeControl == null && playerVision != null)
            {
                eyeControl = playerVision.GetComponent<PlayerEyeControl>();
            }

            if (eyeControl == null)
            {
                eyeControl = GetComponentInParent<PlayerEyeControl>();
            }

            if (playerVision != null)
            {
                playerVision.SetViewCamera(attachedCamera);

                if (eyeControl != null)
                {
                    playerVision.SetEyeControl(eyeControl);
                }
            }
        }

        private void OnEnable()
        {
            if (eyeControl != null)
            {
                eyeControl.EyesForcedClosed += HandleEyesForcedClosed;
                eyeControl.EyesForcedOpened += HandleEyesForcedOpened;
            }

            UpdateOverlay();
        }

        private void OnDisable()
        {
            if (eyeControl != null)
            {
                eyeControl.EyesForcedClosed -= HandleEyesForcedClosed;
                eyeControl.EyesForcedOpened -= HandleEyesForcedOpened;
            }
        }

        private void LateUpdate()
        {
            UpdateOverlay();
        }

        private void HandleEyesForcedClosed()
        {
            UpdateOverlay();
        }

        private void HandleEyesForcedOpened()
        {
            UpdateOverlay();
        }

        private void UpdateOverlay()
        {
            if (eyelidOverlay == null)
            {
                return;
            }

            bool eyesCurrentlyOpen = eyeControl == null || (eyeControl.EyesOpen && !eyeControl.IsForcedClosing);
            eyelidOverlay.alpha = eyesCurrentlyOpen ? 0f : 1f;
            eyelidOverlay.blocksRaycasts = !eyesCurrentlyOpen;
        }
    }
}
