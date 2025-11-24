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
            if (wetnessSlider != null)
            {
                wetnessSlider.gameObject.SetActive(false);
            }

            enabled = false;
        }
    }
}
