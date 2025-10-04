using UnityEngine;

namespace LSP.Gameplay
{
    public enum DisablerState
    {
        Broken,
        Repaired,
        Charged
    }

    /// <summary>
    /// Controls the repair, charging and usage loops of the disabler item.
    /// </summary>
    public class DisablerDevice : MonoBehaviour
    {
        [SerializeField]
        private int fragmentsRequired = 3;

        [SerializeField]
        private float chargeDuration = 4f;

        [SerializeField]
        private float effectRadius = 10f;

        [SerializeField]
        [Tooltip("If enabled, repairing immediately sets the disabler to Charged so it can be used without an extra charging step.")]
        private bool useImmediatelyWhenRepaired = true;

        [SerializeField]
        private Transform effectOrigin;

        private int collectedFragments;
        private float chargeProgress;

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
            if (useImmediatelyWhenRepaired)
            {
                chargeProgress = chargeDuration;
                CurrentState = DisablerState.Charged;
            }
            else
            {
                chargeProgress = 0f;
                CurrentState = DisablerState.Repaired;
            }
            return true;
        }

        /// <summary>
        /// Progressively charges the device. Returns true once it is fully charged.
        /// </summary>
        public bool Charge(float deltaTime)
        {
            if (CurrentState == DisablerState.Charged)
            {
                return true;
            }

            if (CurrentState != DisablerState.Repaired)
            {
                return false;
            }

            chargeProgress += deltaTime;
            if (chargeProgress >= chargeDuration)
            {
                chargeProgress = chargeDuration;
                CurrentState = DisablerState.Charged;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Uses the device, applying its effect to all monsters inside its radius.
        /// The device returns to the repaired state afterwards, requiring another charge cycle.
        /// </summary>
        public void Use()
        {
            if (CurrentState != DisablerState.Charged)
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

            CurrentState = DisablerState.Repaired;
            chargeProgress = 0f;
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
