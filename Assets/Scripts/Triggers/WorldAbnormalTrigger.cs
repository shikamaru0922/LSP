using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Sets the world abnormal state when the player enters the trigger.
    /// The GameManager reference can be assigned directly from the inspector.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WorldAbnormalTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional GameManager override. If not set, the singleton instance is used.")]
        private GameManager gameManager;

        [SerializeField]
        [Tooltip("Name of the player tag allowed to trigger the abnormal state change.")]
        private string playerTag = "Player";

        [SerializeField]
        [Tooltip("World abnormal state value applied when the player enters the trigger.")]
        private bool abnormalState = true;

        [SerializeField]
        [Tooltip("If true, the trigger only fires once and then becomes inactive.")]
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

            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
            }

            if (gameManager == null)
            {
                Debug.LogWarning("WorldAbnormalTrigger could not find a GameManager to update.");
                return;
            }

            gameManager.SetWorldAbnormalState(abnormalState);
            hasTriggered = true;
        }
    }
}
