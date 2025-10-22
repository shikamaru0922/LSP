using UnityEngine;
using UnityEngine.SceneManagement;

namespace LSP.Gameplay
{
    /// <summary>
    /// Loads a target scene when the player enters the trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SceneRedirectTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Scene that will be loaded when the player enters the trigger.")]
        private string targetSceneName;

        [SerializeField]
        [Tooltip("Name of the tag that identifies the player.")]
        private string playerTag = "Player";

        [SerializeField]
        [Tooltip("If true, the trigger can only be used once.")]
        private bool triggerOnce = true;

        private bool hasTriggered;

        private void Reset()
        {
            var collider = GetComponent<Collider>();
            collider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && hasTriggered)
            {
                return;
            }

            if (!other.CompareTag(playerTag))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogWarning("SceneRedirectTrigger has no target scene assigned.");
                return;
            }

            hasTriggered = true;
            SceneManager.LoadScene(targetSceneName);
        }
    }
}
