using UnityEngine;
using UnityEngine.UI;

namespace LSP.Gameplay.UI
{
    /// <summary>
    /// Displays crafting progress using a slider inside a world space canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftingUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Canvas that should be enabled while the crafting UI is visible.")]
        private Canvas rootCanvas;

        [SerializeField]
        [Tooltip("Slider used to visualise crafting progress.")]
        private Slider progressSlider;

        private void Awake()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInChildren<Canvas>();
            }

            if (progressSlider == null)
            {
                progressSlider = GetComponentInChildren<Slider>();
            }

            Hide();
        }

        /// <summary>
        /// Makes the crafting UI visible and updates the displayed progress value.
        /// </summary>
        /// <param name="normalisedProgress">Progress value in the 0-1 range.</param>
        public void Show(float normalisedProgress)
        {
            SetCanvasVisible(true);
            UpdateProgress(normalisedProgress);
        }

        /// <summary>
        /// Hides the crafting UI.
        /// </summary>
        public void Hide()
        {
            SetCanvasVisible(false);
        }

        /// <summary>
        /// Updates the slider to match the supplied progress value.
        /// </summary>
        /// <param name="normalisedProgress">Progress value in the 0-1 range.</param>
        public void UpdateProgress(float normalisedProgress)
        {
            if (progressSlider == null)
            {
                return;
            }

            progressSlider.normalizedValue = Mathf.Clamp01(normalisedProgress);
        }

        private void SetCanvasVisible(bool visible)
        {
            if (rootCanvas != null)
            {
                rootCanvas.enabled = visible;
                return;
            }

            gameObject.SetActive(visible);
        }
    }
}
