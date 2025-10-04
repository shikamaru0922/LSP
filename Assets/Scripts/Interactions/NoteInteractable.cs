using UnityEngine;
using LSP.Gameplay.UI;

namespace LSP.Gameplay.Interactions
{
    /// <summary>
    /// Simple interactable that displays a block of note text through the shared <see cref="NoteUI"/>.
    /// Attach this to note pickups in the scene and populate the body text.
    /// </summary>
    [DisallowMultipleComponent]
    public class NoteInteractable : MonoBehaviour, IInteractable
    {
        [Header("Note Contents")]
        [TextArea]
        [SerializeField]
        private string noteBody;

        [Header("UI")]
        [SerializeField]
        [Tooltip("UI controller that should present this note when interacted with.")]
        private NoteUI noteUi;

        /// <summary>
        /// Body text that will be shown when the player reads the note.
        /// </summary>
        public string NoteBody
        {
            get => noteBody;
            set => noteBody = value;
        }

        /// <summary>
        /// UI controller responsible for showing the note.
        /// </summary>
        public NoteUI NoteUi
        {
            get => noteUi;
            set => noteUi = value;
        }

        /// <inheritdoc />
        public bool CanInteract(PlayerInteractionController caller)
        {
            return noteUi != null;
        }

        /// <inheritdoc />
        public void Interact(PlayerInteractionController caller)
        {
            if (noteUi == null)
            {
                return;
            }

            noteUi.ShowNote(noteBody);
        }
    }
}
