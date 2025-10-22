using UnityEngine;
using UnityEngine.SceneManagement;

namespace LSP.Gameplay
{
    /// <summary>
    /// Handles the logic for the restart menu that appears after the player dies or reaches safety.
    /// </summary>
    public class RestartMenuController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Scene that will be loaded when the player chooses to replay.")]
        private string gameplaySceneName;

        [SerializeField]
        [Tooltip("Optional cursor visibility override when the menu loads.")]
        private bool showCursor = true;

        [SerializeField]
        [Tooltip("Whether to unlock the cursor when the menu loads.")]
        private bool unlockCursor = true;

        private void Awake()
        {
            if (showCursor)
            {
                Cursor.visible = true;
            }

            if (unlockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }

        public void Replay()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogWarning("RestartMenuController has no gameplay scene assigned to reload.");
                return;
            }

            SceneManager.LoadScene(gameplaySceneName);
        }

        public void Quit()
        {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
            Application.Quit();
    #endif
        }
    }
}
