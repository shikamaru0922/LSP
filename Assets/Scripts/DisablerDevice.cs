using UnityEngine;

namespace LSP.Gameplay
{
    public enum DisablerState
    {
        Broken,
        Ready
    }

    /// <summary>
    /// Controls the repair and usage loops of the disabler item.
    /// </summary>
    public class DisablerDevice : MonoBehaviour
    {
        [SerializeField]
        private int fragmentsRequired = 3;

        [SerializeField]
        private float effectRadius = 10f;

        [SerializeField]
        [Tooltip("If enabled, repairing immediately sets the disabler to Charged so it can be used without an extra charging step.")]
        private bool useImmediatelyWhenRepaired = true;

        [SerializeField]
        private Transform effectOrigin;

        private int collectedFragments;

        public DisablerState CurrentState { get; private set; } = DisablerState.Broken;

        public int FragmentsRequired => fragmentsRequired;

        public int CollectedFragments => collectedFragments;

        private void Awake()
        {
            if (effectOrigin == null)
            {
                effectOrigin = transform;
            }
        }

        /// <summary>
        /// Adds a repair fragment to the device. Once enough fragments are gathered the player can repair it.
        /// </summary>
        public void AddRepairFragment()
        {
            AddRepairFragments(1);
        }

        /// <summary>
        /// Adds multiple repair fragments to the device at once. Returns the updated fragment count.
        /// </summary>
        public int AddRepairFragments(int amount)
        {
            if (amount <= 0)
            {
                return collectedFragments;
            }

            collectedFragments = Mathf.Clamp(collectedFragments + amount, 0, fragmentsRequired);
            return collectedFragments;
        }

        /// <summary>
        /// Attempts to repair the device. This should be called when the player interacts with the repair station.
        /// </summary>
        public bool TryRepair()
        {
            if (CurrentState != DisablerState.Broken || collectedFragments < fragmentsRequired)
            {
                return false;
            }

            collectedFragments = Mathf.Max(0, collectedFragments - fragmentsRequired);
            CurrentState = DisablerState.Ready;
            return true;
        }

        /// <summary>
        /// Uses the device, applying its effect to all monsters inside its radius.
        /// The device returns to the broken state afterwards, requiring another repair cycle.
        /// </summary>
        public void Use()
        {
            if (CurrentState != DisablerState.Ready)
            {
                return;
            }

            Collider[] hits = Physics.OverlapSphere(effectOrigin.position, effectRadius);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<MonsterController>(out var monster))
                {
                    monster.ApplyDisablerEffect();
                }
            }

            CurrentState = DisablerState.Broken;
        }

        private void OnDrawGizmosSelected()
        {
            if (effectOrigin == null)
            {
                effectOrigin = transform;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(effectOrigin.position, effectRadius);
        }
    }
}
