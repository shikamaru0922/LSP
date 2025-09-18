using System;
using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Manages the player's eye wetness resource, supporting manual blinking and forced closing.
    /// </summary>
    public class PlayerEyeControl : MonoBehaviour
    {
        [Header("Wetness Settings")]
        [SerializeField]
        private float maximumWetness = 5f;

        [SerializeField]
        private float dryingRate = 1f;

        [SerializeField]
        private float recoveryRate = 2f;

        [Tooltip("Minimum wetness required before the player can open their eyes after a forced blink.")]
        [SerializeField]
        private float forcedOpenThreshold = 1.5f;

        [Tooltip("Seconds the player must keep their eyes shut when wetness hits zero.")]
        [SerializeField]
        private float forcedCloseDuration = 2.5f;

        [Header("Input")]
        [SerializeField]
        private KeyCode manualCloseKey = KeyCode.Space;

        private float currentWetness;
        private float forcedCloseTimer;
        private bool eyesOpen = true;
        private bool isManuallyClosing;

        public float CurrentWetness => currentWetness;

        public float MaximumWetness
        {
            get => maximumWetness;
            set
            {
                maximumWetness = Mathf.Max(0.01f, value);
                if (forcedOpenThreshold > maximumWetness)
                {
                    forcedOpenThreshold = maximumWetness;
                }

                currentWetness = Mathf.Clamp(currentWetness, 0f, maximumWetness);
            }
        }

        public float DryingRate
        {
            get => dryingRate;
            set => dryingRate = Mathf.Max(0f, value);
        }

        public float RecoveryRate
        {
            get => recoveryRate;
            set => recoveryRate = Mathf.Max(0f, value);
        }

        public float ForcedOpenThreshold
        {
            get => forcedOpenThreshold;
            set
            {
                forcedOpenThreshold = Mathf.Clamp(value, 0f, maximumWetness);

                if (!eyesOpen && !IsForcedClosing && !isManuallyClosing && currentWetness >= forcedOpenThreshold)
                {
                    eyesOpen = true;
                }
            }
        }

        public float ForcedCloseDuration
        {
            get => forcedCloseDuration;
            set
            {
                forcedCloseDuration = Mathf.Max(0f, value);

                if (IsForcedClosing)
                {
                    forcedCloseTimer = Mathf.Min(forcedCloseTimer, forcedCloseDuration);
                }
            }
        }

        public bool EyesOpen => eyesOpen;
        public bool IsForcedClosing => forcedCloseTimer > 0f;

        public event Action EyesForcedClosed;
        public event Action EyesForcedOpened;

        private void Awake()
        {
            currentWetness = maximumWetness;
        }

        private void Update()
        {
            HandleInput();
            UpdateWetness(Time.deltaTime);
        }

        private void HandleInput()
        {
            if (IsForcedClosing)
            {
                // Player has no control while forced closed.
                isManuallyClosing = true;
                eyesOpen = false;
                return;
            }

            isManuallyClosing = Input.GetKey(manualCloseKey);
            eyesOpen = !isManuallyClosing;
        }

        private void UpdateWetness(float deltaTime)
        {
            if (eyesOpen)
            {
                currentWetness -= dryingRate * deltaTime;
                if (currentWetness <= 0f)
                {
                    currentWetness = 0f;
                    BeginForcedClose();
                }
            }
            else
            {
                RecoverWetness(deltaTime);
            }

            currentWetness = Mathf.Clamp(currentWetness, 0f, maximumWetness);
        }

        private void RecoverWetness(float deltaTime)
        {
            currentWetness += recoveryRate * deltaTime;

            if (IsForcedClosing)
            {
                forcedCloseTimer -= deltaTime;

                if (forcedCloseTimer <= 0f && currentWetness >= forcedOpenThreshold)
                {
                    forcedCloseTimer = 0f;
                    eyesOpen = true;
                    EyesForcedOpened?.Invoke();
                }
            }
            else if (!isManuallyClosing && currentWetness >= forcedOpenThreshold)
            {
                eyesOpen = true;
            }
        }

        private void BeginForcedClose()
        {
            if (IsForcedClosing)
            {
                return;
            }

            forcedCloseTimer = forcedCloseDuration;
            eyesOpen = false;
            EyesForcedClosed?.Invoke();
        }

        /// <summary>
        /// Instantly restores the wetness resource. Useful when restarting a level.
        /// </summary>
        public void ResetWetness()
        {
            currentWetness = maximumWetness;
            forcedCloseTimer = 0f;
            eyesOpen = true;
        }
    }
}
