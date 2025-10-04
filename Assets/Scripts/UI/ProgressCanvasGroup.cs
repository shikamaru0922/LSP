using UnityEngine;
using UnityEngine.UI;

namespace LSP.UI
{
    /// <summary>
    /// Controls a CanvasGroup-driven progress display so interaction prompts can fade in and out smoothly.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProgressCanvasGroup : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private Slider progressSlider;

        [SerializeField]
        [Min(0.01f)]
        private float fadeSpeed = 10f;

        [SerializeField]
        private bool interactableWhenVisible;

        [SerializeField]
        private bool blocksRaycastsWhenVisible;

        private bool targetVisible;
        private bool initialised;

        /// <summary>
        /// Current progress value displayed on the slider.
        /// </summary>
        public float Progress { get; private set; }

        private void Awake()
        {
            EnsureReferences();
            ApplyVisibilityInstant(false);
        }

        private void EnsureReferences()
        {
            if (initialised)
            {
                return;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (progressSlider == null)
            {
                progressSlider = GetComponentInChildren<Slider>(true);
            }

            initialised = true;
        }

        private void Update()
        {
            float targetAlpha = targetVisible ? 1f : 0f;
            float newAlpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
            canvasGroup.alpha = newAlpha;

            bool visibleNow = newAlpha > 0f;
            canvasGroup.interactable = visibleNow && interactableWhenVisible;
            canvasGroup.blocksRaycasts = visibleNow && blocksRaycastsWhenVisible;

            if (Mathf.Approximately(newAlpha, targetAlpha) && !targetVisible)
            {
                enabled = false;
            }
        }

        /// <summary>
        /// Immediately hides the UI without animation.
        /// </summary>
        public void HideImmediate()
        {
            EnsureReferences();
            ApplyVisibilityInstant(false);
        }

        /// <summary>
        /// Immediately shows the UI without animation.
        /// </summary>
        public void ShowImmediate()
        {
            EnsureReferences();
            ApplyVisibilityInstant(true);
        }

        /// <summary>
        /// Starts fading the UI in.
        /// </summary>
        public void Show()
        {
            EnsureReferences();
            targetVisible = true;
            enabled = true;
        }

        /// <summary>
        /// Starts fading the UI out.
        /// </summary>
        public void Hide()
        {
            EnsureReferences();
            targetVisible = false;
            enabled = true;
        }

        /// <summary>
        /// Sets the slider value (clamped to [0,1]).
        /// </summary>
        public void SetProgress(float value)
        {
            EnsureReferences();
            Progress = Mathf.Clamp01(value);
            if (progressSlider != null)
            {
                progressSlider.value = Progress;
            }
        }

        private void ApplyVisibilityInstant(bool visible)
        {
            targetVisible = visible;
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible && interactableWhenVisible;
            canvasGroup.blocksRaycasts = visible && blocksRaycastsWhenVisible;
            enabled = false;
        }
    }
}
