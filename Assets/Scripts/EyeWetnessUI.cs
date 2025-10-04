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
        [SerializeField]
        private Slider wetnessSlider;

        [SerializeField]
        private PlayerEyeControl eyeControl;

        [Tooltip("When enabled the slider displays a 0-1 normalised value instead of raw wetness.")]
        [SerializeField]
        private bool useNormalisedValue = true;

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
        }

        private void OnEnable()
        {
            UpdateSliderImmediate();
        }

        private void Update()
        {
            UpdateSliderImmediate();
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
    }
}
