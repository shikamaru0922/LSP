using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace LSP.Gameplay.UI
{
    /// <summary>
    /// Controls the in-game note overlay. Attach this component to the dedicated
    /// world-space note canvas and assign the <see cref="CanvasGroup"/> plus the
    /// TextMeshPro text element that should display the body of the note.
    /// Hook the optional interaction controller reference to automatically
    /// suspend player interactions while the note is visible.
    /// </summary>
    [DisallowMultipleComponent]
    public class NoteUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("Canvas group that controls visibility of the note panel.")]
        private CanvasGroup noteCanvasGroup;

        [SerializeField]
        [Tooltip("TextMeshPro component that renders the body of the note.")]
        private TMP_Text noteBodyText;

        [Header("Input")]
        [SerializeField]
        private KeyCode dismissKey = KeyCode.Escape;

        [Header("Fade Durations")]
        [SerializeField]
        [Tooltip("Seconds used to fade in the note overlay. Set to 0 for an instant show.")]
        private float fadeInDuration = 0.15f;

        [SerializeField]
        [Tooltip("Seconds used to fade out the note overlay. Set to 0 for an instant hide.")]
        private float fadeOutDuration = 0.12f;

        [Header("Dependencies")]
        [SerializeField]
        [Tooltip("Player interaction controller that should be disabled while the note is open.")]
        private PlayerInteractionController interactionController;

        private Coroutine fadeRoutine;
        private bool noteVisible;
        private bool cursorOverrideActive;
        private CursorLockMode cachedCursorLockMode;
        private bool cachedCursorVisible;

        /// <summary>
        /// Gets or sets the interaction controller that should be suspended while the note is open.
        /// </summary>
        public PlayerInteractionController InteractionController
        {
            get => interactionController;
            set => interactionController = value;
        }

        /// <summary>
        /// Raised when the note overlay becomes visible.
        /// </summary>
        public event Action NoteOpened;

        /// <summary>
        /// Raised once the note overlay has completely closed.
        /// </summary>
        public event Action NoteClosed;

        /// <summary>
        /// Gets a value indicating whether the note overlay is currently visible to the player.
        /// </summary>
        public bool IsVisible => noteVisible || cursorOverrideActive;

        /// <summary>
        /// Displays the supplied note body text, showing and fading in the overlay if required.
        /// </summary>
        /// <param name="bodyText">Body text to populate inside the note.</param>
        public void ShowNote(string bodyText)
        {
            if (noteBodyText != null)
            {
                noteBodyText.text = bodyText ?? string.Empty;
            }

            bool wasVisible = noteVisible;
            noteVisible = true;

            if (!wasVisible)
            {
                SuppressInteraction(true);
                BeginCursorOverride();
                NoteOpened?.Invoke();
            }

            BeginFade(1f, fadeInDuration);
        }

        /// <summary>
        /// Hides the note overlay and restores the player's normal controls.
        /// </summary>
        public void Hide()
        {
            if (!noteVisible && fadeRoutine == null && !cursorOverrideActive)
            {
                return;
            }

            noteVisible = false;
            BeginFade(0f, fadeOutDuration);
        }

        private void Awake()
        {
            HideImmediate();
        }

        private void Update()
        {
            if (!IsVisible)
            {
                return;
            }

            if (Input.GetKeyDown(dismissKey))
            {
                Hide();
            }
        }

        private void OnDisable()
        {
            HideImmediate();
        }

        private void HideImmediate()
        {
            bool wasVisible = IsVisible || noteVisible;

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            if (noteCanvasGroup != null)
            {
                noteCanvasGroup.alpha = 0f;
                noteCanvasGroup.blocksRaycasts = false;
                noteCanvasGroup.interactable = false;
            }

            noteVisible = false;
            EndCursorOverride();
            SuppressInteraction(false);

            if (wasVisible)
            {
                NoteClosed?.Invoke();
            }
        }

        private void BeginFade(float targetAlpha, float duration)
        {
            if (noteCanvasGroup == null)
            {
                if (Mathf.Approximately(targetAlpha, 0f))
                {
                    CompleteHide();
                }
                else
                {
                    EnsureCanvasEnabled();
                }

                return;
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, Mathf.Max(0f, duration)));
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            EnsureCanvasEnabled();

            if (Mathf.Approximately(duration, 0f))
            {
                noteCanvasGroup.alpha = targetAlpha;
                yield return null;
                fadeRoutine = null;
                HandleFadeComplete(targetAlpha);
                yield break;
            }

            float startAlpha = noteCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                noteCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            noteCanvasGroup.alpha = targetAlpha;
            fadeRoutine = null;
            HandleFadeComplete(targetAlpha);
        }

        private void HandleFadeComplete(float targetAlpha)
        {
            if (Mathf.Approximately(targetAlpha, 0f))
            {
                CompleteHide();
            }
            else
            {
                EnsureCanvasEnabled();
            }
        }

        private void EnsureCanvasEnabled()
        {
            if (noteCanvasGroup == null)
            {
                return;
            }

            noteCanvasGroup.alpha = Mathf.Max(noteCanvasGroup.alpha, 0f);
            noteCanvasGroup.blocksRaycasts = true;
            noteCanvasGroup.interactable = true;
        }

        private void CompleteHide()
        {
            if (noteCanvasGroup != null)
            {
                noteCanvasGroup.alpha = 0f;
                noteCanvasGroup.blocksRaycasts = false;
                noteCanvasGroup.interactable = false;
            }

            noteVisible = false;
            EndCursorOverride();
            SuppressInteraction(false);
            NoteClosed?.Invoke();
        }

        private void SuppressInteraction(bool suppress)
        {
            if (interactionController != null)
            {
                interactionController.IsUiOpen = suppress;
            }
        }

        private void BeginCursorOverride()
        {
            if (cursorOverrideActive)
            {
                return;
            }

            cachedCursorLockMode = Cursor.lockState;
            cachedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            cursorOverrideActive = true;
        }

        private void EndCursorOverride()
        {
            if (!cursorOverrideActive)
            {
                return;
            }

            Cursor.lockState = cachedCursorLockMode;
            Cursor.visible = cachedCursorVisible;
            cursorOverrideActive = false;
        }
    }
}
