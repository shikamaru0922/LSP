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

        [Header("Blink Fade Durations")]
        [Tooltip("Seconds used to fade in the overlay for manual blinks.")]
        [SerializeField]
        private float manualFadeInDuration = 0.08f;

        [Tooltip("Seconds used to fade out the overlay after a manual blink.")]
        [SerializeField]
        private float manualFadeOutDuration = 0.12f;

        [Tooltip("Seconds used to fade in the overlay for forced blinks.")]
        [SerializeField]
        private float forcedFadeInDuration = 0.15f;

        [Tooltip("Seconds used to fade out the overlay after a forced blink.")]
        [SerializeField]
        private float forcedFadeOutDuration = 0.25f;

        private Camera attachedCamera;
        private Coroutine blinkRoutine;

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
                eyeControl.BlinkStarted += HandleBlinkStarted;
                eyeControl.BlinkEnded += HandleBlinkEnded;
            }

            UpdateOverlayImmediate();
        }

        private void OnDisable()
        {
            if (blinkRoutine != null)
            {
                StopCoroutine(blinkRoutine);
                blinkRoutine = null;
            }

            if (eyeControl != null)
            {
                eyeControl.EyesForcedClosed -= HandleEyesForcedClosed;
                eyeControl.EyesForcedOpened -= HandleEyesForcedOpened;
                eyeControl.BlinkStarted -= HandleBlinkStarted;
                eyeControl.BlinkEnded -= HandleBlinkEnded;
            }
        }

        private void LateUpdate()
        {
            if (eyeControl == null)
            {
                return;
            }

            if (!eyeControl.IsBlinking)
            {
                UpdateOverlayImmediate();
            }
        }

        private void HandleEyesForcedClosed()
        {
            UpdateOverlayImmediate();
        }

        private void HandleEyesForcedOpened()
        {
            UpdateOverlayImmediate();
        }

        private void HandleBlinkStarted(PlayerEyeControl.BlinkType blinkType, float duration)
        {
            if (eyelidOverlay == null)
            {
                return;
            }

            if (blinkRoutine != null)
            {
                StopCoroutine(blinkRoutine);
            }

            blinkRoutine = StartCoroutine(BlinkRoutine(blinkType, Mathf.Max(0f, duration)));
        }

        private void HandleBlinkEnded(PlayerEyeControl.BlinkType blinkType)
        {
            if (eyeControl != null && eyeControl.IsBlinking)
            {
                return;
            }

            UpdateOverlayImmediate();
        }

        private System.Collections.IEnumerator BlinkRoutine(PlayerEyeControl.BlinkType blinkType, float duration)
        {
            float fadeIn = blinkType == PlayerEyeControl.BlinkType.Forced ? forcedFadeInDuration : manualFadeInDuration;
            float fadeOut = blinkType == PlayerEyeControl.BlinkType.Forced ? forcedFadeOutDuration : manualFadeOutDuration;

            fadeIn = Mathf.Max(0f, fadeIn);
            fadeOut = Mathf.Max(0f, fadeOut);

            float fadeSum = fadeIn + fadeOut;
            if (fadeSum > duration && fadeSum > 0f)
            {
                float scale = duration / fadeSum;
                fadeIn *= scale;
                fadeOut *= scale;
                fadeSum = fadeIn + fadeOut;
            }

            float holdDuration = Mathf.Max(0f, duration - fadeSum);

            yield return FadeOverlay(1f, fadeIn);

            if (holdDuration > 0f)
            {
                yield return new WaitForSeconds(holdDuration);
            }

            yield return FadeOverlay(0f, fadeOut);

            blinkRoutine = null;
            UpdateOverlayImmediate();
        }

        private System.Collections.IEnumerator FadeOverlay(float targetAlpha, float duration)
        {
            if (eyelidOverlay == null)
            {
                yield break;
            }

            float startAlpha = eyelidOverlay.alpha;
            if (Mathf.Approximately(duration, 0f))
            {
                eyelidOverlay.alpha = targetAlpha;
                eyelidOverlay.blocksRaycasts = targetAlpha > 0f;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                eyelidOverlay.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                eyelidOverlay.blocksRaycasts = eyelidOverlay.alpha > 0f;
                yield return null;
            }

            eyelidOverlay.alpha = targetAlpha;
            eyelidOverlay.blocksRaycasts = targetAlpha > 0f;
        }

        private void UpdateOverlayImmediate()
        {
            if (eyelidOverlay == null)
            {
                return;
            }

            bool eyesCurrentlyOpen = eyeControl == null || eyeControl.EyesOpen;
            eyelidOverlay.alpha = eyesCurrentlyOpen ? 0f : 1f;
            eyelidOverlay.blocksRaycasts = !eyesCurrentlyOpen;
        }
    }
}
