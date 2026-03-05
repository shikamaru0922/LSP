using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LSP.Gameplay
{
    /// <summary>
    /// Updates a UI slider to reflect the player's current eye wetness level.
    /// </summary>
    [DisallowMultipleComponent]
    public class EyeWetnessUI : MonoBehaviour
    {
        [Header("Wetness Slider")]
        [SerializeField]
        private Slider wetnessSlider;

        [SerializeField]
        private PlayerEyeControl eyeControl;

        [Tooltip("When enabled the slider displays a 0-1 normalised value instead of raw wetness.")]
        [SerializeField]
        private bool useNormalisedValue = true;

        [Header("Eye State Visual")]
        [SerializeField]
        [Tooltip("When enabled this component also drives the eye icon state (open/closed/bloodshot).")]
        private bool syncEyeBlinkVisual = true;

        [SerializeField]
        [Tooltip("Primary eye icon image that switches between open and closed sprites.")]
        private Image eyeStateImage;

        [SerializeField]
        [Tooltip("Optional overlay image used for bloodshot feedback during forced/warning blink states.")]
        private Image bloodshotOverlayImage;

        [SerializeField]
        [Tooltip("Sprite shown while the player's eyes are open.")]
        private Sprite openEyeSprite;

        [SerializeField]
        [Tooltip("Sprite shown while the player's eyes are closed.")]
        private Sprite closedEyeSprite;

        [SerializeField]
        [Tooltip("Sprite used by the bloodshot overlay image.")]
        private Sprite bloodshotSprite;

        [SerializeField]
        [Tooltip("Show bloodshot overlay while a forced blink is active.")]
        private bool showBloodshotOnForcedBlink = true;

        [SerializeField]
        [Tooltip("Show bloodshot overlay while warning flashes are active.")]
        private bool showBloodshotOnWarningBlink = true;

        [SerializeField]
        [Tooltip("If enabled, automatically discovers eye images from sibling objects under the parent canvas.")]
        private bool autoDiscoverEyeImages = true;

        private bool eyeVisualReferencesResolved;

        private void Awake()
        {
            if (wetnessSlider == null)
            {
                wetnessSlider = GetComponentInChildren<Slider>();
            }

            if (eyeControl == null)
            {
                eyeControl = FindObjectOfType<PlayerEyeControl>();
            }

            ResolveEyeVisualReferences();
        }

        private void OnEnable()
        {
            UpdateSliderImmediate();
            UpdateEyeStateVisual();
        }

        private void Update()
        {
            UpdateSliderImmediate();
            UpdateEyeStateVisual();
        }

        private void OnDisable()
        {
            SetBloodshotVisible(false);
        }

        private void UpdateSliderImmediate()
        {
            if (wetnessSlider == null || eyeControl == null)
            {
                return;
            }

            float maxWetness = Mathf.Max(eyeControl.MaximumWetness, Mathf.Epsilon);
            float wetnessValue = Mathf.Clamp(eyeControl.CurrentWetness, 0f, maxWetness);

            if (useNormalisedValue)
            {
                wetnessSlider.normalizedValue = wetnessValue / maxWetness;
            }
            else
            {
                wetnessSlider.maxValue = maxWetness;
                wetnessSlider.value = wetnessValue;
            }
        }

        private void UpdateEyeStateVisual()
        {
            if (!syncEyeBlinkVisual)
            {
                SetBloodshotVisible(false);
                return;
            }

            if (eyeControl == null)
            {
                eyeControl = FindObjectOfType<PlayerEyeControl>();
                if (eyeControl == null)
                {
                    SetBloodshotVisible(false);
                    return;
                }
            }

            ResolveEyeVisualReferences();

            if (eyeStateImage != null)
            {
                var targetSprite = eyeControl.EyesOpen ? openEyeSprite : closedEyeSprite;
                if (targetSprite != null && eyeStateImage.sprite != targetSprite)
                {
                    eyeStateImage.sprite = targetSprite;
                }
            }

            bool showBloodshot =
                (showBloodshotOnForcedBlink && eyeControl.IsForcedClosing) ||
                (showBloodshotOnWarningBlink && eyeControl.IsWarningBlinking);

            SetBloodshotVisible(showBloodshot);
        }

        private void ResolveEyeVisualReferences()
        {
            if (!syncEyeBlinkVisual || eyeVisualReferencesResolved)
            {
                return;
            }

            if (autoDiscoverEyeImages)
            {
                TryDiscoverEyeImages();
            }

            if (eyeStateImage != null && openEyeSprite == null)
            {
                openEyeSprite = eyeStateImage.sprite;
            }

            if (bloodshotOverlayImage != null)
            {
                if (bloodshotSprite == null)
                {
                    bloodshotSprite = bloodshotOverlayImage.sprite;
                }

                if (bloodshotSprite != null && bloodshotOverlayImage.sprite != bloodshotSprite)
                {
                    bloodshotOverlayImage.sprite = bloodshotSprite;
                }
            }

            eyeVisualReferencesResolved =
                eyeStateImage != null ||
                bloodshotOverlayImage != null ||
                !autoDiscoverEyeImages;
        }

        private void TryDiscoverEyeImages()
        {
            if (transform.parent == null)
            {
                return;
            }

            var candidates = new List<Image>();
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == null || sibling == transform)
                {
                    continue;
                }

                if (sibling.TryGetComponent(out Image image))
                {
                    candidates.Add(image);
                }
            }

            if (eyeStateImage == null && candidates.Count > 0)
            {
                eyeStateImage = candidates[0];
            }

            if (bloodshotOverlayImage == null && candidates.Count > 1)
            {
                bloodshotOverlayImage = candidates[1];
            }
        }

        private void SetBloodshotVisible(bool visible)
        {
            if (bloodshotOverlayImage == null)
            {
                return;
            }

            if (bloodshotSprite != null && bloodshotOverlayImage.sprite != bloodshotSprite)
            {
                bloodshotOverlayImage.sprite = bloodshotSprite;
            }

            bloodshotOverlayImage.enabled = visible;
        }
    }
}
